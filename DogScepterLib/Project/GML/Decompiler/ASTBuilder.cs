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
                                case Block.ControlFlowType.LoopCondition:
                                case Block.ControlFlowType.SwitchExpression:
                                    curr.Children.Insert(0, stack.Pop());
                                    break;
                                case Block.ControlFlowType.SwitchCase:
                                    curr.Children.Add(new ASTSwitchCase(stack.Pop()));
                                    break;
                                case Block.ControlFlowType.SwitchDefault:
                                    curr.Children.Add(new ASTSwitchDefault());
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
                                astStatement.Children.Add(BuildFromNode(ctx, curr, cond).Pop());
                            stack.Push(astStatement);

                            if (s.Branches.Count == 0)
                                break;
                            statementStack.Push(curr);
                            nodeStack.Push(s.Branches[0]);
                        }
                        break;
                    case Node.NodeType.Loop:
                        {
                            Loop s = node as Loop;

                            ASTNode astStatement = null;
                            switch (s.LoopKind)
                            {
                                case Loop.LoopType.While:
                                    astStatement = new ASTWhileLoop();
                                    break;
                                case Loop.LoopType.DoUntil:
                                    astStatement = new ASTDoUntilLoop();
                                    break;
                                case Loop.LoopType.Repeat:
                                    astStatement = new ASTRepeatLoop(stack.Pop());
                                    break;
                                case Loop.LoopType.With:
                                    astStatement = new ASTWithLoop(stack.Pop());
                                    break;
                            }

                            curr.Children.Add(astStatement);
                            statementStack.Push(astStatement);
                            nodeStack.Push(s.Branches[1]);

                            if (s.Branches.Count == 0)
                                break;
                            statementStack.Push(curr);
                            nodeStack.Push(s.Branches[0]);
                        }
                        break;
                    case Node.NodeType.SwitchStatement:
                        {
                            SwitchStatement s = node as SwitchStatement;

                            var astStatement = new ASTSwitchStatement();
                            curr.Children.Add(astStatement);
                            for (int i = s.Branches.Count - 1; i >= 1; i--)
                            {
                                statementStack.Push(astStatement);
                                nodeStack.Push(s.Branches[i]);
                            }

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
                                    else if (inst.Variable.Type == Instruction.VariableType.Array)
                                    {
                                        variable.Children = ProcessArrayIndex(ctx, stack.Pop());
                                        variable.Left = stack.Pop();
                                    }
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
                            if (inst.Variable == null)
                            {
                                // pop.e.v 5/6 - Swap instruction
                                ASTNode e1 = stack.Pop();
                                ASTNode e2 = stack.Pop();
                                for (int j = 0; j < inst.SwapExtra - 4; j++)
                                    stack.Pop();
                                stack.Push(e2);
                                stack.Push(e1);
                                break;
                            }

                            ASTVariable variable = new ASTVariable(inst.Variable.Target, inst.Variable.Type);

                            ASTNode value = null;
                            if (inst.Type1 == Instruction.DataType.Int32)
                                value = stack.Pop();
                            if (inst.Variable.Type == Instruction.VariableType.StackTop)
                                variable.Left = stack.Pop();
                            else if (inst.Variable.Type == Instruction.VariableType.Array)
                            {
                                variable.Children = ProcessArrayIndex(ctx, stack.Pop());
                                variable.Left = stack.Pop();
                            }
                            else
                                variable.Left = new ASTTypeInst((int)inst.TypeInst);
                            if (inst.Type1 == Instruction.DataType.Variable)
                                value = stack.Pop();

                            // TODO handle +=, -=, etc

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
                        if (stack.Count == 0)
                            break; // This occasionally happens in switch statements; this is probably the fastest way to handle it
                        curr.Children.Add(stack.Pop());
                        break;
                    case Instruction.Opcode.Dup:
                        if (inst.ComparisonKind != 0)
                        {
                            // This is a special instruction for moving around an instance on the stack in GMS2.3
                            throw new System.Exception("Unimplemented GMS2.3");
                        }

                        // Get the number of times duplications should occur
                        // dup.i 1 is the same as dup.l 0
                        int count = ((inst.Extra + 1) * (inst.Type1 == Instruction.DataType.Int64 ? 2 : 1));

                        List<ASTNode> expr1 = new List<ASTNode>();
                        List<ASTNode> expr2 = new List<ASTNode>();
                        for (int j = 0; j < count; j++)
                        {
                            var item = stack.Pop();
                            item.Duplicated = true;
                            expr1.Add(item);
                            expr2.Add(item);
                        }
                        for (int j = count - 1; j >= 0; j--)
                            stack.Push(expr1[j]);
                        for (int j = count - 1; j >= 0; j--)
                            stack.Push(expr2[j]);
                        break;
                }
            }
        }

        public static List<ASTNode> ProcessArrayIndex(DecompileContext ctx, ASTNode index)
        {
            // All array indices are normal in 2.3
            if (ctx.Data.VersionInfo.IsNumberAtLeast(2, 3))
                return new() { index };
            
            // Check for 2D array indices
            if (index.Kind == ASTNode.StatementKind.Binary)
            {
                var add = index as ASTBinary;
                if (add.Instruction.Kind == Instruction.Opcode.Add &&
                    add.Children[0].Kind == ASTNode.StatementKind.Binary)
                {
                    var mul = add.Children[1] as ASTBinary;
                    if (mul.Instruction.Kind == Instruction.Opcode.Mul &&
                        mul.Children[1].Kind == ASTNode.StatementKind.Int16 &&
                        (mul.Children[1] as ASTInt16).Value == 32000)
                    {
                        return new() { add.Children[1], mul.Children[0] };
                    }
                }
            }

            return new() { index };
        }
    }
}
