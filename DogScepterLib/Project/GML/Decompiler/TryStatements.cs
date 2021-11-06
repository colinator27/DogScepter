using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    public class TryStatements
    {
        /// Finds all the try statements within a list of blocks, and processes 
        public static List<TryStatement> FindAndClean(DecompileContext ctx)
        {
            List<TryStatement> res = new List<TryStatement>();

            foreach (Block b in ctx.Blocks.List)
            {
                if (b.ControlFlow == Block.ControlFlowType.TryHook)
                {
                    int finallyAddress = b.Branches[0].Address;
                    int catchAddress = b.Branches.Count == 3 ? b.Branches[1].Address : -1;
                    res.Add(new TryStatement(b, finallyAddress, catchAddress));
                }
                else if (b.Instructions.Count >= 3 && b.Instructions[^1].Kind == Instruction.Opcode.B &&
                         b.Instructions[^2].Kind == Instruction.Opcode.Popz)
                {
                    Instruction call = b.Instructions[^3];
                    if (call.Kind == Instruction.Opcode.Call)
                    {
                        string name = call.Function.Target?.Name.Content;
                        if (name == "@@finish_catch@@" || name == "@@finish_finally@@")
                        {
                            // Remove branch
                            b.Instructions.RemoveAt(b.Instructions.Count - 1);
                            b.Branches.Clear();
                        }
                    }
                }
            }

            return res;
        }

        /// Inserts a try statement node into the graph
        public static void InsertNode(DecompileContext ctx, TryStatement s)
        {
            // Transfer predecessors and branches
            s.Predecessors.AddRange(s.Header.Predecessors);
            Node tail = s.Header.Branches[0];
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

            // Remove branches to tail, then change tail predecessor to be this statement
            foreach (var pred in tail.Predecessors)
            {
                for (int i = pred.Branches.Count - 1; i >= 0; i--)
                {
                    if (pred.Branches[i] == tail)
                    {
                        pred.Branches[i] = new Block(-1, -1);
                        if (pred.Kind == Node.NodeType.Block)
                        {
                            Block predBlock = pred as Block;
                            if (predBlock.Instructions.Count >= 1 &&
                                predBlock.Instructions[^1].Kind == Instruction.Opcode.B)
                            {
                                // Remove `b` instruction, which can confuse "continue" logic
                                predBlock.Instructions.RemoveAt(predBlock.Instructions.Count - 1);
                                predBlock.LastInstr = null;
                                predBlock.ControlFlow = Block.ControlFlowType.None;
                            }
                        }
                    }
                }
            }
            tail.Predecessors.Clear();
            tail.Predecessors.Add(s);

            if (s.CatchAddress != -1)
            {
                // Add branch to the try block, then the catch block
                s.Branches.Add(s.Header.Branches[2]);
                s.Branches.Add(s.Header.Branches[1]);

                // Check for the end of the catch, and remove any possible "continue" detection that already occurred
                Node after = tail.Branches[0];
                foreach (var pred in after.Predecessors)
                {
                    if (pred.Kind == Node.NodeType.Block)
                    {
                        Block predBlock = pred as Block;
                        if (predBlock.Instructions.Count >= 2 && 
                            predBlock.ControlFlow == Block.ControlFlowType.Continue &&
                            predBlock.Instructions[^1].Kind == Instruction.Opcode.Popz &&
                            predBlock.Instructions[^2].Kind == Instruction.Opcode.Call &&
                            predBlock.Instructions[^2].Function.Target.Name.Content == "@@finish_catch@@")
                        {
                            predBlock.LastInstr = null;
                            predBlock.ControlFlow = Block.ControlFlowType.None;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Add branch to the try block
                s.Branches.Add(s.Header.Branches[1]);
            }
        }
    }
}
