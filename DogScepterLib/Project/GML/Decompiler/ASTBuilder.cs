using DogScepterLib.Core.Models;
using System.Collections.Generic;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    public static class ASTBuilder
    {
        // Returns an AST block node from a decompile context that has structures identified and inserted
        public static ASTBlock FromContext(DecompileContext ctx)
        {
            ASTBlock result = new ASTBlock();
            BuildFromNode(ctx, result, ctx.BaseNode);
            return result;
        }

        // Simulates the stack and builds AST nodes, adding to the "start" node, and using "baseNode" as the data context
        // Also returns the remaining stack, if wanted
        public static Stack<ASTNode> BuildFromNode(DecompileContext ctx, ASTNode start, Node baseNode)
        {
            Stack<ASTNode> statementStack = new Stack<ASTNode>();
            Stack<Node> nodeStack = new Stack<Node>();
            statementStack.Push(start);
            nodeStack.Push(baseNode);

            Stack<ASTNode> stack = new Stack<ASTNode>();

            while (statementStack.Count != 0)
            {
                ASTNode curr = statementStack.Pop();
                Node node = nodeStack.Pop();
                switch (node.Kind)
                {
                    case Node.NodeType.Block:
                        {
                            Block block = node as Block;
                            ExecuteBlock(ctx, block, curr, stack);
                            switch (block.ControlFlow)
                            {
                                case Block.ControlFlowType.Break:
                                    curr.Children.Add(new ASTBreak());
                                    break;
                                case Block.ControlFlowType.Continue:
                                    curr.Children.Add(new ASTContinue());
                                    break;
                            }

                            if (block.Branches.Count == 0)
                                break;
                            statementStack.Push(curr);
                            nodeStack.Push(block.Branches[0]);
                        }
                        break;
                    case Node.NodeType.IfStatement:
                        {
                            IfStatement s = node as IfStatement;
                            ExecuteBlock(ctx, s.Header, curr, stack);

                            var astStatement = new ASTIfStatement(stack.Pop());
                            curr.Children.Add(astStatement);
                            {
                                // Main/true block
                                var block = new ASTBlock();
                                statementStack.Push(block);
                                nodeStack.Push(s.Branches[1]);
                                astStatement.Children.Add(block);
                            }
                            if (s.Branches.Count >= 3)
                            {
                                // Else block
                                var block = new ASTBlock();
                                statementStack.Push(block);
                                nodeStack.Push(s.Branches[2]);
                                astStatement.Children.Add(block);
                            }

                            if (s.Branches.Count == 0)
                                break;
                            statementStack.Push(curr);
                            nodeStack.Push(s.Branches[0]);
                        }
                        break;
                    case Node.NodeType.ShortCircuit:
                        {
                            ShortCircuit s = node as ShortCircuit;

                            var astStatement = new ASTShortCircuit(s.ShortCircuitKind, new List<ASTNode>(s.Conditions.Count));
                            foreach (var cond in s.Conditions)
                                astStatement.Children.Add(BuildFromNode(ctx, astStatement, cond).Pop());
                            stack.Push(astStatement);

                            if (s.Branches.Count == 0)
                                break;
                            statementStack.Push(curr);
                            nodeStack.Push(s.Branches[0]);
                        }
                        break;
                }
            }

            return stack;
        }

        public static void ExecuteBlock(DecompileContext ctx, Block block, ASTNode curr, Stack<ASTNode> stack)
        {
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                Instruction inst = block.Instructions[i];

                switch (inst.Kind)
                {
                    case Instruction.Opcode.Push:
                    case Instruction.Opcode.PushLoc:
                    case Instruction.Opcode.PushGlb:
                    case Instruction.Opcode.PushBltn:
                        switch (inst.Type1)
                        {
                            case Instruction.DataType.Int32:
                                stack.Push(new ASTInt32((int)inst.Value));
                                break;
                            case Instruction.DataType.String:
                                stack.Push(new ASTString((int)inst.Value));
                                break;
                            case Instruction.DataType.Variable:
                                {
                                    ASTVariable variable = new ASTVariable(inst.Variable.Target, inst.Variable.Type);

                                    if (inst.TypeInst == Instruction.InstanceType.StackTop)
                                        variable.Left = stack.Pop();
                                    else if (inst.Variable.Type == Instruction.VariableType.StackTop)
                                        variable.Left = stack.Pop();
                                    // TODO: everything else here
                                    else
                                        variable.Left = new ASTTypeInst((int)inst.TypeInst);

                                    stack.Push(variable);
                                }
                                break;
                            case Instruction.DataType.Double:
                                stack.Push(new ASTDouble((double)inst.Value));
                                break;
                            case Instruction.DataType.Int16:
                                stack.Push(new ASTInt16((short)inst.Value, inst.Kind));
                                break;
                            case Instruction.DataType.Int64:
                                stack.Push(new ASTInt64((long)inst.Value));
                                break;
                            case Instruction.DataType.Boolean:
                                stack.Push(new ASTBoolean((bool)inst.Value));
                                break;
                            case Instruction.DataType.Float:
                                stack.Push(new ASTFloat((float)inst.Value));
                                break;
                        }
                        break;
                    case Instruction.Opcode.PushI:
                        switch (inst.Type1)
                        {
                            case Instruction.DataType.Int16:
                                stack.Push(new ASTInt16((short)inst.Value, inst.Kind));
                                break;
                            case Instruction.DataType.Int32:
                                stack.Push(new ASTInt32((int)inst.Value));
                                break;
                            case Instruction.DataType.Int64:
                                stack.Push(new ASTInt64((long)inst.Value));
                                break;
                        }
                        break;
                    case Instruction.Opcode.Pop:
                        {
                            // TODO pop.e.v

                            ASTVariable variable = new ASTVariable(inst.Variable.Target, inst.Variable.Type);

                            ASTNode value = null;
                            if (inst.Type1 == Instruction.DataType.Int32)
                                value = stack.Pop();
                            if (inst.Variable.Type == Instruction.VariableType.StackTop)
                                variable.Left = stack.Pop();
                            else
                                variable.Left = new ASTTypeInst((int)inst.TypeInst);
                            // TODO everything else
                            if (inst.Type1 == Instruction.DataType.Variable)
                                value = stack.Pop();

                            curr.Children.Add(new ASTAssign(variable, value));
                        }
                        break;
                    case Instruction.Opcode.Add:
                    case Instruction.Opcode.Sub:
                    case Instruction.Opcode.Mul:
                    case Instruction.Opcode.Div:
                    case Instruction.Opcode.And:
                    case Instruction.Opcode.Or:
                    case Instruction.Opcode.Mod:
                    case Instruction.Opcode.Rem:
                    case Instruction.Opcode.Xor:
                    case Instruction.Opcode.Shl:
                    case Instruction.Opcode.Shr:
                    case Instruction.Opcode.Cmp:
                        {
                            ASTNode right = stack.Pop();
                            ASTNode left = stack.Pop();
                            stack.Push(new ASTBinary(inst, left, right));
                        }
                        break;
                    case Instruction.Opcode.Call:
                        {
                            List<ASTNode> args = new List<ASTNode>(inst.ArgumentCount);
                            for (int j = 0; j < inst.ArgumentCount; j++)
                                args.Add(stack.Pop());
                            stack.Push(new ASTFunction(inst.Function.Target, args));
                        }
                        break;
                    case Instruction.Opcode.Neg:
                    case Instruction.Opcode.Not:
                        stack.Push(new ASTUnary(inst, stack.Pop()));
                        break;
                    case Instruction.Opcode.Ret:
                        curr.Children.Add(new ASTReturn(stack.Pop()));
                        break;
                    case Instruction.Opcode.Exit:
                        curr.Children.Add(new ASTExit());
                        break;
                    case Instruction.Opcode.Popz:
                        curr.Children.Add(stack.Pop());
                        break;
                }
            }
        }
    }
}
