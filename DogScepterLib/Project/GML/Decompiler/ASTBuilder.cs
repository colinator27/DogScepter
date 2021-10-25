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

        public class ASTContext
        {
            public ASTNode Current;
            public Node Node;
            public ASTNode Loop;
            public ASTIfStatement IfStatement;

            public ASTContext(ASTNode current, Node node, ASTNode loop, ASTIfStatement ifStatement)
            {
                Current = current;
                Node = node;
                Loop = loop;
                IfStatement = ifStatement;
            }
        }

        // Simulates the stack and builds AST nodes, adding to the "start" node, and using "baseNode" as the data context
        // Also returns the remaining stack, if wanted
        public static Stack<ASTNode> BuildFromNode(DecompileContext dctx, ASTNode start, Node baseNode, Stack<ASTNode> existingStack = null)
        {
            Stack<ASTContext> statementStack = new Stack<ASTContext>();
            statementStack.Push(new(start, baseNode, null, null));

            Stack<ASTNode> stack = existingStack ?? new Stack<ASTNode>();

            while (statementStack.Count != 0)
            {
                var context = statementStack.Pop();

                // Guards to prevent massive RAM usage and an eventual crash
                if (stack.Count >= 65536)
                    throw new System.Exception("Massive stack, sign of infinite loop");
                if (statementStack.Count >= 65536)
                    throw new System.Exception("Massive statement stack, sign of infinite loop");

                switch (context.Node.Kind)
                {
                    case Node.NodeType.Block:
                        {
                            Block block = context.Node as Block;
                            ExecuteBlock(dctx, block, context.Current, stack);
                            switch (block.ControlFlow)
                            {
                                case Block.ControlFlowType.Break:
                                    context.Current.Children.Add(new ASTBreak());

                                    // Remove all non-unreachable branches
                                    for (int i = block.Branches.Count - 1; i >= 0; i--)
                                        if (!block.Branches[i].Unreachable)
                                            block.Branches.RemoveAt(i);
                                    break;
                                case Block.ControlFlowType.Continue:
                                    context.Current.Children.Add(new ASTContinue());
                                    if (context.Loop.Kind == ASTNode.StatementKind.WhileLoop)
                                        (context.Loop as ASTWhileLoop).ContinueUsed = true;

                                    // Remove all non-unreachable branches
                                    for (int i = block.Branches.Count - 1; i >= 0; i--)
                                        if (!block.Branches[i].Unreachable)
                                            block.Branches.RemoveAt(i);
                                    break;
                                case Block.ControlFlowType.LoopCondition:
                                    context.Loop.Children.Add(stack.Pop());
                                    break;
                                case Block.ControlFlowType.SwitchExpression:
                                    context.Current.Children.Insert(0, stack.Pop());
                                    break;
                                case Block.ControlFlowType.SwitchCase:
                                    context.Current.Children.Add(new ASTSwitchCase(stack.Pop()));
                                    break;
                                case Block.ControlFlowType.SwitchDefault:
                                    context.Current.Children.Add(new ASTSwitchDefault());
                                    break;
                                case Block.ControlFlowType.IfCondition:
                                case Block.ControlFlowType.WithExpression:
                                case Block.ControlFlowType.RepeatExpression:
                                    // Nothing special to do here
                                    break;
                                default:
                                    if (context.IfStatement != null && stack.Count == context.IfStatement.StackCount + 1 &&
                                        context.IfStatement.Children.Count >= 3 && !context.IfStatement.EmptyElse && 
                                        context.IfStatement.Children.Count < 5)
                                    {
                                        var last = block.Instructions.Count == 0 ? null : block.Instructions[^1];
                                        if (last == null || 
                                            (last.Kind != Instruction.Opcode.Exit && last.Kind != Instruction.Opcode.Ret))
                                        {
                                            // This is a ternary; add the expression
                                            context.IfStatement.Children.Add(stack.Pop());
                                            if (context.IfStatement.Children.Count >= 5)
                                            {
                                                var removeFrom = context.IfStatement.Parent.Children;
                                                removeFrom.RemoveAt(removeFrom.Count - 1);
                                                stack.Push(context.IfStatement);
                                            }
                                        }
                                    }
                                    break;
                            }
                            if (block.Branches.Count != 0)
                                statementStack.Push(new(context.Current, block.Branches[0], context.Loop, context.IfStatement));
                        }
                        break;
                    case Node.NodeType.IfStatement:
                        {
                            IfStatement s = context.Node as IfStatement;
                            ExecuteBlock(dctx, s.Header, context.Current, stack);

                            statementStack.Push(new(context.Current, s.Branches[0], context.Loop, context.IfStatement));

                            var astStatement = new ASTIfStatement(stack.Pop());
                            astStatement.StackCount = stack.Count;
                            astStatement.Parent = context.Current;
                            context.Current.Children.Add(astStatement);
                            if (s.Branches.Count >= 3)
                            {
                                // Else block
                                var elseBlock = new ASTBlock();
                                statementStack.Push(new(elseBlock, s.Branches[2], context.Loop, astStatement));

                                // Main/true block
                                var block = new ASTBlock();
                                statementStack.Push(new(block, s.Branches[1], context.Loop, astStatement));

                                astStatement.EmptyElse = s.Branches[0] == s.Branches[2];

                                astStatement.Children.Add(block);
                                astStatement.Children.Add(elseBlock);
                            }
                            else
                            {
                                // Main/true block
                                var block = new ASTBlock();
                                if (s.Branches.Count == 1)
                                    statementStack.Peek().Current = block;
                                else
                                    statementStack.Push(new(block, s.Branches[1], context.Loop, astStatement));
                                astStatement.Children.Add(block);
                            }
                        }
                        break;
                    case Node.NodeType.ShortCircuit:
                        {
                            ShortCircuit s = context.Node as ShortCircuit;

                            if (s.Branches.Count != 0)
                                statementStack.Push(new(context.Current, s.Branches[0], context.Loop, context.IfStatement));

                            var astStatement = new ASTShortCircuit(s.ShortCircuitKind, new List<ASTNode>(s.Conditions.Count));
                            foreach (var cond in s.Conditions)
                            {
                                Stack<ASTNode> returnedStack = BuildFromNode(dctx, context.Current, cond, stack);
                                astStatement.Children.Add(returnedStack.Pop());
                            }
                            stack.Push(astStatement);
                        }
                        break;
                    case Node.NodeType.Loop:
                        {
                            Loop s = context.Node as Loop;

                            ASTNode astStatement = null;
                            switch (s.LoopKind)
                            {
                                case Loop.LoopType.While:
                                    astStatement = new ASTWhileLoop();
                                    break;
                                case Loop.LoopType.For:
                                    astStatement = new ASTForLoop();
                                    statementStack.Push(new(context.Current, s.Branches[0], astStatement, context.IfStatement));

                                    ASTBlock subBlock2 = new ASTBlock();
                                    astStatement.Children.Add(subBlock2);
                                    statementStack.Push(new(subBlock2, s.Branches[2], context.Loop, context.IfStatement));
                                    break;
                                case Loop.LoopType.DoUntil:
                                    astStatement = new ASTDoUntilLoop();
                                    break;
                                case Loop.LoopType.Repeat:
                                    astStatement = new ASTRepeatLoop(stack.Pop());
                                    break;
                                case Loop.LoopType.With:
                                    if (dctx.Data.VersionInfo.IsNumberAtLeast(2, 3))
                                    {
                                        ASTNode n = stack.Pop();

                                        if (n.Kind == ASTNode.StatementKind.Int16 && (n as ASTInt16).Value == -9)
                                        {
                                            // -9 signifies stacktop instance
                                            if (stack.Count != 0 && !stack.Peek().Duplicated)
                                                n = stack.Pop();
                                        }

                                        astStatement = new ASTWithLoop(n);
                                    }
                                    else
                                        astStatement = new ASTWithLoop(stack.Pop());
                                    break;
                            }

                            ASTBlock subBlock = new ASTBlock();
                            astStatement.Children.Add(subBlock);
                            context.Current.Children.Add(astStatement);

                            if (s.LoopKind != Loop.LoopType.For)
                                statementStack.Push(new(context.Current, s.Branches[0], context.Loop, context.IfStatement));
                            if (s.Branches.Count >= 2)
                                statementStack.Push(new(subBlock, s.Branches[1], astStatement, context.IfStatement));
                            else if (s.LoopKind == Loop.LoopType.With)
                            {
                                // This is an empty with statement, so we need to simulate its Header block first
                                ExecuteBlock(dctx, s.Header, context.Current, stack);
                            }
                        }
                        break;
                    case Node.NodeType.SwitchStatement:
                        {
                            SwitchStatement s = context.Node as SwitchStatement;

                            if (s.Branches.Count != 0)
                                statementStack.Push(new(context.Current, s.Branches[0], context.Loop, context.IfStatement));

                            var astStatement = new ASTSwitchStatement();
                            context.Current.Children.Add(astStatement);
                            for (int i = s.Branches.Count - 1; i >= 1; i--)
                                statementStack.Push(new(astStatement, s.Branches[i], context.Loop, context.IfStatement));
                        }
                        break;
                }
            }

            return stack;
        }

        public static void ExecuteBlock(DecompileContext ctx, Block block, ASTNode current, Stack<ASTNode> stack)
        {
            Instruction.Opcode lastOpcodeBinary = Instruction.Opcode.Break;
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                Instruction inst = block.Instructions[i];

                Instruction.Opcode wasLastOpcodeBinary = lastOpcodeBinary;
                lastOpcodeBinary = Instruction.Opcode.Break;
                switch (inst.Kind)
                {
                    case Instruction.Opcode.Push:
                    case Instruction.Opcode.PushLoc:
                    case Instruction.Opcode.PushGlb:
                    case Instruction.Opcode.PushBltn:
                        switch (inst.Type1)
                        {
                            case Instruction.DataType.Int32:
                                if (inst.Value == null)
                                {
                                    if (block.AfterFragment)
                                    {
                                        // This block should contain some kind of reference to the previous fragment
                                        // Let's detect what exactly it is now, so we don't need to later.
                                        ProcessAfterFragment(ctx, block, current, stack, ref i);
                                        break;
                                    }
                                    stack.Push(new ASTFunctionRef(inst.Function.Target.Name.Content));
                                    break;
                                }
                                stack.Push(new ASTInt32((int)inst.Value));
                                break;
                            case Instruction.DataType.String:
                                stack.Push(new ASTString((int)inst.Value));
                                break;
                            case Instruction.DataType.Variable:
                                {
                                    ASTVariable variable = new ASTVariable(inst.Variable.Target, inst.Variable.Type, inst.Kind);

                                    if (inst.TypeInst == Instruction.InstanceType.StackTop)
                                        variable.Left = stack.Pop();
                                    else if (inst.Variable.Type == Instruction.VariableType.StackTop)
                                        variable.Left = stack.Pop();
                                    else if (inst.Variable.Type == Instruction.VariableType.Array)
                                    {
                                        variable.Children = ProcessArrayIndex(ctx, stack.Pop());
                                        variable.Left = stack.Pop();
                                    }
                                    else if (inst.Variable.Type == Instruction.VariableType.MultiPush ||
                                             inst.Variable.Type == Instruction.VariableType.MultiPushPop)
                                    {
                                        variable.Children = new() { stack.Pop() };
                                        variable.Left = stack.Pop();
                                    }
                                    else
                                        variable.Left = new ASTTypeInst((int)inst.TypeInst);

                                    if (variable.Variable.VariableType == Instruction.InstanceType.Local)
                                        ctx.RemainingLocals.Add(variable.Variable.Name?.Content);

                                    // 2.3 stacktop instance
                                    if (variable.Left.Kind == ASTNode.StatementKind.Int16 &&
                                        ctx.Data.VersionInfo.IsNumberAtLeast(2, 3) &&
                                        (variable.Left as ASTInt16).Value == -9)
                                    {
                                        variable.Left = stack.Pop();
                                    }

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
                                                        break;
                                                    i++;
                                                }

                                                break;
                                            }
                                        }
                                    }
                                    if (i + 2 < block.Instructions.Count)
                                    {
                                        Instruction next1 = block.Instructions[i + 1];
                                        Instruction next2 = block.Instructions[i + 2];

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
                                stack.Push(new ASTInt16((short)inst.Value, Instruction.Opcode.Push));
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
                                stack.Push(new ASTInt16((short)inst.Value, Instruction.Opcode.PushI));
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
                                for (int j = 0; j < (short)inst.TypeInst - 4; j++)
                                    stack.Pop();
                                stack.Push(e2);
                                stack.Push(e1);
                                break;
                            }

                            ASTVariable variable = new ASTVariable(inst.Variable.Target, inst.Variable.Type, inst.Kind);

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

                            // 2.3 stacktop instance
                            if (variable.Left.Kind == ASTNode.StatementKind.Int16 &&
                                ctx.Data.VersionInfo.IsNumberAtLeast(2, 3) &&
                                (variable.Left as ASTInt16).Value == -9)
                            {
                                variable.Left = stack.Pop();
                            }

                            if (inst.Type1 == Instruction.DataType.Variable)
                                value = stack.Pop();

                            if (variable.Variable.VariableType == Instruction.InstanceType.Local)
                                ctx.RemainingLocals.Add(variable.Variable.Name?.Content);

                            // Check for compound operators
                            if (variable.Left.Duplicated &&
                                (inst.Variable.Type == Instruction.VariableType.StackTop || inst.Variable.Type == Instruction.VariableType.Array))
                            {
                                if (value.Kind == ASTNode.StatementKind.Binary && value.Children[0].Kind == ASTNode.StatementKind.Variable)
                                {
                                    ASTBinary binary = value as ASTBinary;
                                    current.Children.Add(new ASTAssign(variable, binary.Children[1], binary.Instruction));
                                    break;
                                }
                            }

                            current.Children.Add(new ASTAssign(variable, value));
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
                            lastOpcodeBinary = inst.Kind;

                            ASTNode right = stack.Pop();
                            ASTNode left = stack.Pop();
                            stack.Push(new ASTBinary(inst, left, right));

                            if (wasLastOpcodeBinary != inst.Kind && left.Kind == ASTNode.StatementKind.Binary)
                                (left as ASTBinary).Chained = true;
                        }
                        break;
                    case Instruction.Opcode.Call:
                        {
                            List<ASTNode> args = new List<ASTNode>((short)inst.Value);
                            for (int j = 0; j < (short)inst.Value; j++)
                                args.Add(stack.Pop());
                            stack.Push(new ASTFunction(inst.Function.Target, args));
                        }
                        break;
                    case Instruction.Opcode.CallV:
                        {
                            ASTNode func = stack.Pop();
                            ASTNode instance = stack.Pop();
                            List<ASTNode> args = new List<ASTNode>(inst.Extra + 2);
                            for (int j = 0; j < inst.Extra; j++)
                                args.Add(stack.Pop());
                            stack.Push(new ASTFunctionVar(instance, func, args));
                        }
                        break;
                    case Instruction.Opcode.Neg:
                    case Instruction.Opcode.Not:
                        stack.Push(new ASTUnary(inst, stack.Pop()));
                        break;
                    case Instruction.Opcode.Ret:
                        current.Children.Add(new ASTReturn(stack.Pop()));
                        break;
                    case Instruction.Opcode.Exit:
                        current.Children.Add(new ASTExit());
                        break;
                    case Instruction.Opcode.Popz:
                        if (stack.Count == 0)
                            break; // This occasionally happens in switch statements; this is probably the fastest way to handle it
                        {
                            ASTNode node = stack.Pop();
                            if (!node.Duplicated)
                                current.Children.Add(node);
                        }
                        break;
                    case Instruction.Opcode.Dup:
                        if (inst.ComparisonKind != 0)
                        {
                            // This is a GMS2.3 "swap" instruction

                            // Variable type seems to do literally nothing
                            if (inst.Type1 == Instruction.DataType.Variable)
                                break;

                            int sourceBytes = inst.Extra * 4;
                            Stack<ASTNode> source = new Stack<ASTNode>();
                            while (sourceBytes > 0)
                            {
                                ASTNode e = stack.Pop();
                                source.Push(e);
                                sourceBytes -= ASTNode.GetStackLength(e);
#if DEBUG
                                if (sourceBytes < 0)
                                    throw new System.Exception("Dup swap stack size incorrect #1");
#endif
                            }

                            int moveBytes = (((byte)inst.ComparisonKind & 0x7F) >> 3) * 4;
                            Stack<ASTNode> moved = new Stack<ASTNode>();
                            while (moveBytes > 0)
                            {
                                ASTNode e = stack.Pop();
                                moved.Push(e);
                                moveBytes -= ASTNode.GetStackLength(e);
#if DEBUG
                                if (moveBytes < 0)
                                    throw new System.Exception("Dup swap stack size incorrect #2");
#endif
                            }

                            while (source.Count != 0)
                                stack.Push(source.Pop());
                            while (moved.Count != 0)
                                stack.Push(moved.Pop());
                            break;
                        }

                        // Normal duplication instruction
                        int bytes = (inst.Extra + 1) * Instruction.GetDataTypeStackLength(inst.Type1);
                        List<ASTNode> expr1 = new List<ASTNode>();
                        List<ASTNode> expr2 = new List<ASTNode>();
                        while (bytes > 0)
                        {
                            var item = stack.Pop();
                            item.Duplicated = true;
                            expr1.Add(item);
                            expr2.Add(item);

                            bytes -= ASTNode.GetStackLength(item);

#if DEBUG
                            if (bytes < 0)
                                throw new System.Exception("Dup stack size incorrect");
#endif
                        }
                        for (int j = expr1.Count - 1; j >= 0; j--)
                            stack.Push(expr1[j]);
                        for (int j = expr2.Count - 1; j >= 0; j--)
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
                        stack.Peek().DataType = inst.Type2;
                        break;
                    case Instruction.Opcode.Break:
                        switch ((short)inst.Value)
                        {
                            case -2:
                                // pushaf
                                {
                                    ASTNode ind = stack.Pop();
                                    ASTVariable variable = stack.Pop() as ASTVariable;

                                    ASTVariable newVar = new ASTVariable(variable.Variable, variable.VarType, inst.Kind);
                                    newVar.Left = variable.Left;
                                    newVar.Children = new List<ASTNode>(variable.Children);
                                    newVar.Children.Add(ind);
                                    stack.Push(newVar);
                                }
                                break;
                            case -3:
                                // popaf
                                {
                                    ASTNode ind = stack.Pop();
                                    ASTVariable variable = stack.Pop() as ASTVariable;

                                    ASTVariable newVar = new ASTVariable(variable.Variable, variable.VarType, inst.Kind);
                                    newVar.Left = variable.Left;
                                    newVar.Children = new List<ASTNode>(variable.Children);
                                    newVar.Children.Add(ind);

                                    current.Children.Add(new ASTAssign(newVar, stack.Pop()));
                                }
                                break;
                            case -5: 
                                // setowner
                                // Used for a unique ID for array copy-on-write functionality
                                stack.Pop();
                                break;
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

        public static void ProcessAfterFragment(DecompileContext ctx, Block block, ASTNode current, Stack<ASTNode> stack, ref int i)
        {
            // Track the function reference
            GMFunctionEntry function = block.Instructions[i].Function.Target;

            // Go past conv instruction
#if DEBUG
            i++;
            if (block.Instructions[i].Kind != Instruction.Opcode.Conv)
                throw new System.Exception("Expected conv after fragment");
            i++;
#else
            i += 2;
#endif

            switch (block.Instructions[i].Kind)
            {
                case Instruction.Opcode.PushI:
                    {
                        // This is a normal function, skip past some already-known instructions
#if DEBUG
                        if ((short)block.Instructions[i].Value != -1)
                            throw new System.Exception("Expected -1, got another value");
                        i++;
                        if (block.Instructions[i].Kind != Instruction.Opcode.Conv)
                            throw new System.Exception("Expected conv #2 after fragment");
                        i++;
                        if (block.Instructions[i].Kind != Instruction.Opcode.Call)
                            throw new System.Exception("Expected call after fragment");
                        if (block.Instructions[i].Function.Target.Name.Content != "method")
                            throw new System.Exception("Expected method, got another function");
                        i++;
#else
                        i += 3;
#endif

                        // Now we need to test if this function is anonymous
                        if (i >= block.Instructions.Count || block.Instructions[i].Kind != Instruction.Opcode.Dup)
                        {
                            // This is an anonymous function (it's not duplicated)
                            stack.Push(new ASTFunctionDecl(
                                ctx.SubContexts.Find(subContext => subContext.Fragment.Name == function.Name.Content), null));
                        }
                        else
                        {
                            // This is a function with a given name
#if DEBUG
                            i++;
                            if (block.Instructions[i].Kind != Instruction.Opcode.PushI)
                                throw new System.Exception("Expected pushi after fragment");
                            i++;
                            if (block.Instructions[i].Kind != Instruction.Opcode.Pop)
                                throw new System.Exception("Expected pop after fragment");
#else
                            i += 2;
#endif
                            stack.Push(new ASTFunctionDecl(
                                ctx.SubContexts.Find(subContext => subContext.Fragment.Name == function.Name.Content), 
                                block.Instructions[i].Variable.Target.Name.Content));
                        }
                    }
                    break;
                case Instruction.Opcode.Call:
                    {
                        // This is a struct or constructor
#if DEBUG
                        if (block.Instructions[i].Function.Target.Name.Content != "@@NullObject@@")
                            throw new System.Exception("Expected @@NullObject@@, got another function");
#endif

                        // TODO
                    }
                    break;
                default:
                    throw new System.Exception("Unknown instruction pattern after fragment");
            }
        }
    }
}
