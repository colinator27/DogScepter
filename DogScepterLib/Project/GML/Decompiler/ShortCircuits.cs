using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    public static class ShortCircuits
    {
        /// Finds all the short-circuit operations within a list of nodes
        public static List<ShortCircuit> Find(BlockList blocks, bool oldBytecode)
        {
            List<ShortCircuit> res = new List<ShortCircuit>();

            foreach (Block b in blocks.List)
            {
                if (b.Instructions.Count == 1)
                {
                    var instr = b.Instructions[0];
                    if (instr.Kind == (oldBytecode ? Instruction.Opcode.PushI : Instruction.Opcode.Push) && instr.Type1 == Instruction.DataType.Int16)
                    {
                        // This is a new short circuit.
                        // This block's first predecessor is the first condition, and the instruction Value determines whether it's && or ||.
                        res.Add(new ShortCircuit((ShortCircuit.ShortCircuitType)(short)instr.Value, (Block)b.Predecessors[0], b));
                    }
                }
            }

            return res;
        }

        /// Inserts short-circuit nodes into the graph, and compile list of conditions
        public static void InsertNodes(DecompileContext ctx)
        {
            foreach (var s in ctx.ShortCircuitNodes)
            {
                Node header = ctx.Blocks.AddressToBlock[s.Address];
                (header as Block).BelongingTo = s;
                if ((header as Block).Instructions.Count == 1 ||
                    header.Predecessors[0].Kind == Node.NodeType.ShortCircuit)
                {
                    // The header is actually another short circuit, need to adjust up
                    header = header.Predecessors[0];
                    s.Address = header.Address;
                }

                // Find all the conditions
                Node curr = header;
                Node prev = header;
                bool skip = false;
                while (curr != s.Tail)
                {
                    if (curr.Kind == Node.NodeType.Block)
                    {
                        Block b = curr as Block;
                        if (b.LastInstr.Kind == Instruction.Opcode.B)
                        {
                            // This is the last condition
                            if (!skip)
                            {
                                if (b.Instructions.Count != 1)
                                    s.Conditions.Add(b);    // This has the full condition in the block
                                else
                                    s.Conditions.Add(prev); // Another short-circuit node
                            }
                            if (b.Instructions.LastOrDefault()?.Kind == Instruction.Opcode.B) // It might not be inside a loop!
                                b.Instructions.RemoveAt(b.Instructions.Count - 1);
                            b.ControlFlow = Block.ControlFlowType.None;
                            break;
                        }
                        else // (assuming either Bf or Bt)
                        {
                            // This is a new condition
                            if (!skip)
                            {
                                if (b.Instructions.Count != 1)
                                    s.Conditions.Add(b);    // This has the full condition in the block
                                else
                                    s.Conditions.Add(prev); // Another short-circuit node
                            }
                            else
                                skip = false;
                            b.Instructions.RemoveAt(b.Instructions.Count - 1);

                            // Continue onwards
                            prev = curr;
                            curr = curr.Branches[1];
                        }
                    }
                    else
                    {
                        // Continue onwards
                        prev = curr;
                        s.Conditions.Add(curr);
                        curr = curr.Branches[0];
                        skip = true;
                    }
                }

                // Change header predecessors to point to this node instead
                foreach (var node in header.Predecessors)
                {
                    for (int i = 0; i < node.Branches.Count; i++)
                    {
                        if (node.Branches[i] != node && node.Branches[i] == header)
                        {
                            node.Branches[i] = s;
                            break;
                        }
                    }
                }

                // Change tail branches to come from this node instead
                foreach (var node in s.Tail.Branches)
                {
                    node.Predecessors.Clear();
                    node.Predecessors.Add(s);
                }

                // Remove necessary branches from conditions
                foreach (var cond in s.Conditions)
                {
                    for (int i = cond.Branches.Count - 1; i >= 0; i--)
                    {
                        if (cond.Kind == Node.NodeType.ShortCircuit && cond.Branches[i].Address >= s.EndAddress)
                            cond.Branches.RemoveAt(i);
                        else if (cond.Branches[i].Address >= s.Tail.Address || s.Conditions.Contains(cond.Branches[i]))
                            cond.Branches.RemoveAt(i);
                        else
                        {
                            // Need to cut off the branch eventually
                            Stack<Node> work = new Stack<Node>();
                            work.Push(cond.Branches[i]);
                            while (work.Count != 0)
                            {
                                Node currBranch = work.Pop();
                                for (int j = currBranch.Branches.Count - 1; j >= 0; j--)
                                {
                                    if (currBranch.Branches[j].Address >= cond.EndAddress)
                                        currBranch.Branches.RemoveAt(j);
                                    else if (currBranch.Branches[j].Address > s.Address && currBranch.Branches[j] != s.Tail)
                                        work.Push(currBranch.Branches[j]);
                                }
                            }
                        }
                    }
                }

                // Transfer predecessors and branches
                s.Predecessors = header.Predecessors;
                s.Branches.AddRange(s.Tail.Branches);
                s.Tail.Branches.Clear();
            }
        }
    }
}
