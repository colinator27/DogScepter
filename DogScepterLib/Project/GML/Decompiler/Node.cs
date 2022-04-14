using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler;

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
        TryStatement
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

    public BlockList(BlockList existing, int start, int end, List<int> excludes)
    {
        AddressToBlock = existing.AddressToBlock;
        List = new List<Block>((end - start) + 1);
        for (int i = start; i <= end; i++)
        {
            if (excludes.Contains(i))
                continue;
            Block curr = existing.List[i];
            curr.Index = List.Count;
            List.Add(curr);
        }
    }

    public BlockList(BlockList existing, List<int> excludes)
    {
        AddressToBlock = existing.AddressToBlock;
        List = new List<Block>();
        for (int i = 0; i < existing.List.Count; i++)
        {
            if (excludes.Contains(i))
                continue;
            Block curr = existing.List[i];
            curr.Index = List.Count;
            List.Add(curr);
        }
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

    public void FindUnreachables(List<int> exclude = null)
    {
        exclude ??= new List<int>();
        foreach (Block b in List)
        {
            if (b.Predecessors.Count == 0 && !exclude.Contains(b.Index))
            {
                b.Unreachable = true;
                if (b.Index > 0)
                {
                    Block prev = List[b.Index - 1];
                    prev.Branches.Add(b);
                    b.Predecessors.Add(prev);
                }
            }
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
    public Instruction LastInstr { get; set; } = null;
    public List<Instruction> Instructions = new List<Instruction>();
    public ControlFlowType ControlFlow { get; set; } = ControlFlowType.None;
    public bool Unreachable { get; set; } = false;
    public Node BelongingTo { get; set; } // Somewhat hacky field to mark a block as belonging to another node, as some control flow use blocks directly for detection
    public bool AfterFragment { get; set; } = false; // Whether this block was directly after a fragment

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

        TryHook, // Block that hooks a try statement

        PreFragment, // Block that jumps past a fragment
        PreStatic, // Block that jumps past a static region
    }

    public Block(int startAddress, int endAddress)
    {
        Address = startAddress;
        EndAddress = endAddress;
    }

    public static BlockList GetBlocks(GMCode codeEntry, bool slow = true)
    {
        BlockList res = new BlockList();

        // Get the addresses of blocks using the disassembler functionality
        int endAddr = codeEntry.Length;
        List<int> blockAddresses = Disassembler.FindBlockAddresses(codeEntry.BytecodeEntry, slow);

        // Ensure there's at least an exit block
        blockAddresses.Add(endAddr);
        blockAddresses.Add(endAddr);

        // Create blocks using the addresses
        for (int i = 0; i < blockAddresses.Count - 1; i++)
        {
            res.TryAddBlock(blockAddresses[i], blockAddresses[i + 1]);
        }

        // Fill blocks with instructions, resolve references between blocks
        int ind = 0;
        foreach (Block b in res.List)
        {
            int addr = b.Address;
            while (ind < codeEntry.BytecodeEntry.Instructions.Count &&
                    codeEntry.BytecodeEntry.Instructions[ind].Address != addr)
                ind++;
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
                    case Instruction.Opcode.B:
                        {
                            var other = res.AddressToBlock[(addr - 4) + (b.LastInstr.JumpOffset * 4)];
                            b.Branches.Add(other);
                            other.Predecessors.Add(b);
                            break;
                        }
                    case Instruction.Opcode.Bf:
                    case Instruction.Opcode.Bt:
                    case Instruction.Opcode.PushEnv:
                        {
                            var other = res.AddressToBlock[(addr - 4) + (b.LastInstr.JumpOffset * 4)];
                            b.Branches.Add(other);
                            other.Predecessors.Add(b);

                            other = res.AddressToBlock[addr];
                            b.Branches.Add(other);
                            other.Predecessors.Add(b);
                            break;
                        }
                    case Instruction.Opcode.Popz:
                        {
                            if (slow && b.Instructions.Count == 6)
                            {
                                Instruction tryHookCall = b.Instructions[^2];
                                if (tryHookCall.Kind == Instruction.Opcode.Call &&
                                    tryHookCall.Function.Target.Name?.Content == "@@try_hook@@")
                                {
                                    b.ControlFlow = ControlFlowType.TryHook; // Mark this now for performance

                                    int finallyBlock = (int)b.Instructions[^6].Value;
                                    int catchBlock = (int)b.Instructions[^4].Value;

                                    var other = res.AddressToBlock[finallyBlock];
                                    b.Branches.Add(other);
                                    other.Predecessors.Add(b);

                                    if (catchBlock != -1)
                                    {
                                        other = res.AddressToBlock[catchBlock];
                                        b.Branches.Add(other);
                                        other.Predecessors.Add(b);
                                    }

                                    other = res.AddressToBlock[addr];
                                    b.Branches.Add(other);
                                    other.Predecessors.Add(b);

                                    break;
                                }
                            }

                            {
                                var other = res.AddressToBlock[addr];
                                b.Branches.Add(other);
                                other.Predecessors.Add(b);
                            }
                        }
                        break;
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
        Unreachable = header.Unreachable;
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
        Unreachable = header.Unreachable;
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

public class TryStatement : Node
{
    public Node.NodeType Kind { get; set; } = Node.NodeType.TryStatement;

    public List<Node> Predecessors { get; set; } = new List<Node>();
    public List<Node> Branches { get; set; } = new List<Node>();
    public bool Unreachable { get; set; } = false;
    public int Address { get; set; }
    public int EndAddress { get; set; }

    public Block Header;
    public int CatchAddress;

    public TryStatement(Block header, int endAddress, int catchAddress)
    {
        Address = header.Address;
        EndAddress = endAddress;
        Header = header;
        Unreachable = header.Unreachable;
        CatchAddress = catchAddress;
    }

    public override string ToString()
    {
        return $"Try Statement, Address {Address}";
    }
}
