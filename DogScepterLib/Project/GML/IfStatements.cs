using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML
{
    public static class IfStatements
    {
        /// Finds all the if statements within a list of blocks
        public static List<IfStatement> Find(BlockList blocks)
        {
            List<IfStatement> res = new List<IfStatement>();

            foreach (Block b in blocks.List)
            {
                if (b.Instructions.Count >= 1)
                {
                    var instr = b.Instructions[^1];
                    if (instr.Kind == Instruction.Opcode.Bf)
                    {
                        // Any `bf` instruction should be part of an if statement at the normal
                        // point in decompilation (only other control flow type is a switch statement).

                        // Find the meetpoint/After node
                        Node after = null;
                        Node endTruthy = null;
                        if (b.Branches[0] == b.Branches[1])
                            after = b.Branches[0]; // Empty if statement
                        else
                        {
                            List<Node> visited = new List<Node>();
                            Stack<Node> stack = new Stack<Node>();
                            stack.Push(b.Branches[0]);
                            while (stack.Count != 0)
                            {
                                Node curr = stack.Pop();
                                visited.Add(curr);

                                foreach (var branch in curr.Branches)
                                    if (!visited.Contains(branch))
                                        stack.Push(branch);
                            }

                            List<Node> otherVisited = new List<Node>();
                            stack.Push(b.Branches[1]);
                            while (stack.Count != 0 && after == null)
                            {
                                Node curr = stack.Pop();
                                otherVisited.Add(curr);

                                foreach (var branch in curr.Branches)
                                {
                                    if (!otherVisited.Contains(branch))
                                    {
                                        if (visited.Contains(branch))
                                        {
                                            // `branch` is the meetpoint
                                            // TODO? maybe will have to check for minimum address, but this would be faster if it works
                                            endTruthy = curr;
                                            after = branch;
                                            break;
                                        }
                                        stack.Push(branch);
                                    }
                                }
                            }
                        }

                        res.Add(new IfStatement(b, after, endTruthy));
                    }
                }
            }

            // Get into order we want to process (top to bottom, inner to outer), and return
            return res.OrderBy(s => s.EndAddress).ThenByDescending(s => s.Address).ToList();
        }

        /// Inserts if statement nodes into the graph
        public static void InsertNodes(DecompileContext ctx)
        {
            foreach (var s in ctx.IfStatements)
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
                    // Link up the truthy clause
                    Node truthy = s.Header.Branches[1];
                    s.Branches.Add(truthy);
                    truthy.Predecessors.Clear();
                    truthy.Predecessors.Add(s);

                    // Check for else clause
                    Node potential = s.EndTruthy;
                    if (potential?.Kind == Node.NodeType.Block)
                    {
                        Block potentialBlock = potential as Block;
                        if (potentialBlock.Instructions.Count >= 1)
                        {
                            var lastInstr = potentialBlock.Instructions[^1];
                            if (lastInstr.Kind == Instruction.Opcode.B)
                            {
                                // This is the end of the else clause
                                potentialBlock.Instructions.RemoveAt(potentialBlock.Instructions.Count - 1); // Remove `b`

                                Node falsey = s.Header.Branches[0];
                                s.Branches.Add(falsey);
                                falsey.Predecessors.Clear();
                                falsey.Predecessors.Add(s);
                            }
                        }
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
                                                                      // Though... TODO? Maybe it's already annoying in the cases of some loops, in which case this can just remove
                        }
                    }
                }
                s.After.Predecessors.RemoveRange(s.After.Predecessors.Count - 2, 2);
                s.After.Predecessors.Insert(0, s);
            }
        }
    }
}
