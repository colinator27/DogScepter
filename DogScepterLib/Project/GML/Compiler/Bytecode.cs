using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using static DogScepterLib.Core.Models.GMCode.Bytecode;
using static DogScepterLib.Core.Models.GMCode.Bytecode.Instruction;

namespace DogScepterLib.Project.GML.Compiler
{
    public static class Bytecode
    {
        public static void ProcessReferences(CodeContext ctx)
        {
            // Resolve string references
            foreach (var p in ctx.StringPatches)
            {
                p.Target.Value = ctx.BaseContext.Project.DataHandle.DefineStringIndex(p.Content);
            }

            // Resolve function references
            var func = ctx.BaseContext.Project.DataHandle.GetChunk<GMChunkFUNC>();
            foreach (var p in ctx.FunctionPatches)
            {
                GMFunctionEntry entry;
                if (p.Reference != null)
                    entry = p.Reference.DataEntry;
                else
                    entry = func.FindOrDefine(p.Token.Name, ctx.BaseContext.Project.DataHandle);
                p.Target.Function = new Reference<GMFunctionEntry>(entry);
            }

            // Resolve variable references
            // TODO
        }

        /// <summary>
        /// Emits the necessary bytecode for a statement and its sub-nodes.
        /// </summary>
        public static void CompileStatement(CodeContext ctx, Node stmt)
        {
            switch (stmt.Kind)
            {
                case NodeKind.Block:
                    CompileBlock(ctx, stmt);
                    break;
                case NodeKind.FunctionCall:
                case NodeKind.FunctionCallChain:
                    CompileFunctionCall(ctx, stmt);
                    Emit(ctx, Opcode.Popz, DataType.Variable);
                    break;
            }
        }

        /// <summary>
        /// Emits a basic instruction with no properties.
        /// </summary>
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

        /// <summary>
        /// Emits a basic single-type instruction.
        /// </summary>
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

        /// <summary>
        /// Emits a basic double-type instruction.
        /// </summary>
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

        /// <summary>
        /// Emits a special "dup swap" instruction, with its parameters and type.
        /// </summary>
        private static Instruction EmitDupSwap(CodeContext ctx, DataType type, byte param1, byte param2)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = Opcode.Dup,
                Type1 = type,
                Extra = param1,
                ComparisonKind = (ComparisonType)(param2 | 0x80)
            };
            ctx.Instructions.Add(res);
            ctx.BytecodeLength += 4;
            return res;
        }

        /// <summary>
        /// Emits an instruction that references a variable, with data type.
        /// </summary>
        private static Instruction EmitVariable(CodeContext ctx, Opcode opcode, DataType type, TokenVariable variable)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = opcode,
                Type1 = type
            };

            ctx.VariablePatches.Add(new(res, variable));

            ctx.Instructions.Add(res);
            ctx.BytecodeLength += 8;
            return res;
        }

        /// <summary>
        /// Emits an instruction that references a function, with data type, argument count, and optional reference.
        /// </summary>
        private static Instruction EmitCall(CodeContext ctx, Opcode opcode, DataType type, TokenFunction function, int argCount, FunctionReference reference = null)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = opcode,
                Type1 = type,
                Value = (short)argCount
            };

            ctx.FunctionPatches.Add(new(res, function, reference));

            ctx.Instructions.Add(res);
            ctx.BytecodeLength += 8;
            return res;
        }

        /// <summary>
        /// Emits an instruction that references a string, with data type.
        /// </summary>
        private static Instruction EmitString(CodeContext ctx, Opcode opcode, DataType type, string str)
        {
            Instruction res = new(ctx.BytecodeLength)
            {
                Kind = opcode,
                Type1 = type
            };

            ctx.StringPatches.Add(new(res, str));

            ctx.Instructions.Add(res);
            ctx.BytecodeLength += 8;
            return res;
        }

        /// <summary>
        /// If the top of the type stack isn't the supplied type, emits an instruction to convert to that type.
        /// This removes the type from the top of the type stack in the process.
        /// </summary>
        /// <returns>True if a conversion instruction was emitted, false otherwise</returns>
        private static bool ConvertTo(CodeContext ctx, DataType to)
        {
            DataType from = ctx.TypeStack.Pop();
            if (from != to)
            {
                Emit(ctx, Opcode.Conv, from, to);
                return true;
            }
            return false;
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
                case NodeKind.FunctionCallChain:
                    CompileFunctionCall(ctx, expr);
                    break;
                case NodeKind.Variable:
                    // todo: support more than basic variables
                    EmitVariable(ctx, Opcode.Push, DataType.Variable, expr.Token.Value as TokenVariable);
                    break;
                case NodeKind.Accessor:
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
                    EmitString(ctx, Opcode.Push, DataType.String, constant.ValueString);
                    ctx.TypeStack.Push(DataType.String);
                    break;
            }
        }

        private static void CompileFunctionCall(CodeContext ctx, Node func)
        {
            TokenFunction tokenFunc = func.Token.Value as TokenFunction;

            if (ctx.BaseContext.IsGMS23)
            {
                if (func.Kind == NodeKind.FunctionCall)
                {
                    // Arguments get pushed in reverse order
                    for (int i = func.Children.Count - 1; i >= 0; i--)
                    {
                        CompileExpression(ctx, func.Children[i]);
                        ConvertTo(ctx, DataType.Variable);
                    }

                    // Need to check for actual functions, otherwise default to variables
                    if (ctx.BaseContext.Builtins.Functions.TryGetValue(tokenFunc.Name, out BuiltinFunction builtinFunc))
                    {
                        EmitCall(ctx, Opcode.Call, DataType.Int32, 
                            new TokenFunction(tokenFunc.Name, builtinFunc), func.Children.Count);
                    }
                    else if (ctx.BaseContext.Functions.TryGetValue(tokenFunc.Name, out FunctionReference reference))
                    {
                        EmitCall(ctx, Opcode.Call, DataType.Int32, tokenFunc, func.Children.Count, reference);
                    }
                    else if (ctx.BaseContext.Scripts.Contains(tokenFunc.Name))
                    {
                        EmitCall(ctx, Opcode.Call, DataType.Int32, tokenFunc, func.Children.Count);
                    }
                    else
                    {
                        // This is just a simple variable call
                        EmitCall(ctx, Opcode.Call, DataType.Int32, new TokenFunction("@@This@@", null), 0);
                        EmitVariable(ctx, Opcode.Push, DataType.Variable,
                                new TokenVariable(tokenFunc.Name, null) { InstanceType = -6 /* for some reason */ });
                        Emit(ctx, Opcode.CallV, DataType.Variable).Value = func.Children.Count;
                    }

                    ctx.TypeStack.Push(DataType.Variable);
                }
                else
                {
                    // This is a function chain.
                    Node chainRef = func.Children[0];

                    // Expand all the chain reference nodes to be a more flat structure
                    void handleNode(Node n, Node parent, int parentIndex)
                    {
                        for (int i = n.Children.Count - 1; i >= 0; i--)
                        {
                            Node curr = n.Children[i];
                            if (curr.Kind == NodeKind.ChainReference ||
                                curr.Kind == NodeKind.FunctionCallChain)
                            {
                                handleNode(curr, n, i);
                            }
                        }
                        bool allFunctions = true;
                        foreach (Node child in n.Children)
                        {
                            if (child.Kind != NodeKind.FunctionCall)
                            {
                                allFunctions = false;
                                break;
                            }    
                        }
                        if (allFunctions)
                        {
                            // This chain is now solely populated by function calls, so
                            // push all of them up to the parent's children
                            parent.Children.RemoveAt(parentIndex);
                            parent.Children.InsertRange(parentIndex, n.Children);
                        }
                    }
                    for (int i = chainRef.Children.Count - 1; i >= 0; i--)
                    {
                        Node curr = chainRef.Children[i];
                        if (curr.Kind == NodeKind.ChainReference ||
                            curr.Kind == NodeKind.FunctionCallChain)
                        {
                            handleNode(curr, chainRef, i);
                        }
                    }

                    // Now actually produce the bytecode
                    bool needToSwap = false;
                    bool needToDup = false;
                    bool alreadyPushedArgs = false;
                    for (int i = 0; i < chainRef.Children.Count; i++)
                    {
                        Node curr = chainRef.Children[i];
                        switch (curr.Kind)
                        {
                            case NodeKind.Constant:
                                // This is a constant representing an object index.
                                // In order to convert to an instance ID, this goes through @@GetInstance@@,
                                // or other similar built-in constant functions.
                                if (i != 0)
                                    ctx.Error("Invalid constant in function chain", curr.Token?.Index ?? -1);
                                else
                                {
                                    TokenConstant cst = curr.Token.Value as TokenConstant;
                                    if (cst.Kind == ConstantKind.Number && cst.ValueNumber < 0 && (int)cst.ValueNumber == cst.ValueNumber)
                                    {
                                        string funcName = (int)cst.ValueNumber switch
                                        {
                                            // todo? check -6 for global?
                                            -2 => "@@Other@@",
                                            -5 => "@@Global@@",
                                            _ => "@@This@@"
                                        };
                                        EmitCall(ctx, Opcode.Call, DataType.Int32, new TokenFunction(funcName, null), 0);
                                    }
                                    else
                                    {
                                        CompileConstant(ctx, cst);
                                        ConvertTo(ctx, DataType.Variable);
                                        EmitCall(ctx, Opcode.Call, DataType.Int32, new TokenFunction("@@GetInstance@@", null), 1);
                                    }
                                }
                                needToSwap = true;
                                needToDup = true;
                                break;
                            case NodeKind.FunctionCall:
                                if (i != chainRef.Children.Count - 1 && !alreadyPushedArgs)
                                {
                                    // Not the final part of this chain, so deal with this first

                                    // In this case, arguments get pushed before, apparently
                                    // In reverse order as usual
                                    for (int j = chainRef.Children.Count - 1; j > i; j--)
                                    {
                                        Node n = chainRef.Children[j];
                                        if (n.Kind == NodeKind.FunctionCall)
                                        {
                                            for (int k = n.Children.Count - 1; k >= 0; k--)
                                            {
                                                CompileExpression(ctx, n.Children[k]);
                                                ConvertTo(ctx, DataType.Variable);
                                            }
                                        }
                                    }
                                    alreadyPushedArgs = true;

                                    CompileFunctionCall(ctx, curr);
                                    needToDup = true;
                                    ctx.TypeStack.Pop();
                                    break;
                                }

                                if (!alreadyPushedArgs)
                                {
                                    // Arguments get pushed in reverse order
                                    for (int j = curr.Children.Count - 1; j >= 0; j--)
                                    {
                                        CompileExpression(ctx, curr.Children[j]);
                                        ConvertTo(ctx, DataType.Variable);
                                    }
                                }

                                if (needToSwap && needToDup && !alreadyPushedArgs)
                                {
                                    // Need to move the instance ID to the top of the stack, then duplicate it
                                    EmitDupSwap(ctx, DataType.Variable, (byte)curr.Children.Count, 8);
                                    Emit(ctx, Opcode.Dup, DataType.Variable);

                                    // Push stacktop variable from instance ID on stack
                                    EmitVariable(ctx, Opcode.Push, DataType.Variable,
                                            new TokenVariable((curr.Token.Value as TokenFunction).Name, null) { InstanceType = -9 });
                                }
                                else if (needToDup)
                                {
                                    // We need to only duplicate
                                    Emit(ctx, Opcode.Dup, DataType.Variable);

                                    // Then we push the stacktop variable... using -9 magic
                                    Emit(ctx, Opcode.PushI, DataType.Int16).Value = (short)-9;
                                    EmitVariable(ctx, Opcode.Push, DataType.Variable,
                                            new TokenVariable((curr.Token.Value as TokenFunction).Name, null) 
                                                { InstanceType = -1, VariableType = VariableType.StackTop });
                                }
                                else
                                {
                                    // This is just a simple variable push
                                    EmitVariable(ctx, Opcode.Push, DataType.Variable,
                                            new TokenVariable((curr.Token.Value as TokenFunction).Name, null) { InstanceType = -6 /* for some reason */ });
                                }

                                // Finally, call the function, using the remaining
                                // (possibly duplicated) instance ID, as well as function on the stack.
                                Emit(ctx, Opcode.CallV, DataType.Variable).Value = curr.Children.Count;
                                ctx.TypeStack.Push(DataType.Variable);

                                needToSwap = true;
                                needToDup = true;
                                break;
                            case NodeKind.FunctionCallChain:
                                CompileFunctionCall(ctx, curr);
                                needToSwap = true;
                                needToDup = true;
                                ctx.TypeStack.Pop();
                                break;
                            case NodeKind.Variable:
                                if (i != 0)
                                {
                                    // Need to push from stacktop specifically, using -9 magic
                                    Emit(ctx, Opcode.PushI, DataType.Int16).Value = (short)-9;
                                    EmitVariable(ctx, Opcode.Push, DataType.Variable,
                                            new TokenVariable((curr.Token.Value as TokenVariable).Name, null)
                                                { InstanceType = -1, VariableType = VariableType.StackTop });
                                }
                                else
                                    CompileExpression(ctx, curr);
                                needToSwap = true;
                                needToDup = true;
                                break;
                            case NodeKind.Accessor:
                                // TODO
                                ctx.Error("Arrays/accessors not yet implemented for function call chains", -1);
                                break;
                            case NodeKind.FunctionCallExpr:
                                // TODO
                                ctx.Error("Specific function expression chain not yet implemented", -1);
                                break;
                        }
                    }
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

                EmitCall(ctx, Opcode.Call, DataType.Int32, tokenFunc, func.Children.Count);
                ctx.TypeStack.Push(DataType.Variable);
            }
        }
    }
}
