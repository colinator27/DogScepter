using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    public static class SwitchStatements
    {
        /// Finds all the switch statements within a list of blocks
        public static List<SwitchStatement> Find(DecompileContext ctx)
        {
            List<SwitchStatement> res = new List<SwitchStatement>();

            foreach (Block b in ctx.Blocks.List)
            {
                if (b.Instructions.Count >= 1)
                {
                    var instr = b.Instructions[0];
                    if (instr.Kind == Instruction.Opcode.Popz)
                    {
                        // This is most likely the end of a switch statement (repeat loops already abstracted out anyway)
                        // It only can't be if this is inside another switch and we exit the script
                        Block header = null;
                        Block continueBlock = null;
                        Block endCasesBranch = null;
                        Block defaultCaseBranch = null;
                        List<Block> caseBranches = new List<Block>();
                        bool empty;
                        if (b.Predecessors.Count == 1)
                        {
                            header = (Block)b.Predecessors[0];
                            empty = true;

                            // Do check for exit
                            if (b.Instructions.Count == 2 && b.Instructions[1].Kind == Instruction.Opcode.Exit)
                            {
                                // Abort. This isn't a switch statement; it's an exit inside of another switch
                                continue;
                            }
                        }
                        else
                        {
                            empty = false;

                            // Do check for exit
                            if (b.Instructions.Count > 2 && b.Instructions[^1].Kind == Instruction.Opcode.Exit)
                            {
                                bool shouldAbort = true;
                                for (int i = 0; i < b.Instructions.Count - 1; i++)
                                    if (b.Instructions[i].Kind != Instruction.Opcode.Popz)
                                        shouldAbort = false;
                                if (shouldAbort)
                                {
                                    // Abort. This isn't a switch statement; it's an exit inside of other switches/repeats
                                    continue;
                                }
                            }

                            // Find the end of the chain of cases (using "header" as a temporary variable here)
                            for (int i = 0; i < b.Predecessors.Count; i++)
                            {
                                if ((b.Predecessors[i] as Block)?.LastInstr.Kind == Instruction.Opcode.B)
                                {
                                    header = b.Predecessors[i] as Block;
                                    break;
                                }
                            }
                            // Work backwards to the first one in this statement
                            while (header.Index > 0)
                            {
                                Block next = ctx.Blocks.List[header.Index - 1];
                                if (next.Instructions.Count >= 1 && next.Instructions[^1].Kind == Instruction.Opcode.B &&
                                    next.Branches[0].Address <= b.Address)
                                    header = next;
                                else
                                    break;
                            }
                            endCasesBranch = header;

                            // Go backwards, searching for the real header
                            int startCaseBodies = int.MaxValue;
                            while (header.Index > 0)
                            {
                                Block next = ctx.Blocks.List[header.Index - 1];
                                if (next.Instructions.Count >= 4 && next.Instructions[^1].Kind == Instruction.Opcode.Bt)
                                {
                                    startCaseBodies = Math.Min(startCaseBodies, next.Branches[0].Address);
                                    caseBranches.Add(next);
                                    header = next;
                                }
                                else
                                    break;
                            }
                            caseBranches.Reverse(); // They're in the wrong order, so reverse the list

                            // Check if this switch has a default case or not, and register it
                            if (endCasesBranch.Index + 1 < ctx.Blocks.List.Count)
                            {
                                Block maybeRealEnd = ctx.Blocks.List[endCasesBranch.Index + 1];
                                if (maybeRealEnd.Address < startCaseBodies && 
                                    maybeRealEnd.Instructions.Count == 1 && maybeRealEnd.Instructions[0].Kind == Instruction.Opcode.B)
                                {
                                    defaultCaseBranch = endCasesBranch;

                                    // The end case branch is actually one further here
                                    endCasesBranch = maybeRealEnd;
                                }
                            }

                            // Also check if there's a continue block right before the tail
                            if (b.Index > 0 && ctx.Blocks.List[b.Index - 1].ControlFlow == Block.ControlFlowType.Continue)
                                continueBlock = ctx.Blocks.List[b.Index - 1];
                        }

                        // Find surrounding loop for later
                        Loop surroundingLoop = null;
                        foreach (var loop in ctx.LoopNodes)
                            if (loop.Address < header.Address && loop.EndAddress > header.Address && (surroundingLoop == null || loop.Address > surroundingLoop.Address))
                                surroundingLoop = loop;

                        res.Add(new SwitchStatement(header, b, empty, caseBranches, defaultCaseBranch, endCasesBranch, continueBlock, surroundingLoop));
                    }
                }
            }

            // Get into order we want to process (top to bottom, inner to outer), and return
            return res.OrderBy(s => s.EndAddress).ThenByDescending(s => s.Address).ToList();
        }

        /// Inserts switch statement nodes into the graph
        public static void InsertNode(DecompileContext ctx, SwitchStatement s)
        {
            // Transfer predecessors and branches
            s.Predecessors.AddRange(s.Header.Predecessors);
            Node tail;
            if (s.Tail.BelongingTo != null)
                tail = s.Tail.BelongingTo;
            else
                tail = s.Tail;
            s.Branches.Add(tail);

            // Change header predecessors to point to this node instead
            foreach (var node in s.Header.Predecessors)
            {
                for (int i = 0; i < node.Branches.Count; i++)
                {
                    if (node.Branches[i] != node && node.Branches[i] == s.Header)
                        node.Branches[i] = s;
                }
            }

            // Change Tail predecessor to be this statement
            tail.Predecessors.Clear();
            tail.Predecessors.Add(s);

            s.Tail.Instructions.RemoveAt(0); // Remove `popz` in tail

            if (s.Empty)
            {
                s.Header.Instructions.RemoveAt(s.Header.Instructions.Count - 1);
                s.Branches.Add(s.Header);
                s.Header.ControlFlow = Block.ControlFlowType.SwitchExpression;
            }
            else
            {
                // Resolve all "break" and "continue" statements (will be in non-Node blocks still)
                int end;
                if (s.ContinueBlock != null)
                    end = s.ContinueBlock.Index - 1; // There's a block to skip around the continue block too
                else
                    end = s.Tail.Index;
                for (int i = s.EndCasesBranch.Index + 1; i < end; i++)
                {
                    Block curr = ctx.Blocks.List[i];
                    if (curr.Instructions.Count >= 1 && curr.Instructions[^1].Kind == Instruction.Opcode.B)
                    {
                        // This might be a continue or break; need to check which
                        if (curr.Branches[0].Address >= s.EndAddress)
                        {
                            // Must be a break
                            curr.ControlFlow = Block.ControlFlowType.Break;
                        }
                        else // This should be guaranteed by this point, but: if (curr.Branches[0].Address >= continueAddress)
                        {
                            // Must be a continue
                            curr.ControlFlow = Block.ControlFlowType.Continue;
                        }

                        // Clear some data
                        curr.Instructions.RemoveAt(curr.Instructions.Count - 1);

                        // Branch to the next block if necessary (unreachable)
                        if (curr.Index + 1 < ctx.Blocks.List.Count)
                        {
                            Block nextBlock = ctx.Blocks.List[curr.Index + 1];
                            if (nextBlock.Predecessors.Count == 0)
                            {
                                nextBlock.Unreachable = true;
                                curr.Branches.Clear();
                                curr.Branches.Add(nextBlock);
                                nextBlock.Predecessors.Add(curr);
                            }
                        }
                    }
                }

                // Add all the nodes as branches, including cases and default
                int lastBranchAddress = -1;
                int lastIndex = -1;
                int endAddress = ctx.Blocks.List[end].Address;

                void insertCaseBranch(Block curr, Node next, int instructionsRequiredForExpression, bool defaultCase = false)
                {
                    if (lastBranchAddress == -1)
                    {
                        // This is the first case, which may contain extra instructions we want to split
                        Block newBlock = new Block(s.Address, s.Address);
                        newBlock.Instructions.AddRange(curr.Instructions.GetRange(0, curr.Instructions.Count - instructionsRequiredForExpression));
                        curr.Instructions.RemoveRange(0, curr.Instructions.Count - instructionsRequiredForExpression);
                        newBlock.ControlFlow = Block.ControlFlowType.SwitchExpression;
                        s.Branches.Insert(1, newBlock);
                    }
                    else if (defaultCase)
                    {
                        int index = s.Branches.IndexOf(curr.Branches[0]);
                        if (index != -1)
                        {
                            // This is branching to an existing case and needs to be placed up there
                            s.Branches.Insert(index, curr);
                            curr.Branches.Clear();
                            return;
                        }
                    }

                    if (curr.Branches[0].Address == lastBranchAddress)
                        s.Branches.Insert(++lastIndex, curr); // This shares the same node as the previous case
                    else
                    {
                        if (defaultCase)
                        {
                            int i = 2;
                            for (; i < s.Branches.Count; i++)
                            {
                                if (curr.Branches[0].Address < s.Branches[i].Address)
                                {
                                    i--;
                                    break;
                                }
                            }
                            lastIndex = i;
                            lastBranchAddress = curr.Branches[0].Address;
                            if (curr.Branches[0].Address < endAddress) // Prevent adding unnecessary blocks at the end
                                s.Branches.Insert(i, curr.Branches[0]); // todo? maybe wire up predecessors here
                            s.Branches.Insert(i, curr);
                        }
                        else
                        {
                            lastIndex = s.Branches.Count;
                            s.Branches.Add(curr);
                            lastBranchAddress = curr.Branches[0].Address;
                            if (curr.Branches[0].Address < endAddress) // Prevent adding unnecessary blocks at the end
                                s.Branches.Add(curr.Branches[0]); // todo? maybe wire up predecessors here
                        }
                    }

                    // Cut off branches that go into the next case, if necessary
                    if (curr.Branches[0].Address != next.Address)
                    {
                        Stack<Node> work = new Stack<Node>();
                        List<Node> visited = new List<Node>();
                        work.Push(curr.Branches[0]);
                        while (work.Count != 0)
                        {
                            Node n = work.Pop();
                            for (int i = n.Branches.Count - 1; i >= 0; i--)
                            {
                                Node jump = n.Branches[i];
                                if (jump.Address >= next.Address)
                                    n.Branches.RemoveAt(i);
                                else if (!visited.Contains(jump))
                                {
                                    work.Push(jump);
                                    visited.Add(jump);
                                }
                            }
                        }
                    }
                    curr.Branches.Clear();
                }

                for (int i = 0; i < s.CaseBranches.Count; i++)
                {
                    Block curr = s.CaseBranches[i];

                    // Remove some instructions, mark as a case block
                    curr.ControlFlow = Block.ControlFlowType.SwitchCase;
                    curr.Instructions.RemoveAt(curr.Instructions.Count - 4); // Should be `dup`, TODO? Constant should always take one instruction, right?
                    curr.Instructions.RemoveRange(curr.Instructions.Count - 2, 2); // Should be `cmp` and `bt`

                    // Insert the case node
                    Node next;
                    if (i + 1 < s.CaseBranches.Count)
                        next = s.CaseBranches[i + 1].Branches[0];
                    else if (s.DefaultCaseBranch != null && s.DefaultCaseBranch.Branches[0].Address > curr.Branches[0].Address)
                        next = s.DefaultCaseBranch.Branches[0];
                    else if (s.ContinueBlock != null)
                        next = ctx.Blocks.List[s.ContinueBlock.Index - 1]; // Block that jumps past continue block
                    else
                        next = s.Tail;
                    insertCaseBranch(curr, next, 1);

                    curr.Branches.Clear();
                }

                if (s.DefaultCaseBranch != null)
                {
                    // Remove `b` instruction, mark as a case block
                    s.DefaultCaseBranch.ControlFlow = Block.ControlFlowType.SwitchDefault;
                    s.DefaultCaseBranch.Instructions.RemoveAt(s.DefaultCaseBranch.Instructions.Count - 1);

                    // Insert the node
                    Block next;
                    if (s.ContinueBlock != null)
                        next = ctx.Blocks.List[s.ContinueBlock.Index - 1]; // Block that jumps past continue block
                    else
                        next = s.Tail;
                    insertCaseBranch(s.DefaultCaseBranch, next, 0, true);

                    s.DefaultCaseBranch.Branches.Clear();
                }

                // Remove all additional `b` instructions, and do some other processing
                s.EndCasesBranch.Instructions.RemoveAt(s.EndCasesBranch.Instructions.Count - 1);
                if (s.ContinueBlock != null)
                {
                    Block prev = ctx.Blocks.List[s.ContinueBlock.Index - 1];
                    prev.Instructions.RemoveAt(prev.Instructions.Count - 1);

                    // Also do a check here to see if this continue block is inside of a for loop, instead of a while
                    if (s.SurroundingLoop?.LoopKind == Loop.LoopType.While)
                    {
                        Node jumpTarget = s.ContinueBlock.Branches[0];
                        if (jumpTarget != s.SurroundingLoop.Tail || jumpTarget.Address < s.SurroundingLoop.EndAddress - 4) // 4 bytes for the `b` instruction
                        {
                            // This loop can't be a while loop
                            s.SurroundingLoop.LoopKind = Loop.LoopType.For;

                            // Need to rewire predecessors of `jumpTarget` to go nowhere
                            foreach (var pred in jumpTarget.Predecessors)
                            {
                                for (int i = pred.Branches.Count - 1; i >= 0; i--)
                                {
                                    if (pred.Branches[i] == jumpTarget)
                                        pred.Branches.RemoveAt(i);
                                }
                            }

                            s.SurroundingLoop.Branches.Add(jumpTarget);
                            jumpTarget.Predecessors.Clear();
                        }
                    }
                }
            }
        }
    }
}
