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
        public static List<ShortCircuit> Find(BlockList blocks)
        {
            List<ShortCircuit> res = new List<ShortCircuit>();

            foreach (Block b in blocks.List)
            {
                if (b.Instructions.Count == 1)
                {
                    var instr = b.Instructions[0];
                    if (instr.Kind == Instruction.Opcode.Push && instr.Type1 == Instruction.DataType.Int16)
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
                if ((header as Block).Instructions.Count == 1)
                {
                    // The header is actually another short circuit, need to adjust up
                    header = header.Predecessors[0];
                    s.Address = header.Address;
                }

                // Find all the conditions
                Node curr = header;
                Node prev = header;
                while (curr != s.Tail)
                {
                    if (curr.Kind == Node.NodeType.Block)
                    {
                        Block b = curr as Block;
                        if (b.LastInstr.Kind == Instruction.Opcode.B)
                        {
                            // This is the last condition
                            if (b.Instructions.Count != 1)
                                s.Conditions.Add(b);    // This has the full condition in the block
                            else
                                s.Conditions.Add(prev); // Another short-circuit node
                            if (b.Instructions.LastOrDefault()?.Kind == Instruction.Opcode.B) // It might not be inside a loop!
                                b.Instructions.RemoveAt(b.Instructions.Count - 1);
                            b.ControlFlow = Block.ControlFlowType.None;
                            break;
                        }
                        else // (assuming either Bf or Bt)
                        {
                            // This is a new condition
                            if (b.Instructions.Count != 1)
                                s.Conditions.Add(b);    // This has the full condition in the block
                            else
                                s.Conditions.Add(prev); // Another short-circuit node
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
                        curr = curr.Branches[0];
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

                // Remove all the branches from conditions
                foreach (var cond in s.Conditions)
                    cond.Branches.Clear();

                // Transfer predecessors and branches
                s.Predecessors = header.Predecessors;
                s.Branches.AddRange(s.Tail.Branches);
                s.Tail.Branches.Clear();
            }
        }
    }
}
