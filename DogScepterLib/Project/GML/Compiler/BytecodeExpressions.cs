using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using System;
using static DogScepterLib.Core.Models.GMCode.Bytecode.Instruction;

namespace DogScepterLib.Project.GML.Compiler;

public static partial class Bytecode
{

    private static void CompileExpression(CodeContext ctx, Node expr)
    {
        switch (expr.Kind)
        {
            case NodeKind.Constant:
                CompileConstant(ctx, expr.Token.Value as TokenConstant);
                break;
            case NodeKind.FunctionCall:
            case NodeKind.FunctionCallChain:
                CompileFunctionCall(ctx, expr, false);
                break;
            case NodeKind.Variable:
                EmitVariable(ctx, Opcode.Push, DataType.Variable, expr.Token.Value as TokenVariable);
                ctx.TypeStack.Push(DataType.Variable);
                break;
            case NodeKind.ChainReference:
                CompileChain(ctx, expr);
                break;
            case NodeKind.Prefix:
                // TODO
                break;
            case NodeKind.Postfix:
                // TODO
                break;
            case NodeKind.Conditional:
                {
                    // Condition
                    CompileExpression(ctx, expr.Children[0]);
                    ConvertTo(ctx, DataType.Boolean);
                    var falseJump = new JumpForwardPatch(Emit(ctx, Opcode.Bf));

                    // True expression
                    CompileExpression(ctx, expr.Children[1]);
                    ConvertTo(ctx, DataType.Variable);
                    var endJump = new JumpForwardPatch(Emit(ctx, Opcode.B));

                    // False expression
                    falseJump.Finish(ctx);
                    CompileExpression(ctx, expr.Children[2]);
                    ConvertTo(ctx, DataType.Variable);

                    endJump.Finish(ctx);
                    ctx.TypeStack.Push(DataType.Variable);
                }
                break;
            case NodeKind.NullCoalesce:
                // TODO
                break;
            case NodeKind.Binary:
                CompileBinary(ctx, expr);
                break;
            case NodeKind.Unary:
                CompileUnary(ctx, expr);
                break;
        }
    }

    private static void CompileConstant(CodeContext ctx, TokenConstant constant)
    {
        switch (constant.Kind)
        {
            case ConstantKind.Number:
                if (constant.IsBool && 0 <= constant.ValueNumber && constant.ValueNumber <= 1)
                {
                    // This is a proper boolean type
                    Emit(ctx, Opcode.Push).Value = (short)constant.ValueNumber;
                    ctx.TypeStack.Push(DataType.Boolean);
                }
                else if ((long)constant.ValueNumber == constant.ValueNumber)
                {
                    // This is an integer type
                    long number = (long)constant.ValueNumber;
                    if (number <= int.MaxValue && number >= int.MinValue)
                    {
                        if (number <= short.MaxValue && number >= short.MinValue)
                        {
                            // 16-bit int
                            Emit(ctx, Opcode.PushI, DataType.Int16).Value = (short)number;
                            ctx.TypeStack.Push(DataType.Int32);
                        }
                        else
                        {
                            // 32-bit int
                            Emit(ctx, Opcode.Push, DataType.Int32).Value = (int)number;
                            ctx.TypeStack.Push(DataType.Int32);
                        }
                    }
                    else
                    {
                        // 64-bit int
                        Emit(ctx, Opcode.Push, DataType.Int64).Value = number;
                        ctx.TypeStack.Push(DataType.Int64);
                    }
                }
                else
                {
                    // Floating point (64-bit)
                    Emit(ctx, Opcode.Push, DataType.Double).Value = constant.ValueNumber;
                    ctx.TypeStack.Push(DataType.Double);
                }
                break;
            case ConstantKind.Int64:
                Emit(ctx, Opcode.Push, DataType.Int64).Value = constant.ValueInt64;
                ctx.TypeStack.Push(DataType.Int64);
                break;
            case ConstantKind.String:
                EmitString(ctx, Opcode.Push, DataType.String, constant.ValueString);
                ctx.TypeStack.Push(DataType.String);
                break;
        }
    }

    private static void CompileBinary(CodeContext ctx, Node bin)
    {
        TokenKind kind = bin.Token.Kind;

        // Put left value onto stack
        CompileExpression(ctx, bin.Children[0]);

        if ((kind == TokenKind.And || kind == TokenKind.Or) &&
             ctx.BaseContext.Project.DataHandle.VersionInfo.ShortCircuit)
        {
            // This is a short-circuit evaluation
            var branchJump = new JumpForwardPatch();

            for (int i = 1; i < bin.Children.Count; i++)
            {
                // Convert previous value in chain to boolean
                ConvertTo(ctx, DataType.Boolean);

                // If necessary, branch to the end
                branchJump.Add(Emit(ctx, (kind == TokenKind.And) ? Opcode.Bf : Opcode.Bt));

                // Put next value onto the stack
                CompileExpression(ctx, bin.Children[i]);
            }

            // (convert last value in chain to boolean)
            ConvertTo(ctx, DataType.Boolean);

            // Push final result
            var endJump = new JumpForwardPatch(Emit(ctx, Opcode.B));
            branchJump.Finish(ctx);
            Emit(ctx, Opcode.Push, DataType.Int16).Value = ((kind == TokenKind.And) ? (short)0 : (short)1);
            ctx.TypeStack.Push(DataType.Boolean);
            endJump.Finish(ctx);
            return;
        }

        // Convert left value on stack appropriately
        ConvertForBinaryOp(ctx, bin.Token.Kind);

        for (int i = 1; i < bin.Children.Count; i++)
        {
            // Put next value onto the stack
            CompileExpression(ctx, bin.Children[i]);
            ConvertForBinaryOp(ctx, bin.Token.Kind);

            // Figure out the new type after the operation
            DataType type1 = ctx.TypeStack.Pop();
            DataType type2 = ctx.TypeStack.Pop();

            // Produce actual operation instructions
            bool pushesBoolean = false;
            switch (bin.Token.Kind)
            {
                case TokenKind.Plus:
                    Emit(ctx, Opcode.Add, type1, type2);
                    break;
                case TokenKind.Minus:
                    Emit(ctx, Opcode.Sub, type1, type2);
                    break;
                case TokenKind.Times:
                    Emit(ctx, Opcode.Mul, type1, type2);
                    break;
                case TokenKind.Divide:
                    Emit(ctx, Opcode.Div, type1, type2);
                    break;
                case TokenKind.Div:
                    Emit(ctx, Opcode.Rem, type1, type2);
                    break;
                case TokenKind.Mod:
                    Emit(ctx, Opcode.Mod, type1, type2);
                    break;
                case TokenKind.BitShiftLeft:
                    Emit(ctx, Opcode.Shl, type1, type2);
                    break;
                case TokenKind.BitShiftRight:
                    Emit(ctx, Opcode.Shr, type1, type2);
                    break;
                case TokenKind.And:
                case TokenKind.BitAnd:
                    Emit(ctx, Opcode.And, type1, type2);
                    break;
                case TokenKind.Or:
                case TokenKind.BitOr:
                    Emit(ctx, Opcode.Or, type1, type2);
                    break;
                case TokenKind.Xor:
                case TokenKind.BitXor:
                    Emit(ctx, Opcode.Or, type1, type2);
                    break;
                case TokenKind.Equal:
                    EmitCompare(ctx, ComparisonType.EQ, type1, type2);
                    pushesBoolean = true;
                    break;
                case TokenKind.NotEqual:
                    EmitCompare(ctx, ComparisonType.NEQ, type1, type2);
                    pushesBoolean = true;
                    break;
                case TokenKind.Greater:
                    EmitCompare(ctx, ComparisonType.GT, type1, type2);
                    pushesBoolean = true;
                    break;
                case TokenKind.GreaterEqual:
                    EmitCompare(ctx, ComparisonType.GTE, type1, type2);
                    pushesBoolean = true;
                    break;
                case TokenKind.Lesser:
                    EmitCompare(ctx, ComparisonType.LT, type1, type2);
                    pushesBoolean = true;
                    break;
                case TokenKind.LesserEqual:
                    EmitCompare(ctx, ComparisonType.LTE, type1, type2);
                    pushesBoolean = true;
                    break;
            }

            if (pushesBoolean)
            {
                ctx.TypeStack.Push(DataType.Boolean);
            }
            else
            {
                // Figure out resulting type
                int type1Bias = DataTypeBias(type1);
                int type2Bias = DataTypeBias(type2);
                DataType newType;
                if (type1Bias == type2Bias)
                {
                    // Same bias, so just go for the lower DataType value
                    newType = (DataType)Math.Min((byte)type1, (byte)type2);
                }
                else
                {
                    newType = (type1Bias > type2Bias) ? type1 : type2;
                }
                ctx.TypeStack.Push(newType);
            }
        }
    }

    /// <summary>
    /// Returns the priority of a given data type in an expression
    /// </summary>
    private static int DataTypeBias(DataType type)
    {
        return type switch
        {
            DataType.Float or DataType.Int32 or DataType.Boolean or DataType.String => 0,
            DataType.Double or DataType.Int64 => 1,
            DataType.Variable => 2,
            _ => -1,
        };
    }

    /// <summary>
    /// Converts the type on the top of the stack to match the operators being used
    /// </summary>
    private static void ConvertForBinaryOp(CodeContext ctx, TokenKind kind)
    {
        DataType type = ctx.TypeStack.Peek();
        switch (kind)
        {
            case TokenKind.Divide:
                if (type != DataType.Double && type != DataType.Variable)
                {
                    ctx.TypeStack.Pop();
                    Emit(ctx, Opcode.Conv, type, DataType.Double);
                    ctx.TypeStack.Push(DataType.Double);
                }
                break;
            case TokenKind.Plus:
            case TokenKind.Minus:
            case TokenKind.Times:
            case TokenKind.Div:
            case TokenKind.Mod:
                if (type == DataType.Boolean)
                {
                    ctx.TypeStack.Pop();
                    Emit(ctx, Opcode.Conv, type, DataType.Int32);
                    ctx.TypeStack.Push(DataType.Double);
                }
                break;
            case TokenKind.BitAnd:
            case TokenKind.BitOr:
            case TokenKind.BitXor:
                if (type != DataType.Int32)
                {
                    ctx.TypeStack.Pop();
                    if (type != DataType.Variable &&
                        type != DataType.Double &&
                        type != DataType.Int64)
                    {
                        Emit(ctx, Opcode.Conv, type, DataType.Int32);
                        ctx.TypeStack.Push(DataType.Int32);
                    }
                    else
                    {
                        if (type != DataType.Int64)
                            Emit(ctx, Opcode.Conv, type, DataType.Int64);
                        ctx.TypeStack.Push(DataType.Int64);
                    }
                }
                break;
            case TokenKind.BitShiftLeft:
            case TokenKind.BitShiftRight:
                if (type != DataType.Int64)
                {
                    ctx.TypeStack.Pop();
                    Emit(ctx, Opcode.Conv, type, DataType.Int64);
                    ctx.TypeStack.Push(DataType.Int64);
                }
                break;
            case TokenKind.And:
            case TokenKind.Or:
            case TokenKind.Xor:
                if (type != DataType.Boolean)
                {
                    ctx.TypeStack.Pop();
                    Emit(ctx, Opcode.Conv, type, DataType.Boolean);
                    ctx.TypeStack.Push(DataType.Boolean);
                }
                break;
        }
    }

    private static void CompileUnary(CodeContext ctx, Node unary)
    {
        CompileExpression(ctx, unary.Children[0]);
        DataType type = ctx.TypeStack.Peek();

        switch (unary.Token.Kind)
        {
            case TokenKind.Not:
                if (type != DataType.Boolean)
                {
                    if (type == DataType.String)
                        ctx.Error("Cannot use '!' on string", unary.Token.Index);
                    ctx.TypeStack.Pop();
                    Emit(ctx, Opcode.Conv, type, DataType.Boolean);
                    ctx.TypeStack.Push(DataType.Boolean);
                }
                Emit(ctx, Opcode.Not, DataType.Boolean);
                break;
            case TokenKind.Minus:
                if (type == DataType.String)
                    ctx.Error("Cannot use '-' on string", unary.Token.Index);
                else if (type == DataType.Boolean)
                {
                    ctx.TypeStack.Pop();
                    Emit(ctx, Opcode.Conv, DataType.Boolean, DataType.Int32);
                    ctx.TypeStack.Push(DataType.Int32);
                }
                Emit(ctx, Opcode.Neg, type);
                break;
            case TokenKind.BitNegate:
                if (type == DataType.String)
                    ctx.Error("Cannot use '~' on string", unary.Token.Index);
                else if (type == DataType.Double ||
                         type == DataType.Float ||
                         type == DataType.Variable)
                {
                    ctx.TypeStack.Pop();
                    Emit(ctx, Opcode.Conv, type, DataType.Int32);
                    ctx.TypeStack.Push(DataType.Int32);
                }
                Emit(ctx, Opcode.Not, type);
                break;
        }
    }

    private static void CompileChain(CodeContext ctx, Node chain)
    {
        // Compile left side first
        CompileExpression(ctx, chain.Children[0]);

        // Compile right side separately
        switch (chain.Children[1].Kind)
        {
            case NodeKind.Variable:
                ConvertToInstance(ctx);

                var variable = chain.Children[1].Token.Value as TokenVariable;
                variable.VariableType = VariableType.StackTop;
                EmitVariable(ctx, Opcode.Push, DataType.Variable, variable);

                ctx.TypeStack.Push(DataType.Variable);
                break;
            // todo, functions and arrays
            default:
                ctx.Error("Unsupported", chain.Children[1].Token);
                break;
        }
    }
}