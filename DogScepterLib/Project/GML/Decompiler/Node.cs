using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Decompiler
{
    public interface Node
    {
        public enum NodeType
        {
            Error,

            Block,
            Loop,
            ShortCircuit,
            IfStatement,
            SwitchStatement,
        }

        public NodeType Kind { get; set; }
        public int Address { get; set; }
        public int EndAddress { get; set; }
        public List<Node> Predecessors { get; set; }
        public List<Node> Branches { get; set; }
        public bool Unreachable { get; set; }
    }

    public class BlockList
    {
        public Dictionary<int, Block> AddressToBlock { get; init; }
        public List<Block> List { get; init; }

        public BlockList()
        {
            AddressToBlock = new Dictionary<int, Block>();
            List = new List<Block>();
        }

        public void TryAddBlock(int start, int end)
        {
            var newBlock = new Block(start, end);
            if (AddressToBlock.TryAdd(start, newBlock))
            {
                newBlock.Index = List.Count;
                List.Add(newBlock);
            }
        }
    }

    public class Block : Node
    {
        public Node.NodeType Kind { get; set; } = Node.NodeType.Block;
        public List<Node> Predecessors { get; set; } = new List<Node>();
        public List<Node> Branches { get; set; } = new List<Node>();

        public int Index = -1;
        public int Address { get; set; }
        public int EndAddress { get; set; }
        public GMCode.Bytecode.Instruction LastInstr { get; set; } = null;
        public List<GMCode.Bytecode.Instruction> Instructions = new List<GMCode.Bytecode.Instruction>();
        public ControlFlowType ControlFlow { get; set; } = ControlFlowType.None;
        public bool Unreachable { get; set; } = false;

        public enum ControlFlowType
        {
            None,

            Break,
            Continue,

            RepeatExpression, // Block that precedes a repeat loop (and thus ends pushing the expression for it)
            WithExpression, // Block that precedes a with loop (ditto),
            SwitchExpression, // Block that precedes a switch statement (ditto)

            LoopCondition, // Block that ends certain loop conditions

            IfCondition, // Block that ends an if statement condition

            SwitchCase, // Block that represents a switch case entry
            SwitchDefault, // Block that represents a switch default entry
        }

        public Block(int startAddress, int endAddress)
        {
            Address = startAddress;
            EndAddress = endAddress;
        }

        public static BlockList GetBlocks(GMCode codeEntry, int startAddr = 0, int endAddr = -1)
        {
            BlockList res = new BlockList();

            // Get the addresses of blocks using the disassembler functionality
            if (endAddr == -1)
                endAddr = codeEntry.Length;
            List<int> blockAddresses = Disassembler.FindBlockAddresses(codeEntry.BytecodeEntry, startAddr, endAddr);

            // Ensure there's at least an exit block
            blockAddresses.Add(endAddr);
            blockAddresses.Add(endAddr);

            // Create blocks using the addresses
            for (int i = 0; i < blockAddresses.Count - 1; i++)
            {
                res.TryAddBlock(blockAddresses[i], blockAddresses[i + 1]);
            }

            // Fill blocks with instructions, resolve references between blocks
            foreach (Block b in res.List)
            {
                int addr = b.Address;
                int ind = codeEntry.BytecodeEntry.Instructions.FindIndex((instr) => instr.Address == addr);
                while (addr < b.EndAddress)
                {
                    var instr = codeEntry.BytecodeEntry.Instructions[ind++];
                    b.Instructions.Add(instr);
                    addr += instr.GetLength() * 4;
                }

                if (b.Instructions.Count != 0)
                {
                    b.LastInstr = b.Instructions.Last();
                    switch (b.LastInstr.Kind)
                    {
                        case GMCode.Bytecode.Instruction.Opcode.B:
                            {
                                var other = res.AddressToBlock[(addr - 4) + (b.LastInstr.JumpOffset * 4)];
                                b.Branches.Add(other);
                                other.Predecessors.Add(b);
                                break;
                            }
                        case GMCode.Bytecode.Instruction.Opcode.Bf:
                        case GMCode.Bytecode.Instruction.Opcode.Bt:
                        case GMCode.Bytecode.Instruction.Opcode.PushEnv:
                            {
                                var other = res.AddressToBlock[(addr - 4) + (b.LastInstr.JumpOffset * 4)];
                                b.Branches.Add(other);
                                other.Predecessors.Add(b);

                                other = res.AddressToBlock[addr];
                                b.Branches.Add(other);
                                other.Predecessors.Add(b);
                                break;
                            }
                        default:
                            {
                                var other = res.AddressToBlock[addr];
                                b.Branches.Add(other);
                                other.Predecessors.Add(b);
                                break;
                            }
                    }
                }
            }
            
            foreach (Block b in res.List)
            {
                if (b.Predecessors.Count == 0)
                {
                    b.Unreachable = true;
                    if (b.Index > 0)
                    {
                        Block prev = res.List[b.Index - 1];
                        prev.Branches.Add(b);
                        b.Predecessors.Add(prev);
                    }
                }
            }

            return res;
        }

        public override string ToString()
        {
            return $"Block {Index}, Address {Address}";
        }
    }

    public class Loop : Node
    {
        public enum LoopType
        {
            Error,

            While,
            DoUntil,
            Repeat,
            With,
            For,
        }

        public Node.NodeType Kind { get; set; } = Node.NodeType.Loop;
        public LoopType LoopKind { get; set; }
        public List<Node> Predecessors { get; set; } = new List<Node>();
        public List<Node> Branches { get; set; } = new List<Node>();
        public bool Unreachable { get; set; }

        public int Address { get; set; }
        public int EndAddress { get; set; }
        public Block Header { get; set; }
        public Block Tail { get; set; }

        public Loop(LoopType type, Block header, Block tail)
        {
            LoopKind = type;
            Address = header.Address;
            EndAddress = tail.EndAddress;
            Header = header;
            Unreachable = header.Unreachable;
            Tail = tail;
        }

        public override string ToString()
        {
            return $"{LoopKind} Loop, Address {Address}";
        }
    }

    public class ShortCircuit : Node
    {
        public enum ShortCircuitType : short
        {
            And = 0,
            Or = 1
        }

        public Node.NodeType Kind { get; set; } = Node.NodeType.ShortCircuit;
        public ShortCircuitType ShortCircuitKind { get; set; }

        public List<Node> Predecessors { get; set; }
        public List<Node> Branches { get; set; } = new List<Node>();
        public bool Unreachable { get; set; }
        public int Address { get; set; }
        public int EndAddress { get; set; }

        public List<Node> Conditions { get; set; } = new List<Node>();
        public Block Tail;

        public ShortCircuit(ShortCircuitType type, Block header, Block tail)
        {
            ShortCircuitKind = type;
            Address = header.Address;
            EndAddress = tail.EndAddress;
            Unreachable = header.Unreachable;
            Tail = tail;
        }

        public override string ToString()
        {
            return $"{ShortCircuitKind} Short-Circuit, Address {Address}";
        }
    }

    public class IfStatement : Node
    {
        public Node.NodeType Kind { get; set; } = Node.NodeType.IfStatement;

        public List<Node> Predecessors { get; set; } = new List<Node>();
        public List<Node> Branches { get; set; } = new List<Node>();
        public bool Unreachable { get; set; } = false;
        public int Address { get; set; }
        public int EndAddress { get; set; }

        public Block Header;
        public Node After;
        public Node EndTruthy;
        public Loop SurroundingLoop;

        public IfStatement(Block header, Node after, Node endTruthy, Loop surroundingLoop)
        {
            Address = header.Address;
            EndAddress = after.Address;
            Header = header;
            After = after;
            EndTruthy = endTruthy;
            SurroundingLoop = surroundingLoop;
        }

        public override string ToString()
        {
            return $"If Statement, Address {Address}";
        }
    }

    public class SwitchStatement : Node
    {
        public Node.NodeType Kind { get; set; } = Node.NodeType.SwitchStatement;

        public List<Node> Predecessors { get; set; } = new List<Node>();
        public List<Node> Branches { get; set; } = new List<Node>();
        public bool Unreachable { get; set; } = false;
        public int Address { get; set; }
        public int EndAddress { get; set; }
        public Block Header;
        public Block Tail;
        public bool Empty;
        public Block DefaultCaseBranch;
        public Block EndCasesBranch;
        public Block ContinueBlock;
        public List<Block> CaseBranches;
        public Loop SurroundingLoop;

        public SwitchStatement(Block header, Block tail, bool empty, List<Block> caseBranches, Block defaultCaseBranch, 
                               Block endCasesBranch, Block continueBlock, Loop surroundingLoop)
        {
            Header = header;
            Address = header.Address;
            Tail = tail;
            EndAddress = tail.Address;
            Empty = empty;
            CaseBranches = caseBranches;
            DefaultCaseBranch = defaultCaseBranch;
            EndCasesBranch = endCasesBranch;
            ContinueBlock = continueBlock;
            SurroundingLoop = surroundingLoop;
        }

        public override string ToString()
        {
            return $"Switch Statement, Address {Address}";
        }
    }
}
