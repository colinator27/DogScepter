using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    public static class Loops
    {
        /// Finds all loops within the given list of blocks
        public static List<Loop> Find(BlockList blocks)
        {
            Dictionary<int, Block> whileLoops = new Dictionary<int, Block>();
            Dictionary<int, Loop> loops = new Dictionary<int, Loop>();
            List<int> loopEnds = new List<int>();

            foreach (Block b in blocks.List)
            {
                var i = b.LastInstr;
                if (i == null)
                    continue;

                switch (i.Kind)
                {
                    case Instruction.Opcode.B:
                        if (i.JumpOffset < 0)
                        {
                            // This is part of a while/for loop, but scan for the *last* `b` instruction
                            // in order to avoid confusing with a `continue` statement.
                            // We can assume that while/for loops never share the same header block.
                            int target = i.Address + (i.JumpOffset * 4);
                            if (!whileLoops.ContainsKey(target) || b.EndAddress > whileLoops[target].EndAddress)
                                whileLoops[target] = b;
                        }
                        break;
                    case Instruction.Opcode.Bf:
                        if (i.JumpOffset < 0)
                        {
                            // This is part of a do...until loop
                            var newLoop = new Loop(Loop.LoopType.DoUntil, blocks.AddressToBlock[i.Address + (i.JumpOffset * 4)], b);
                            loops[b.EndAddress] = newLoop;
                            loopEnds.Add(b.EndAddress);
                        }
                        break;
                    case Instruction.Opcode.Bt:
                        if (i.JumpOffset < 0)
                        {
                            // This is part of a repeat loop
                            var newLoop = new Loop(Loop.LoopType.Repeat, blocks.AddressToBlock[i.Address + (i.JumpOffset * 4)], b);
                            loops[b.EndAddress] = newLoop;
                            loopEnds.Add(b.EndAddress);
                        }
                        break;
                }

                if (b.Instructions.Count >= 1)
                {
                    var firstInstr = b.Instructions[0];
                    if (firstInstr.Kind == Instruction.Opcode.PopEnv && !firstInstr.PopenvExitMagic)
                    {
                        // This is part of a with loop
                        var newLoop = new Loop(Loop.LoopType.With, blocks.AddressToBlock[firstInstr.Address + (firstInstr.JumpOffset * 4)], b);
                        loops[firstInstr.Address + 4] = newLoop;
                        loopEnds.Add(firstInstr.Address + 4);
                    }
                }
            }

            // Now add the processed while/for loops to the result
            foreach (var whileLoop in whileLoops)
            {
                var newLoop = new Loop(Loop.LoopType.While, blocks.AddressToBlock[whileLoop.Key], whileLoop.Value);
                loops[whileLoop.Value.EndAddress] = newLoop;
                loopEnds.Add(whileLoop.Value.EndAddress);
            }

            // Nothing found, just exit
            if (loopEnds.Count == 0)
                return new List<Loop>();

            // Order the loops in the order we want them in:
            // Nested loops should come before their enveloping loops, so nodes can be reconnected
            loopEnds.Sort();
            List<Loop> res = new List<Loop>(loopEnds.Count);
            foreach (int end in loopEnds)
                res.Add(loops[end]);

            return res;
        }

        /// Inserts loop nodes into the graph, and resolves break/continue
        public static void InsertNodes(DecompileContext ctx)
        {
            foreach (var loop in ctx.LoopNodes)
            {
                // Change header predecessors to point to the loop instead
                foreach (var node in loop.Header.Predecessors)
                {
                    for (int i = 0; i < node.Branches.Count; i++)
                    {
                        if (node.Branches[i] != node && node.Branches[i] == loop.Header)
                        {
                            if (loop.LoopKind == Loop.LoopType.With || loop.LoopKind == Loop.LoopType.Repeat)
                            {
                                if (node.Kind != Node.NodeType.Loop ||
                                    (node.Branches[i].Kind == Node.NodeType.Loop &&
                                     node.Branches[i].Address != loop.Address)) // Special case when a with or repeat statement starts with a loop
                                {
                                    node.Branches[i] = loop; 
                                    break;
                                }
                            }
                            else
                            {
                                node.Branches[i] = loop;
                                break;
                            }
                        }
                    }
                }

                // Initialize predecessors/branches if they're outside of the loop bounds
                foreach (var pred in loop.Header.Predecessors)
                    if ((pred.Address < loop.Address && pred.EndAddress <= loop.Address) || pred.Address >= loop.EndAddress)
                        loop.Predecessors.Add(pred);
                if (loop.LoopKind != Loop.LoopType.Repeat) // Hacky(?) fix to prevent too many loop branches
                {
                    foreach (var branch in loop.Header.Branches)
                        if ((branch.Address < loop.Address && branch.EndAddress <= loop.Address) || branch.Address >= loop.EndAddress)
                            loop.Branches.Add(branch);
                }
                if (loop.LoopKind == Loop.LoopType.With)
                {
                    // Need to make a new block at the end of the with statement so other control flow detection doesn't explode
                    Block withEndBlock = new Block(loop.Tail.Address, loop.Tail.Address);
                    foreach (var pred in loop.Tail.Predecessors)
                    {
                        for (int i = pred.Branches.Count - 1; i >= 0; i--)
                        {
                            if (pred.Branches[i] == loop.Tail)
                            {
                                pred.Branches[i] = withEndBlock;
                                withEndBlock.Predecessors.Add(pred);
                            }
                        }
                    }
                    loop.Tail.Predecessors.Clear();
                    loop.Branches.Add(loop.Tail);
                    loop.Tail.Predecessors.Add(loop);
                }
                else
                {
                    foreach (var branch in loop.Tail.Branches)
                    {
                        if ((branch.Address < loop.Address && branch.EndAddress <= loop.Address) || branch.Address >= loop.EndAddress)
                        {
                            if (!loop.Branches.Contains(branch))
                                loop.Branches.Add(branch);
                        }
                    }
                }

                // Change any nodes jumped outbound to be marked as jumped from this loop
                Stack<Node> work = new Stack<Node>();
                List<Node> visited = new List<Node>();
                work.Push(loop.Header);
                visited.Add(loop.Header);
                while (work.Count != 0)
                {
                    Node curr = work.Pop();
                    for (int i = 0; i < curr.Branches.Count; i++)
                    {
                        var branch = curr.Branches[i];
                        if ((branch.Address < loop.Address && branch.EndAddress <= loop.Address) || branch.Address >= loop.EndAddress)
                        {
                            // This node branches outside of the loop.
                            if (curr.Kind == Node.NodeType.Block && (curr as Block).LastInstr?.Kind == Instruction.Opcode.B && 
                                branch.Address >= loop.EndAddress && curr.EndAddress < loop.EndAddress)
                            {
                                // This is actually a break statement

                                // Remove `b` instruction, mark the block as a "break" block
                                Block currBlock = (curr as Block);
                                currBlock.Instructions.RemoveAt(currBlock.Instructions.Count - 1);
                                currBlock.ControlFlow = Block.ControlFlowType.Break;
                            }
                            else
                            {
                                // Make the branch come from the loop instead
                                if (loop.LoopKind != Loop.LoopType.With && !loop.Branches.Contains(branch))
                                    loop.Branches.Add(branch);

                                var preds = branch.Predecessors;
                                for (int j = 0; j < preds.Count; j++)
                                {
                                    if (preds[j] == curr)
                                    {
                                        preds[j] = loop;
                                        break;
                                    }
                                }
                            }
                        }
                        else if (branch == loop || branch == loop.Tail)
                        {
                            if (curr != loop.Tail &&
                                curr.Kind == Node.NodeType.Block && (curr as Block).LastInstr?.Kind == Instruction.Opcode.B)
                            {
                                if (branch == loop.Tail && branch is Block b &&
                                    b.Instructions.FirstOrDefault()?.Kind == Instruction.Opcode.Popz)
                                {
                                    // This is actually a break statement inside of a switch, inside of a loop
                                    // Don't do anything to this here, because it should be processed later? (TODO?)
                                }
                                else
                                {
                                    // This is a continue statement

                                    // Remove `b` instruction, mark the block as a "continue" block
                                    Block currBlock = (curr as Block);
                                    currBlock.Instructions.RemoveAt(currBlock.Instructions.Count - 1);
                                    currBlock.ControlFlow = Block.ControlFlowType.Continue;
                                }
                            }

                            // (otherwise just ignore the jump)
                        }
                        
                        if (branch.Address >= loop.Address && branch.EndAddress <= loop.EndAddress && !visited.Contains(branch))
                        {
                            work.Push(branch);
                            visited.Add(branch);
                        }
                    }
                }

                // Remove unnecessary predecessors from the header (inside the loop)
                for (int i = loop.Header.Predecessors.Count - 1; i >= 0; i--)
                {
                    var pred = loop.Header.Predecessors[i];
                    if (pred.Address >= loop.Address || pred.EndAddress > loop.Address)
                        loop.Header.Predecessors.RemoveAt(i);
                }
                loop.Header.Predecessors.Add(loop);

                // Set it up so that the last branch signifies REAL header/body (not just the block)
                loop.Branches.Add(loop.Header);

                // Remove unnecessary instructions and deal with references for the loop
                switch (loop.LoopKind)
                {
                    case Loop.LoopType.While:
                        // Remove `b`
                        loop.Tail.Instructions.RemoveAt(loop.Tail.Instructions.Count - 1);

                        // Find the end of the condition:
                        //  - Branches out from loop.Header (or is loop.Header)
                        //  - Ends in a `bf` instruction
                        //  - First branch goes out of loop
                        Block conditionBlock = null;
                        work.Push(loop.Header);
                        while (work.Count != 0)
                        {
                            Node curr = work.Pop();
                            if (curr.Kind == Node.NodeType.Block && (curr as Block).LastInstr?.Kind == Instruction.Opcode.Bf)
                            {
                                if (curr.Branches[0].Address >= loop.EndAddress)
                                {
                                    // We found the condition!
                                    conditionBlock = curr as Block;
                                    break;
                                }
                            }

                            foreach (var branch in curr.Branches)
                                work.Push(branch);
                        }
                        conditionBlock.Branches.RemoveAt(0);
                        conditionBlock.Instructions.RemoveAt(conditionBlock.Instructions.Count - 1); // Remove `bf`
                        conditionBlock.ControlFlow = Block.ControlFlowType.LoopCondition;
                        break;
                    case Loop.LoopType.Repeat:
                        {
                            // Remove initial condition in block before loop, and its branch
                            Block prev = loop.Predecessors[0] as Block;
                            prev.Branches.RemoveAt(0);
                            prev.Instructions.RemoveRange(prev.Instructions.Count - 4, 4);
                            prev.ControlFlow = Block.ControlFlowType.RepeatExpression; // Mark this for later reference

                            // Handle edge cases similar to "with"
                            for (int i = prev.Branches.Count - 1; i >= 0; i--)
                            {
                                if (prev.Branches[i] != loop)
                                {
                                    if (prev.Branches[i].Kind == Node.NodeType.Loop &&
                                        prev.Branches[i].Address == loop.Address)
                                    {
                                        // Edge case when a loop begins a with statement
                                        Node innerLoop = prev.Branches[i];
                                        prev.Branches[i] = loop;
                                        foreach (var pred2 in innerLoop.Predecessors)
                                        {
                                            for (int j = pred2.Branches.Count - 1; j >= 0; j--)
                                                if (pred2.Branches[j] == innerLoop)
                                                    pred2.Branches[j] = loop;
                                        }
                                        innerLoop.Predecessors.Clear();
                                        innerLoop.Predecessors.Add(loop);
                                        loop.Branches[1] = innerLoop;
                                        loop.Header.Predecessors[1] = innerLoop;
                                    }
                                }
                            }
                        }

                        // Remove decrement and branch
                        loop.Tail.Instructions.RemoveRange(loop.Tail.Instructions.Count - 5, 5);

                        // Remove popz in branch
                        (loop.Branches[0] as Block).Instructions.RemoveAt(0);
                        break;
                    case Loop.LoopType.DoUntil:
                        // Remove `bf`
                        loop.Tail.Instructions.RemoveAt(loop.Tail.Instructions.Count - 1);
                        loop.Tail.ControlFlow = Block.ControlFlowType.LoopCondition;
                        break;
                    case Loop.LoopType.With:
                        {
                            // Mark block before loop as a with expression (pushenv and popenv don't need to be removed; they're unique)
                            Node prev = loop.Predecessors[0];
                            if (prev.Kind == Node.NodeType.Block)
                                (prev as Block).ControlFlow = Block.ControlFlowType.WithExpression; // Mark this for later reference

                            // Remove unnecessary branches from the block before
                            for (int i = prev.Branches.Count - 1; i >= 0; i--)
                            {
                                if (prev.Branches[i] != loop)
                                {
                                    if (prev.Branches[i].Kind == Node.NodeType.Loop &&
                                        prev.Branches[i].Address == loop.Address)
                                    {
                                        // Edge case when a loop begins a with statement
                                        Node innerLoop = prev.Branches[i];
                                        prev.Branches[i] = loop;
                                        foreach (var pred2 in innerLoop.Predecessors)
                                        {
                                            for (int j = pred2.Branches.Count - 1; j >= 0; j--)
                                                if (pred2.Branches[j] == innerLoop)
                                                    pred2.Branches[j] = loop;
                                        }
                                        innerLoop.Predecessors.Clear();
                                        innerLoop.Predecessors.Add(loop);
                                        loop.Branches[1] = innerLoop;
                                        loop.Header.Predecessors[1] = innerLoop;
                                    }
                                    else
                                    {
                                        prev.Branches[i].Predecessors.Remove(prev.Branches[i]);
                                        prev.Branches.RemoveAt(i);
                                    }
                                }
                            }

                            // Check for a "break"/popenv drop block at the end, and remove the `b` instruction
                            if (loop.Tail.Instructions.Count == 2 &&
                                loop.Tail.Instructions[1].Kind == Instruction.Opcode.B &&
                                loop.Tail.Index + 1 < ctx.Blocks.List.Count)
                            {
                                Block nextBlock = ctx.Blocks.List[loop.Tail.Index + 1];
                                if (loop.Tail.Branches[0].Address == nextBlock.EndAddress &&
                                    nextBlock.Instructions.Count == 1 && 
                                    nextBlock.Instructions[0].Kind == Instruction.Opcode.PopEnv &&
                                    nextBlock.Instructions[0].PopenvExitMagic)
                                {
                                    loop.Tail.Instructions.RemoveAt(1);
                                }
                            }
                        }
                        break;
                }
            }

            // Clear some additional unnecessary branches
            foreach (var loop in ctx.LoopNodes)
            {
                if (loop.LoopKind != Loop.LoopType.With)
                    loop.Tail.Branches.Clear(); // A with statement tail is the block after
            }
        }
    }
}
