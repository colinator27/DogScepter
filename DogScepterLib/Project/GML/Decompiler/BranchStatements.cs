using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    public class BranchStatements
    {
        public static void InsertNodes(DecompileContext ctx)
        {
            ctx.PredecessorsToClear = new List<Node>();

            // Process basic linear branch statements in nested order
            List<Node> toProcess = new List<Node>(ctx.SwitchStatementNodes.Count + ctx.IfStatementNodes.Count);
            toProcess.AddRange(ctx.SwitchStatementNodes);
            toProcess.AddRange(ctx.IfStatementNodes);
            toProcess = toProcess.OrderBy(s => s.EndAddress).ThenByDescending(s => s.Address).ToList();
            foreach (var node in toProcess)
            {
                if (node.Kind == Node.NodeType.IfStatement)
                    IfStatements.InsertNode(ctx, node as IfStatement);
                else
                    SwitchStatements.InsertNode(ctx, node as SwitchStatement);
            }

            // Clear predecessors after the fact
            foreach (var node in ctx.PredecessorsToClear)
            {
                foreach (var pred in node.Predecessors)
                {
                    for (int i = pred.Branches.Count - 1; i >= 0; i--)
                    {
                        if (pred.Branches[i] == node)
                            pred.Branches[i] = new Block(-1, -1); // Don't actually remove: causes problems writing AST
                    }
                }
            }
        }

        // Processes "isstaticok" jumps (by removing them), and marks the block
        public static void ProcessStatic(DecompileContext ctx)
        {
            foreach (Block b in ctx.Blocks.List)
            {
                if (b.Instructions.Count >= 2)
                {
                    if (b.Instructions[^1].Kind == Instruction.Opcode.Bt)
                    {
                        var instr = b.Instructions[^2];
                        if (instr.Kind == Instruction.Opcode.Break &&
                            (ushort)instr.Value == 65530 /* isstaticok */)
                        {
                            // Remove these instructions and the true branch
                            b.Instructions.RemoveRange(b.Instructions.Count - 2, 2);
                            b.Branches.RemoveAt(0);
                            b.ControlFlow = Block.ControlFlowType.PreStatic;
                        }
                    }
                }
            }
        }
    }
}
