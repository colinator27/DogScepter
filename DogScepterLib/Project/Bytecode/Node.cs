using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Bytecode
{
    public interface Node
    {
        public enum NodeType
        {
            Base,
            Block,
            ConditionalBranch,
            Loop
        }

        public NodeType Kind { get; set; }
        public List<Node> Predecessors { get; set; }
        public List<Node> Branches { get; set; }
    }

    public class Block : Node
    {
        public Node.NodeType Kind { get; set; } = Node.NodeType.Block;
        public List<Node> Predecessors { get; set; } = new List<Node>();
        public List<Node> Branches { get; set; } = new List<Node>();

        public int StartAddress { get; private set; }
        public int EndAddress { get; private set; }
        public GMCode.Bytecode.Instruction.Opcode LastOpcode { get; set; }
        public List<GMCode.Bytecode.Instruction> Instructions = new List<GMCode.Bytecode.Instruction>();

        public Block(int startAddress, int endAddress)
        {
            StartAddress = startAddress;
            EndAddress = endAddress;
        }

        public static Dictionary<int, Block> GetBlocks(GMCode codeEntry)
        {
            Dictionary<int, Block> res = new Dictionary<int, Block>();

            // Get the addresses of blocks using the disassembler functionality
            List<int> blockAddresses = Disassembler.FindBlockAddresses(codeEntry.BytecodeEntry);

            // Ensure there's at least an exit block
            blockAddresses.Add(codeEntry.Length);
            blockAddresses.Add(codeEntry.Length);

            // Create blocks using the addresses
            for (int i = 0; i < blockAddresses.Count - 1; i++)
            {
                int addr = blockAddresses[i];
                if (!res.ContainsKey(addr))
                    res[addr] = new Block(addr, blockAddresses[i + 1]);
            }

            // Fill blocks with instructions, resolve references between blocks
            foreach (Block b in res.Values)
            {
                int addr = b.StartAddress;
                int ind = codeEntry.BytecodeEntry.Instructions.FindIndex((instr) => instr.Address == addr);
                while (addr < b.EndAddress)
                {
                    var instr = codeEntry.BytecodeEntry.Instructions[ind++];
                    b.Instructions.Add(instr);
                    addr += instr.GetLength() * 4;
                }

                if (b.Instructions.Count != 0)
                {
                    var lastInstr = b.Instructions.Last();
                    b.LastOpcode = lastInstr.Kind;
                    switch (lastInstr.Kind)
                    {
                        case GMCode.Bytecode.Instruction.Opcode.B:
                            {
                                var other = res[(addr - 4) + (lastInstr.JumpOffset * 4)];
                                b.Branches.Add(other);
                                other.Predecessors.Add(b);
                                break;
                            }
                        case GMCode.Bytecode.Instruction.Opcode.Bf:
                        case GMCode.Bytecode.Instruction.Opcode.Bt:
                        case GMCode.Bytecode.Instruction.Opcode.PushEnv:
                            {
                                var other = res[(addr - 4) + (lastInstr.JumpOffset * 4)];
                                b.Branches.Add(other);
                                other.Predecessors.Add(b);

                                other = res[addr];
                                b.Branches.Add(other);
                                other.Predecessors.Add(b);
                                break;
                            }
                        default:
                            {
                                var other = res[addr];
                                b.Branches.Add(other);
                                other.Predecessors.Add(b);
                                break;
                            }
                        // maybe not handle popenv? "with" is *kind of* a loop, but not really
                    }
                }
            }

            return res;
        }
    }
}
