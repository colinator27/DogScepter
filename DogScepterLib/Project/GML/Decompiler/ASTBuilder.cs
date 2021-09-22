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
            Stack<(ASTNode, Node, ASTNode)> statementStack = new Stack<(ASTNode, Node, ASTNode)>();
            statementStack.Push((start, baseNode, null));

            Stack<ASTNode> stack = new Stack<ASTNode>();

            while (statementStack.Count != 0)
            {
                var tuple = statementStack.Pop();
                ASTNode curr = tuple.Item1;
                Node node = tuple.Item2;
                ASTNode loop = tuple.Item3;
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

                                    // Remove all non-unreachable branches
                                    for (int i = block.Branches.Count - 1; i >= 0; i--)
                                        if (!block.Branches[i].Unreachable)
                                            block.Branches.RemoveAt(i);
                                    break;
                                case Block.ControlFlowType.Continue:
                                    curr.Children.Add(new ASTContinue());
                                    if (loop.Kind == ASTNode.StatementKind.WhileLoop)
                                        (loop as ASTWhileLoop).ContinueUsed = true;

                                    // Remove all non-unreachable branches
                                    for (int i = block.Branches.Count - 1; i >= 0; i--)
                                        if (!block.Branches[i].Unreachable)
                                            block.Branches.RemoveAt(i);
                                    break;
                                case Block.ControlFlowType.LoopCondition:
                                    loop.Children.Add(stack.Pop());
                                    break;
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
                            statementStack.Push((curr, block.Branches[0], loop));
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
                                statementStack.Push((block, s.Branches[1], loop));
                                astStatement.Children.Add(block);
                            }
                            if (s.Branches.Count >= 3)
                            {
                                // Else block
                                var block = new ASTBlock();
                                statementStack.Push((block, s.Branches[2], loop));
                                astStatement.Children.Add(block);
                            }

                            if (s.Branches.Count == 0)
                                break;
                            statementStack.Push((curr, s.Branches[0], loop));
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
                            statementStack.Push((curr, s.Branches[0], loop));
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
                                case Loop.LoopType.For:
                                    astStatement = new ASTForLoop();

                                    ASTBlock subBlock2 = new ASTBlock();
                                    astStatement.Children.Add(subBlock2);
                                    statementStack.Push((subBlock2, s.Branches[2], loop));
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

                            ASTBlock subBlock = new ASTBlock();
                            astStatement.Children.Add(subBlock);
                            curr.Children.Add(astStatement);

                            loop = astStatement;
                            statementStack.Push((subBlock, s.Branches[1], loop));
                            statementStack.Push((curr, s.Branches[0], loop));
                        }
                        break;
                    case Node.NodeType.SwitchStatement:
                        {
                            SwitchStatement s = node as SwitchStatement;

                            var astStatement = new ASTSwitchStatement();
                            curr.Children.Add(astStatement);
                            for (int i = s.Branches.Count - 1; i >= 1; i--)
                            {
                                statementStack.Push((astStatement, s.Branches[i], loop));
                            }

                            if (s.Branches.Count == 0)
                                break;
                            statementStack.Push((curr, s.Branches[0], loop));
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
                                if ((short)inst.Value == 1)
                                {
                                    if (i >= 2 && i + 1 < block.Instructions.Count)
                                    {
                                        // Check for postfix
                                        Instruction prev1 = block.Instructions[i - 1];
                                        Instruction prev2 = block.Instructions[i - 2];
                                        Instruction next = block.Instructions[i + 1];
                                        if (
                                            // Check for `dup.v`
                                            (prev1.Kind == Instruction.Opcode.Dup && prev1.Type1 == Instruction.DataType.Variable) ||

                                            // Check for `dup.v`, then `pop.e.v` (TODO: Only works before 2.3)
                                            (prev2.Kind == Instruction.Opcode.Dup && prev2.Type1 == Instruction.DataType.Variable &&
                                             prev1.Kind == Instruction.Opcode.Pop && prev1.Type1 == Instruction.DataType.Int16 && prev1.Type1 == Instruction.DataType.Variable))
                                        {
                                            if (next.Kind == Instruction.Opcode.Add || next.Kind == Instruction.Opcode.Sub)
                                            {
                                                // This is a postfix ++/--
                                                // Remove duplicate from stack
                                                stack.Pop();

                                                // Make the statement
                                                stack.Push(new ASTAssign(next, stack.Pop(), false));

                                                // Continue until the end of this operation
                                                while (i < block.Instructions.Count)
                                                {
                                                    if (block.Instructions[i].Kind == Instruction.Opcode.Pop || 
                                                        (block.Instructions[i].Type1 == Instruction.DataType.Int16 && block.Instructions[i].Type2 == Instruction.DataType.Variable))
                                                        i++;
                                                    else
                                                        break;
                                                }

                                                break;
                                            }
                                        }
                                    }
                                    else if (i + 2 < block.Instructions.Count)
                                    {
                                        Instruction next1 = block.Instructions[i + 1];
                                        Instruction next2 = block.Instructions[i + 1];

                                        // Check for add/sub, then `dup.v`
                                        if ((next1.Kind == Instruction.Opcode.Add || next1.Kind == Instruction.Opcode.Sub) &&
                                            (next2.Kind == Instruction.Opcode.Dup && next2.Type1 == Instruction.DataType.Variable))
                                        {
                                            // This is a prefix ++/--
                                            stack.Push(new ASTAssign(next1, stack.Pop(), true));

                                            // Continue until the end of this operation
                                            while (i < block.Instructions.Count && block.Instructions[i].Kind != Instruction.Opcode.Pop)
                                                i++;

                                            // If the end is a pop.e.v, then deal with it properly
                                            // TODO: deal with this in 2.3
                                            if (block.Instructions[i].Type1 == Instruction.DataType.Int16 && block.Instructions[i].Type2 == Instruction.DataType.Variable)
                                            {
                                                ASTNode e = stack.Pop();
                                                stack.Pop();
                                                stack.Push(e);
                                                i++;
                                            }

                                            break;
                                        }
                                    }
                                }
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

                            // Check for compound operators
                            if (variable.Left.Duplicated &&
                                (inst.Variable.Type == Instruction.VariableType.StackTop || inst.Variable.Type == Instruction.VariableType.Array))
                            {
                                if (value.Kind == ASTNode.StatementKind.Binary && value.Children[0].Kind == ASTNode.StatementKind.Variable)
                                {
                                    ASTBinary binary = value as ASTBinary;
                                    curr.Children.Add(new ASTAssign(variable, binary.Children[1], binary.Instruction));
                                    break;
                                }
                            }

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
                    case Instruction.Opcode.Conv:
                        if (inst.Type1 == Instruction.DataType.Int32 && inst.Type2 == Instruction.DataType.Boolean && stack.Peek().Kind == ASTNode.StatementKind.Int16)
                        {
                            // Check if a 0 or 1 should be converted to a boolean for readability, such as in while (true)
                            ASTInt16 val = stack.Peek() as ASTInt16;
                            if (val.Value == 0)
                            {
                                stack.Pop();
                                stack.Push(new ASTBoolean(false));
                            }
                            else if (val.Value == 1)
                            {
                                stack.Pop();
                                stack.Push(new ASTBoolean(true));
                            }
                        }
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
                    var mul = add.Children[0] as ASTBinary;
                    if (mul.Instruction.Kind == Instruction.Opcode.Mul &&
                        mul.Children[1].Kind == ASTNode.StatementKind.Int32 &&
                        (mul.Children[1] as ASTInt32).Value == 32000)
                    {
                        return new() { mul.Children[0], add.Children[1] };
                    }
                }
            }

            return new() { index };
        }
    }
}
