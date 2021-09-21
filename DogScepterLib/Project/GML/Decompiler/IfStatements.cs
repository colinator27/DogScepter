using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    public static class IfStatements
    {
        /// Finds all the if statements within a list of blocks
        public static List<IfStatement> Find(DecompileContext ctx)
        {
            List<IfStatement> res = new List<IfStatement>();

            foreach (Block b in ctx.Blocks.List)
            {
                if (b.Instructions.Count >= 1)
                {
                    var instr = b.Instructions[^1];
                    if (instr.Kind == Instruction.Opcode.Bf)
                    {
                        // Any `bf` instruction should be part of an if statement at the normal
                        // point in decompilation (only other control flow type is a switch statement).

                        // Find the surrounding loop for later
                        Loop surroundingLoop = null;
                        foreach (var loop in ctx.Loops)
                            if (loop.Address < b.Address && loop.EndAddress > b.Address && (surroundingLoop == null || loop.Address > surroundingLoop.Address))
                                surroundingLoop = loop;

                        // Find the meetpoint/After node
                        Node after = null;
                        Node endTruthy = null;

                        if (b.Branches[0] == b.Branches[1])
                            after = b.Branches[0]; // Empty if statement
                        else
                        {
                            List<Node> visited = new List<Node>();
                            Stack<Node> work = new Stack<Node>();
                            work.Push(b.Branches[0]);
                            while (work.Count != 0)
                            {
                                Node curr = work.Pop();
                                visited.Add(curr);

                                foreach (var branch in curr.Branches)
                                    if (!visited.Contains(branch))
                                        work.Push(branch);
                            }

                            List<Node> otherVisited = new List<Node>();
                            bool ignoreFirst = true;
                            work.Push(b);
                            while (work.Count != 0)
                            {
                                Node curr = work.Pop();
                                otherVisited.Add(curr);

                                if (curr.Kind == Node.NodeType.Block)
                                {
                                    Block currBlock = curr as Block;
                                    if (currBlock.ControlFlow == Block.ControlFlowType.Break ||
                                        currBlock.ControlFlow == Block.ControlFlowType.Continue)
                                    {
                                        if (currBlock.Branches.Count == 1)
                                        {
                                            // There's no unreachable block, so there's no "else" here.
                                            endTruthy = currBlock;
                                            after = b.Branches[0];
                                            break;
                                        }
                                    }
                                }

                                foreach (var branch in curr.Branches)
                                {
                                    if (ignoreFirst)
                                    {
                                        // This is b.Branches[0], and we don't want to process it yet
                                        ignoreFirst = false;
                                    }
                                    else if (branch.Address > b.Address && !otherVisited.Contains(branch))
                                    {
                                        if (visited.Contains(branch))
                                        {
                                            if (endTruthy == null || (curr.Unreachable && curr.Address > endTruthy.Address &&
                                                                      curr.Address < b.Branches[0].Address))
                                            {
                                                // `curr` is the end of the true branch
                                                endTruthy = curr;
                                            }
                                            if (after == null || (curr == endTruthy && branch.Address > after.Address) || branch.Address < after.Address)
                                            {
                                                // `branch` is the meetpoint
                                                after = branch;
                                            }
                                        }
                                        work.Push(branch);
                                    }
                                }
                            }
                        }

                        res.Add(new IfStatement(b, after, endTruthy, surroundingLoop));
                    }
                }
            }

            // Get into order we want to process (top to bottom, inner to outer), and return
            return res.OrderBy(s => s.EndAddress).ThenByDescending(s => s.Address).ToList();
        }

        /// Inserts if statement nodes into the graph
        public static void InsertNode(DecompileContext ctx, IfStatement s)
        {
            // Transfer predecessors and branches
            s.Predecessors.AddRange(s.Header.Predecessors);
            s.Branches.Add(s.After);

            // Identify true/else nodes, do some cleanup
            s.Header.Instructions.RemoveAt(s.Header.Instructions.Count - 1); // Remove `bf`
            s.Header.ControlFlow = Block.ControlFlowType.IfCondition;
            if (s.Header.Branches[0] == s.Header.Branches[1])
            {
                // This is an empty if statement
                s.Branches.Add(new Block(-1, -1));
            }
            else
            {
                Node jumpTarget = null;

                // First deal with continues being wonky
                Block endTruthyBlock = null;
                if (s.EndTruthy.Kind == Node.NodeType.Block)
                {
                    endTruthyBlock = s.EndTruthy as Block;
                    if (endTruthyBlock.ControlFlow == Block.ControlFlowType.Continue)
                    {
                        // First check if this is actually an impossible goto
                        bool possible = true;
                        if (s.SurroundingLoop.LoopKind == Loop.LoopType.While)
                        {
                            jumpTarget = endTruthyBlock.Branches[0];
                            if (s.After == jumpTarget &&
                                (jumpTarget != s.SurroundingLoop.Tail || jumpTarget.Address < s.SurroundingLoop.EndAddress - 4)) // 4 bytes for the `b` instruction
                            {
                                // This loop can't be a while loop
                                possible = false;
                                s.SurroundingLoop.LoopKind = Loop.LoopType.For;

                                s.SurroundingLoop.Branches.Add(jumpTarget);
                            }
                            else
                                jumpTarget = null;
                        }

                        if (possible && endTruthyBlock.Branches[0].Address > endTruthyBlock.Address)
                        {
                            // This was initially detected as a continue, but now we know it's (most likely) not
                            // TODO? This might not be a possible thing to occur, not sure what purpose this serves at the time of writing
                            endTruthyBlock.Instructions.Insert(endTruthyBlock.Instructions.Count, endTruthyBlock.LastInstr);
                            endTruthyBlock.ControlFlow = Block.ControlFlowType.None;
                        }
                    }
                }

                // Do a check for if a while loop needs to be turned into a for loop because of continue statements
                if (s.SurroundingLoop?.LoopKind == Loop.LoopType.While)
                {
                    Stack<Node> work = new Stack<Node>();
                    // TODO? maybe add a visited list here--but should maybe be unnecessary
                    work.Push(s.Header);
                    while (work.Count != 0)
                    {
                        Node curr = work.Pop();

                        if (curr.Kind == Node.NodeType.Block)
                        {
                            Block currBlock = curr as Block;
                            if (currBlock.ControlFlow == Block.ControlFlowType.Continue)
                            {
                                jumpTarget = currBlock.Branches[0];
                                if (jumpTarget != s.SurroundingLoop && 
                                    !jumpTarget.Unreachable &&
                                    (jumpTarget != s.SurroundingLoop.Tail || jumpTarget.Address < s.SurroundingLoop.EndAddress - 4)) // 4 bytes for the `b` instruction
                                {
                                    // This loop can't be a while loop
                                    s.SurroundingLoop.LoopKind = Loop.LoopType.For;
                                    s.SurroundingLoop.Branches.Add(jumpTarget);
                                }
                                else
                                    jumpTarget = null;
                            }
                        }

                        foreach (var branch in curr.Branches)
                            if (branch.Address > s.Header.Address && branch.Address < s.After.Address)
                                work.Push(branch);
                    }
                }

                // Link up the truthy clause
                Node truthy = s.Header.Branches[1];
                s.Branches.Add(truthy);
                truthy.Predecessors.Clear();
                truthy.Predecessors.Add(s);

                // Check for else clause
                if (endTruthyBlock?.Instructions.Count >= 1)
                {
                    var lastInstr = endTruthyBlock.Instructions[^1];
                    if (lastInstr.Kind == Instruction.Opcode.B)
                    {
                        // This is the end of the else clause
                        endTruthyBlock.Instructions.RemoveAt(endTruthyBlock.Instructions.Count - 1); // Remove `b`

                        Node falsey = s.Header.Branches[0];
                        s.Branches.Add(falsey);
                        falsey.Predecessors.Clear();
                        falsey.Predecessors.Add(s);
                    }
                }

                if (jumpTarget != null)
                {
                    // This is a continuation of the impossible "continue" logic from both places above
                    // Need to rewire predecessors of `jumpTarget` to go nowhere
                    // ... but we'll do that later because otherwise we run into issues with other if statements, apparently
                    ctx.PredecessorsToClear.Add(jumpTarget);
                }
            }
            s.Header.Branches.Clear();

            // Change header predecessors to point to this node instead
            foreach (var node in s.Header.Predecessors)
            {
                for (int i = 0; i < node.Branches.Count; i++)
                {
                    if (node.Branches[i] != node && node.Branches[i] == s.Header)
                        node.Branches[i] = s;
                }
            }

            // Change After predecessors to come from the if statement instead, and remove their internal references
            foreach (var pred in s.After.Predecessors)
            {
                if (pred.Address >= s.Address && pred.EndAddress <= s.EndAddress)
                {
                    for (int i = pred.Branches.Count - 1; i >= 0; i--)
                    {
                        if (pred.Branches[i].Address >= s.EndAddress)
                            pred.Branches[i] = new Block(-1, -1); // Make an empty block, don't just remove (otherwise making the AST later is more annoying)
                    }
                }
            }
            s.After.Predecessors.Insert(0, s);
        }
    }
}
