using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DogScepterLib.Core.Models.GMCode.Bytecode;
using static DogScepterLib.Core.Models.GMCode.Bytecode.Instruction;

namespace DogScepterLib.Project.GML.Compiler
{
    public static class Bytecode
    {
        public static void CompileStatement(CodeContext ctx, Node stmt)
        {
            switch (stmt.Kind)
            {
                case NodeKind.Block:
                    CompileBlock(ctx, stmt);
                    break; 
            }
        }

        private static Instruction Emit(CodeContext ctx, Opcode opcode)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = opcode
            };
            ctx.Instructions.Add(res);
            ctx.BytecodeLength += 4;
            return res;
        }

        private static Instruction Emit(CodeContext ctx, Opcode opcode, DataType type)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = opcode,
                Type1 = type
            };
            ctx.Instructions.Add(res);
            ctx.BytecodeLength += res.GetLength() * 4;
            return res;
        }

        private static Instruction Emit(CodeContext ctx, Opcode opcode, DataType type, DataType type2)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = opcode,
                Type1 = type,
                Type2 = type2
            };
            ctx.Instructions.Add(res);
            ctx.BytecodeLength += res.GetLength() * 4;
            return res;
        }

        private static Instruction Emit(CodeContext ctx, Opcode opcode, DataType type, TokenVariable variable)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = opcode,
                Type1 = type
            };
            // TODO
            ctx.Instructions.Add(res);
            ctx.BytecodeLength += 8;
            return res;
        }


        private static Instruction Emit(CodeContext ctx, Opcode opcode, DataType type, TokenFunction function)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = opcode,
                Type1 = type
            };
            // TODO
            ctx.Instructions.Add(res);
            ctx.BytecodeLength += 8;
            return res;
        }

        private static Instruction Emit(CodeContext ctx, Opcode opcode, DataType type, string str)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = opcode,
                Type1 = type,
                Value = ctx.BaseContext.Project.DataHandle.DefineStringIndex(str)
            };
            ctx.Instructions.Add(res);
            ctx.BytecodeLength += 8;
            return res;
        }

        private static void ConvertTo(CodeContext ctx, DataType to)
        {
            DataType from = ctx.TypeStack.Pop();
            if (from != to)
                Emit(ctx, Opcode.Conv, from, to);
        }

        private static void CompileBlock(CodeContext ctx, Node block)
        {
            foreach (Node n in block.Children)
                CompileStatement(ctx, n);
        }

        private static void CompileExpression(CodeContext ctx, Node expr)
        {
            switch (expr.Kind)
            {
                case NodeKind.Constant:
                    CompileConstant(ctx, expr.Token.Value as TokenConstant);
                    break;
                case NodeKind.FunctionCall:
                    CompileFunctionCall(ctx, expr, true);
                    break;
                case NodeKind.FunctionCallChain:
                    break;
                case NodeKind.Variable:
                    break;
                case NodeKind.ChainReference:
                    break;
                case NodeKind.Prefix:
                    break;
                case NodeKind.Postfix:
                    break;
                case NodeKind.Conditional:
                    break;
                case NodeKind.NullCoalesce:
                    break;
                case NodeKind.Binary:
                    break;
                case NodeKind.Unary:
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
                    Emit(ctx, Opcode.Push, DataType.String, constant.ValueString);
                    ctx.TypeStack.Push(DataType.String);
                    break;
            }
        }

        private static void CompileFunctionCall(CodeContext ctx, Node func, bool firstInChain)
        {
            TokenFunction tokenFunc = func.Token.Value as TokenFunction;

            if (ctx.BaseContext.IsGMS23)
            {
                if (firstInChain)
                {
                    // Need to check for variable names
                }
            }
            else
            {
                // Arguments get pushed in reverse order
                for (int i = func.Children.Count - 1; i >= 0; i--)
                {
                    CompileExpression(ctx, func.Children[i]);
                    ConvertTo(ctx, DataType.Variable);
                }

                Emit(ctx, Opcode.Call, DataType.Int32, tokenFunc);
                ctx.TypeStack.Push(DataType.Variable);
            }
        }
    }
}
