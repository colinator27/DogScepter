using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using System.Collections.Generic;
using static DogScepterLib.Core.Models.GMCode.Bytecode.Instruction;

namespace DogScepterLib.Project.GML.Compiler;

public static partial class Bytecode
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
        var vari = ctx.BaseContext.Project.DataHandle.GetChunk<GMChunkVARI>();
        foreach (var p in ctx.VariablePatches)
        {
            GMVariable variable = vari.FindOrDefine(p.Token.Name,
                                                    (InstanceType)p.Token.InstanceType,
                                                    p.Token.Builtin != null,
                                                    ctx.BaseContext.Project.DataHandle);
            p.Target.Variable = new Reference<GMVariable>(variable, p.Token.VariableType);
            if (p.Token.VariableType == VariableType.Normal)
                p.Target.TypeInst = (InstanceType)p.Token.InstanceType;
            if (p.Token.InstanceType == (int)InstanceType.Local)
            {
                if (!ctx.ReferencedLocalVars.Contains(p.Token.Name))
                    ctx.ReferencedLocalVars.Add(p.Token.Name);
            }
        }
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
                CompileFunctionCall(ctx, stmt, false);
                Emit(ctx, Opcode.Popz, DataType.Variable);
                break;
            case NodeKind.Assign:
                switch (stmt.Token.Kind)
                {
                    case TokenKind.Assign:
                        CompileExpression(ctx, stmt.Children[1]);
                        CompileAssign(ctx, stmt.Children[0]);
                        break;
                    case TokenKind.AssignPlus:
                        CompileCompoundAssign(ctx, stmt, Opcode.Add);
                        break;
                    case TokenKind.AssignMinus:
                        CompileCompoundAssign(ctx, stmt, Opcode.Sub);
                        break;
                    case TokenKind.AssignTimes:
                        CompileCompoundAssign(ctx, stmt, Opcode.Mul);
                        break;
                    case TokenKind.AssignDivide:
                        CompileCompoundAssign(ctx, stmt, Opcode.Div);
                        break;
                    case TokenKind.AssignAnd:
                        CompileCompoundAssign(ctx, stmt, Opcode.And, true);
                        break;
                    case TokenKind.AssignOr:
                        CompileCompoundAssign(ctx, stmt, Opcode.Or, true);
                        break;
                    case TokenKind.AssignXor:
                        CompileCompoundAssign(ctx, stmt, Opcode.Xor, true);
                        break;
                    case TokenKind.AssignMod:
                        CompileCompoundAssign(ctx, stmt, Opcode.Mod);
                        break;
                }
                break;
            case NodeKind.Exit:
                ExitCleanup(ctx);
                Emit(ctx, Opcode.Exit, DataType.Int32);
                break;
            case NodeKind.Break:
                if (ctx.BytecodeContexts.Count == 0)
                {
                    ctx.Error("Break statement has no context", stmt.Token);
                    break;
                }
                ctx.BytecodeContexts.Peek().UseBreakJump().Add(Emit(ctx, Opcode.B));
                break;
            case NodeKind.Continue:
                if (ctx.BytecodeContexts.Count == 0)
                {
                    ctx.Error("Continue statement has no context", stmt.Token);
                    break;
                }
                ctx.BytecodeContexts.Peek().UseContinueJump().Add(Emit(ctx, Opcode.B));
                break;
            case NodeKind.Return:
                if (stmt.Children.Count == 0)
                {
                    // No return value (same as exit)
                    ExitCleanup(ctx);
                    Emit(ctx, Opcode.Exit, DataType.Int32);
                }
                else
                {
                    // Has a return value, so push it to the stack
                    CompileExpression(ctx, stmt.Children[0]);
                    ConvertTo(ctx, DataType.Variable);

                    if (ctx.BytecodeContexts.Count != 0)
                    {
                        // Need to store the return value in a temporary local
                        var tempVar = new TokenVariable("$$$$temp$$$$", null) { InstanceType = (int)InstanceType.Local };
                        EmitVariable(ctx, Opcode.Pop, DataType.Variable, tempVar, DataType.Variable);

                        // Then, clean up outer contexts
                        ExitCleanup(ctx);

                        // Finally, restore the return value to the stack
                        EmitVariable(ctx, Opcode.PushLoc, DataType.Variable, tempVar);
                    }

                    // Actual return instruction
                    Emit(ctx, Opcode.Ret, DataType.Variable);
                }
                break;
            case NodeKind.If:
                {
                    // Condition
                    CompileExpression(ctx, stmt.Children[0]);
                    ConvertTo(ctx, DataType.Boolean);
                    var conditionJump = new JumpForwardPatch(Emit(ctx, Opcode.Bf));

                    // Body
                    CompileStatement(ctx, stmt.Children[1]);

                    if (stmt.Children.Count == 3)
                    {
                        // Else branch
                        var elseJump = new JumpForwardPatch(Emit(ctx, Opcode.B));
                        conditionJump.Finish(ctx);
                        CompileStatement(ctx, stmt.Children[2]);
                        elseJump.Finish(ctx);
                    }
                    else
                    {
                        // Normal ending
                        conditionJump.Finish(ctx);
                    }
                }
                break;
            case NodeKind.Switch:
                {
                    // Expression to be checked
                    CompileExpression(ctx, stmt.Children[0]);
                    DataType exprType = ctx.TypeStack.Pop();

                    // Ensure that this switch statement even has anything in it
                    if (stmt.Children.Count < 2)
                    {
                        ctx.Error("Empty switch statement", stmt.Token.Index);
                        break;
                    }

                    // Ensure that no statements come before cases
                    NodeKind firstKind = stmt.Children[1].Kind;
                    if (firstKind != NodeKind.SwitchCase && firstKind != NodeKind.SwitchDefault)
                    {
                        ctx.Error("Switch statement needs to start with 'case' or 'default' statement", stmt.Children[1].Token?.Index ?? stmt.Token.Index);
                        break;
                    }

                    // Emit branch chains, which will have targets written after
                    var endJump = new JumpForwardPatch();
                    var continueJump = new JumpForwardPatch();
                    JumpForwardPatch defaultJump = null;
                    List<SwitchCase> cases = new();
                    for (int i = 1; i < stmt.Children.Count; i++)
                    {
                        Node curr = stmt.Children[i];
                        switch (curr.Kind)
                        {
                            case NodeKind.SwitchCase:
                                {
                                    // Duplicate original expression, and compare it to this case
                                    Emit(ctx, Opcode.Dup, exprType);
                                    CompileExpression(ctx, curr.Children[0]);
                                    Emit(ctx, Opcode.Cmp, ctx.TypeStack.Pop(), exprType).ComparisonKind = ComparisonType.EQ;

                                    // Branch to target code if equal (and add to list of cases)
                                    cases.Add(new SwitchCase(new JumpForwardPatch(Emit(ctx, Opcode.Bt)), i));
                                }
                                break;
                            case NodeKind.SwitchDefault:
                                {
                                    defaultJump = new JumpForwardPatch();
                                    cases.Add(new SwitchCase(defaultJump, i));
                                }
                                break;
                        }
                    }
                    defaultJump?.Add(Emit(ctx, Opcode.B));
                    endJump.Add(Emit(ctx, Opcode.B));

                    // Emit actual code blocks
                    ctx.BytecodeContexts.Push(new Context(endJump, continueJump, exprType));
                    for (int i = 0; i < cases.Count; i++)
                    {
                        var curr = cases[i];

                        // Figure out statement indices for this case
                        int startIndex = curr.ChildIndex;
                        int nextIndex = (i + 1 < cases.Count) ? 
                                            cases[i + 1].ChildIndex : 
                                            stmt.Children.Count;

                        // Finish jump and compile actual statements
                        curr.Jump.Finish(ctx);
                        for (int j = startIndex; j < nextIndex; j++)
                            CompileStatement(ctx, stmt.Children[j]);
                    }

                    // Check if continue block is necessary
                    Context bctx = ctx.BytecodeContexts.Pop();
                    if (bctx.ContinueUsed)
                    {
                        // A continue statement was used inside the statement, applying to an outer loop
                        // First, check to see if there IS any loop
                        if (ctx.BytecodeContexts.Count == 0)
                        {
                            ctx.Error("Continue used without context in switch statement", stmt.Token.Index);
                            break;
                        }

                        // Allow all other branches to skip past this block
                        endJump.Add(Emit(ctx, Opcode.B));

                        // All necessary continue statements inside will be routed here
                        // Perform stack cleanup and branch
                        continueJump.Finish(ctx);
                        Emit(ctx, Opcode.Popz, exprType);
                        ctx.BytecodeContexts.Peek().UseContinueJump().Add(Emit(ctx, Opcode.B));
                    }

                    // General end and stack cleanup
                    endJump.Finish(ctx);
                    Emit(ctx, Opcode.Popz, exprType);
                }
                break;
            case NodeKind.LocalVarDecl:
                {
                    for (int i = 0; i < stmt.Children.Count; i++)
                    {
                        if (stmt.Children[i].Children.Count == 0)
                            continue;

                        // Compile initial assignment
                        // Remove the expression from it first so it's not an array
                        Node expr = stmt.Children[i].Children[0];
                        stmt.Children[i].Children.Clear();
                        CompileExpression(ctx, expr);
                        CompileAssign(ctx, stmt.Children[i]);
                    }
                }
                break;
            case NodeKind.With:
                {
                    // Instance
                    CompileExpression(ctx, stmt.Children[0]);
                    DataType type = ctx.TypeStack.Pop();
                    if (type != DataType.Int32)
                    {
                        if (type == DataType.Variable && ctx.BaseContext.IsGMS23)
                        {
                            // Use magic -9 to reference stacktop instance
                            Emit(ctx, Opcode.PushI, DataType.Int16).Value = (short)-9;
                        }
                        else
                            Emit(ctx, Opcode.Conv, type, DataType.Int32);
                    }

                    // Body
                    var popEnvJump = new JumpForwardPatch(Emit(ctx, Opcode.PushEnv));
                    var startJump = new JumpBackwardPatch(ctx);
                    var endJump = new JumpForwardPatch();
                    ctx.BytecodeContexts.Push(new Context(Context.ContextKind.With, endJump, popEnvJump));
                    CompileStatement(ctx, stmt.Children[1]);

                    popEnvJump.Finish(ctx);
                    startJump.Add(Emit(ctx, Opcode.PopEnv));
                    startJump.Finish(ctx);

                    if (ctx.BytecodeContexts.Pop().BreakUsed)
                    {
                        // Need to generate custom "magic" popenv block
                        var endOfCleanupJump = new JumpForwardPatch(Emit(ctx, Opcode.B));

                        endJump.Finish(ctx);

                        // Special magic that doesn't continue the loop
                        var instr = Emit(ctx, Opcode.PopEnv);
                        instr.PopenvExitMagic = true;
                        if (ctx.BaseContext.Project.DataHandle.VersionInfo.FormatID <= 14)
                        {
                            // Older versions use a different magic value
                            instr.JumpOffset = -1048576;
                        }

                        endOfCleanupJump.Finish(ctx);
                    }
                    else
                        endJump.Finish(ctx);
                }
                break;
            case NodeKind.While:
                {
                    // Condition
                    var repeatJump = new JumpBackwardPatch(ctx);
                    CompileExpression(ctx, stmt.Children[0]);
                    ConvertTo(ctx, DataType.Boolean);
                    var endLoopJump = new JumpForwardPatch(Emit(ctx, Opcode.Bf));

                    // Body
                    ctx.BytecodeContexts.Push(new Context(Context.ContextKind.BasicLoop, endLoopJump, repeatJump));
                    CompileStatement(ctx, stmt.Children[1]);
                    ctx.BytecodeContexts.Pop();
                    repeatJump.Add(Emit(ctx, Opcode.B));
                    repeatJump.Finish(ctx);

                    endLoopJump.Finish(ctx);
                }
                break;
            case NodeKind.For:
                {
                    // Initial statement
                    CompileStatement(ctx, stmt.Children[0]);

                    // Condition
                    var repeatJump = new JumpBackwardPatch(ctx);
                    CompileExpression(ctx, stmt.Children[1]);
                    ConvertTo(ctx, DataType.Boolean);
                    var endLoopJump = new JumpForwardPatch(Emit(ctx, Opcode.Bf));

                    // Body
                    var continuePatch = new JumpForwardPatch();
                    ctx.BytecodeContexts.Push(new Context(Context.ContextKind.BasicLoop, endLoopJump, continuePatch));
                    CompileStatement(ctx, stmt.Children[3]);
                    ctx.BytecodeContexts.Pop();

                    // Iteration statement
                    continuePatch.Finish(ctx);
                    CompileStatement(ctx, stmt.Children[2]);
                    repeatJump.Add(Emit(ctx, Opcode.B));
                    repeatJump.Finish(ctx);

                    endLoopJump.Finish(ctx);
                }
                break;
            case NodeKind.Repeat:
                {
                    // Repeat counter
                    CompileExpression(ctx, stmt.Children[0]);
                    ConvertTo(ctx, DataType.Int32);

                    // This loop type keeps its counter on the stack, starting here
                    Emit(ctx, Opcode.Dup, DataType.Int32);
                    Emit(ctx, Opcode.Push, DataType.Int32).Value = 0; // special instruction
                    EmitCompare(ctx, ComparisonType.LTE, DataType.Int32, DataType.Int32);
                    var endJump = new JumpForwardPatch(Emit(ctx, Opcode.Bt));
                    var continueJump = new JumpForwardPatch();

                    // Body
                    var startJump = new JumpBackwardPatch(ctx);
                    ctx.BytecodeContexts.Push(new Context(Context.ContextKind.Repeat, endJump, continueJump));
                    CompileStatement(ctx, stmt.Children[1]);
                    ctx.BytecodeContexts.Pop();

                    // Decrement counter
                    continueJump.Finish(ctx);
                    Emit(ctx, Opcode.Push, DataType.Int32).Value = 1; // special instruction
                    Emit(ctx, Opcode.Sub, DataType.Int32, DataType.Int32);
                    Emit(ctx, Opcode.Dup, DataType.Int32);
                    Emit(ctx, Opcode.Conv, DataType.Int32, DataType.Boolean);
                    startJump.Add(Emit(ctx, Opcode.Bt));
                    startJump.Finish(ctx);

                    // Cleanup
                    endJump.Finish(ctx);
                    Emit(ctx, Opcode.Popz, DataType.Int32);
                }
                break;
            case NodeKind.DoUntil:
                {
                    var endJump = new JumpForwardPatch();
                    var continueJump = new JumpForwardPatch();

                    // Body
                    var startJump = new JumpBackwardPatch(ctx);
                    ctx.BytecodeContexts.Push(new Context(Context.ContextKind.BasicLoop, endJump, continueJump));
                    CompileStatement(ctx, stmt.Children[0]);
                    ctx.BytecodeContexts.Pop();

                    // Condition
                    continueJump.Finish(ctx);
                    CompileExpression(ctx, stmt.Children[1]);
                    ConvertTo(ctx, DataType.Boolean);
                    startJump.Add(Emit(ctx, Opcode.Bf));
                    startJump.Finish(ctx);

                    endJump.Finish(ctx);
                }
                break;
            case NodeKind.FunctionDecl:
                CompileFunctionDecl(ctx, stmt);

                // The declaration produces garbage on the stack; clean it up
                Emit(ctx, Opcode.Popz, ctx.TypeStack.Pop());
                break;
            case NodeKind.Static:
                CompileStatic(ctx, stmt);
                break;
            case NodeKind.ChainReference:
                {
                    int stackCount = ctx.TypeStack.Count;
                    CompileExpression(ctx, stmt);
                    if (ctx.TypeStack.Count > stackCount)
                    {
                        // The chain produced garbage on the stack; clean it up
                        Emit(ctx, Opcode.Popz, ctx.TypeStack.Pop());
                    }
                }
                break;
            case NodeKind.Postfix:
            case NodeKind.Prefix:
                CompilePrefix(ctx, stmt, false);
                break;
        }
    }

    private static void CompileBlock(CodeContext ctx, Node block)
    {
        foreach (Node n in block.Children)
            CompileStatement(ctx, n);
    }

    private static void CompileFunctionCall(CodeContext ctx, Node func, bool inChain)
    {
        Token token = func.Token;
        TokenFunction tokenFunc = token.Value as TokenFunction;
        FunctionReference funcRef = null;
        if (func.Kind == NodeKind.Variable)
        {
            if (ctx.BaseContext.Functions.TryGetValue((token.Value as TokenVariable).Name, out funcRef))
            {
                // TODO: check argument counts if possible here?
                tokenFunc = new TokenFunction(funcRef.Name, null);
            }
        }
        else if (tokenFunc != null && tokenFunc.Builtin == null)
        {
            if (ctx.BaseContext.Functions.TryGetValue(tokenFunc.Name, out funcRef))
            {
                // TODO: check argument counts if possible here?
                tokenFunc = new TokenFunction(funcRef.Name, null);
            }
        }
        if (funcRef == null && tokenFunc?.Builtin == null)
        {
            // TODO: properly handle single variable calls
            ctx.Error("Unsupported", token);
            return;
        }

        if (ctx.BaseContext.IsGMS23)
        {
            if (inChain)
            {
                // TODO
                ctx.Error("Unsupported", -1);
            }
            else
            {
                // Arguments get pushed in reverse order
                for (int i = func.Children.Count - 1; i >= 0; i--)
                {
                    CompileExpression(ctx, func.Children[i]);
                    ConvertTo(ctx, DataType.Variable);
                }

                EmitCall(ctx, Opcode.Call, DataType.Int32, tokenFunc, func.Children.Count, token, funcRef);
                ctx.TypeStack.Push(DataType.Variable);
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

            EmitCall(ctx, Opcode.Call, DataType.Int32, tokenFunc, func.Children.Count, token, funcRef);
            ctx.TypeStack.Push(DataType.Variable);
        }
    }

    private static void ExitCleanup(CodeContext ctx)
    {
        // Perform cleanup for contexts
        foreach (var context in ctx.BytecodeContexts)
        {
            switch (context.Kind)
            {
                case Context.ContextKind.Switch:
                    // Clean up duplicated values left on the stack by switch statements
                    Emit(ctx, Opcode.Popz, context.DuplicatedType);
                    break;
                case Context.ContextKind.With:
                    {
                        // This is a special instruction that signals to not continue the with loop
                        var instr = Emit(ctx, Opcode.PopEnv);
                        instr.PopenvExitMagic = true;
                        if (ctx.BaseContext.Project.DataHandle.VersionInfo.FormatID <= 14)
                        {
                            // Older versions use a different magic value
                            instr.JumpOffset = -1048576;
                        }
                    }
                    break;
                case Context.ContextKind.Repeat:
                    // Clean up repeat counters on stack
                    Emit(ctx, Opcode.Popz, DataType.Int32);
                    break;
            }
        }
    }

    private static void CompileAssign(CodeContext ctx, Node lhs)
    {
        DataType type = ctx.TypeStack.Pop();

        if (lhs.Kind == NodeKind.Variable)
        {
            if (lhs.Children.Count == 0)
            {
                // Simple variable
                TokenVariable tokenVar = lhs.Token.Value as TokenVariable;
                ProcessTokenVariable(ctx, ref tokenVar);
                EmitVariable(ctx, Opcode.Pop, DataType.Variable, tokenVar, type);
            }
            else
            {
                // Variable with array
                var variable = lhs.Token.Value as TokenVariable;
                ProcessTokenVariable(ctx, ref variable);

                // If 2.3, convert expression to variable
                if (ctx.BaseContext.IsGMS23)
                {
                    if (type != DataType.Variable)
                    {
                        Emit(ctx, Opcode.Conv, type, DataType.Variable);
                        type = DataType.Variable;
                    }
                }

                // Compile instance type
                CompileConstant(ctx, new TokenConstant((double)variable.InstanceType));
                ConvertToInstance(ctx);

                // Array index
                CompileExpression(ctx, lhs.Children[0]);
                ConvertTo(ctx, DataType.Int32);

                if (lhs.Children.Count > 1)
                {
                    // 2d array or above
                    if (!ctx.BaseContext.IsGMS23)
                        ctx.Error("Chained array usage not supported pre-2.3", lhs.Token);
                    variable.VariableType = VariableType.MultiPushPop;
                    if (ctx.BaseContext.IsGMS23)
                        variable.InstanceType = (int)InstanceType.Self;
                    EmitVariable(ctx, Opcode.Push, DataType.Variable, variable);
                    CompileMultiArrayPop(ctx, lhs);
                }
                else
                {
                    // 1d array
                    variable.VariableType = VariableType.Array;
                    if (ctx.BaseContext.IsGMS23)
                        variable.InstanceType = (int)InstanceType.Self;
                    EmitVariable(ctx, Opcode.Pop, DataType.Variable, variable, type);
                }
            }
        }
        else if (lhs.Kind == NodeKind.ChainReference)
        {
            Node end = lhs.Children[1];
            if (end.Kind != NodeKind.Variable)
            {
                ctx.Error("Invalid end of chain reference", end.Token);
                return;
            }
            var variable = end.Token.Value as TokenVariable;

            // If this is an array
            if (end.Children.Count != 0)
            {
                // In 2.3+, convert expression to variable data type (in case it's needed, apparently)
                if (ctx.BaseContext.IsGMS23)
                {
                    if (type != DataType.Variable)
                    {
                        Emit(ctx, Opcode.Conv, type, DataType.Variable);
                        type = DataType.Variable;
                    }
                }
            }

            // Compile left side of dot
            CompileExpression(ctx, lhs.Children[0]);
            ConvertToInstance(ctx);

            // If this is an array (again)
            if (end.Children.Count != 0)
            {
                // Array index
                if (ctx.BaseContext.IsGMS23)
                {
                    CompileExpression(ctx, end.Children[0]);
                    ConvertTo(ctx, DataType.Int32);

                    if (end.Children.Count > 1)
                    {
                        // 2d array or above
                        variable.VariableType = VariableType.MultiPushPop;
                        EmitVariable(ctx, Opcode.Push, DataType.Variable, variable);
                        CompileMultiArrayPop(ctx, end);
                    }
                    else
                    {
                        // 1d array
                        variable.VariableType = VariableType.Array;
                        EmitVariable(ctx, Opcode.Pop, DataType.Variable, variable, type);
                    }
                }
                else
                {
                    if (end.Children.Count >= 2)
                    {
                        // Prior to 2.3, there's fake "2d" arrays... this is how they're handled
                        // Essentially just ((index1 * 32000) + index2)
                        CompileExpression(ctx, end.Children[0]); // first index
                        ConvertTo(ctx, DataType.Int32);
                        Emit(ctx, Opcode.Push, DataType.Int32).Value = (int)32000;
                        Emit(ctx, Opcode.Mul, DataType.Int32, DataType.Int32);
                        CompileExpression(ctx, end.Children[1]); // second index
                        ConvertTo(ctx, DataType.Int32);
                        EmitBreak(ctx, BreakType.chkindex);
                        Emit(ctx, Opcode.Add, DataType.Int32, DataType.Int32);
                    }
                    else
                    {
                        // Only compile single index
                        CompileExpression(ctx, end.Children[0]);
                        ConvertTo(ctx, DataType.Int32);
                    }

                    // Actually store into variable now
                    variable.VariableType = VariableType.Array;
                    EmitVariable(ctx, Opcode.Pop, DataType.Variable, variable, type);
                }
            }
            else
            {
                // Normal variable at the end, no arrays or anything
                variable.VariableType = VariableType.StackTop;
                EmitVariable(ctx, Opcode.Pop, DataType.Variable, variable, type);
            }
        }
        else
            ctx.Error("Unsupported assignment", lhs.Token);
    }

    private static void CompileCompoundAssign(CodeContext ctx, Node assign, Opcode opcode, bool bitwise = false)
    {
        Node lhs = assign.Children[0], expr = assign.Children[1];

        if (lhs.Kind == NodeKind.Variable)
        {
            // Push original variable value
            EmitVariable(ctx, Opcode.Push, DataType.Variable, lhs.Token.Value as TokenVariable);

            // Push expression and convert to necessary data types
            CompileExpression(ctx, expr);
            DataType type = ctx.TypeStack.Pop();
            if (bitwise)
            {
                ConvertTo(ctx, DataType.Int64);
                type = DataType.Int64;
            }
            else
            {
                if (type == DataType.Boolean)
                {
                    Emit(ctx, Opcode.Conv, DataType.Boolean, DataType.Int32);
                    type = DataType.Int32;
                }
            }

            // Actual specific operation
            Emit(ctx, opcode, type, DataType.Variable);

            // Store result in original variable
            TokenVariable tokenVar = lhs.Token.Value as TokenVariable;
            ProcessTokenVariable(ctx, ref tokenVar);
            EmitVariable(ctx, Opcode.Pop, DataType.Variable, tokenVar, DataType.Variable);
        }
        else if (lhs.Kind == NodeKind.ChainReference)
        {
            Node end = lhs.Children[1];
            if (end.Kind != NodeKind.Variable)
            {
                ctx.Error("Invalid end of chain reference", end.Token);
                return;
            }
            var variable = end.Token.Value as TokenVariable;

            // Compile left side of dot first
            CompileExpression(ctx, lhs.Children[0]);

            // Compile end of chain separately
            bool usedMagic = (ConvertToInstance(ctx) == 2);

            // Need to duplicate the instance on the stack
            EmitDup(ctx, DataType.Int32, usedMagic ? (byte)4 : (byte)0);

            // Push original variable value, finally
            variable.VariableType = VariableType.StackTop;
            EmitVariable(ctx, Opcode.Push, DataType.Variable, variable);

            // Push expression and convert to necessary data types
            CompileExpression(ctx, expr);
            DataType type = ctx.TypeStack.Pop();
            if (bitwise)
            {
                ConvertTo(ctx, DataType.Int64);
                type = DataType.Int64;
            }
            else
            {
                if (type == DataType.Boolean)
                {
                    Emit(ctx, Opcode.Conv, DataType.Boolean, DataType.Int32);
                    type = DataType.Int32;
                }
            }

            // Actual specific operation
            Emit(ctx, opcode, type, DataType.Variable);

            // Store result in original variable
            EmitVariable(ctx, Opcode.Pop, DataType.Variable, variable, DataType.Variable);
        }
        else
            ctx.Error("Unsupported compound assignment", lhs.Token);
    }

    private static void CompilePrefix(CodeContext ctx, Node prefix, bool leaveOnStack)
    {
        Node expr = prefix.Children[0];

        if (expr.Kind == NodeKind.Variable)
        {
            // Push original variable value
            var variable = expr.Token.Value as TokenVariable;
            EmitVariable(ctx, Opcode.Push, DataType.Variable, variable);

            // Push int16 1 (special instruction here)
            Emit(ctx, Opcode.Push, DataType.Int16).Value = (short)1;

            // Actual operation
            if (prefix.Token.Kind == TokenKind.Increment)
                Emit(ctx, Opcode.Add, DataType.Int32, DataType.Variable);
            else
                Emit(ctx, Opcode.Sub, DataType.Int32, DataType.Variable);

            if (leaveOnStack)
            {
                // Duplicate value to leave it on stack
                EmitDup(ctx, DataType.Variable, 0);
                ctx.TypeStack.Push(DataType.Variable);
            }

            // Store result into original variable
            EmitVariable(ctx, Opcode.Pop, DataType.Variable, variable, DataType.Variable);
        }
        else if (expr.Kind == NodeKind.ChainReference)
        {
            // Compile left side of dot first
            CompileExpression(ctx, expr.Children[0]);

            // Compile end of chain separately
            switch (expr.Children[1].Kind)
            {
                case NodeKind.Variable:
                    bool usedMagic = (ConvertToInstance(ctx) == 2);

                    // Need to duplicate the instance on the stack
                    EmitDup(ctx, DataType.Int32, usedMagic ? (byte)4 : (byte)0);

                    // Push original variable value, finally
                    var variable = expr.Children[1].Token.Value as TokenVariable;
                    variable.VariableType = VariableType.StackTop;
                    EmitVariable(ctx, Opcode.Push, DataType.Variable, variable);

                    // Push int16 1 (special instruction here)
                    Emit(ctx, Opcode.Push, DataType.Int16).Value = (short)1;

                    // Actual operation
                    if (prefix.Token.Kind == TokenKind.Increment)
                        Emit(ctx, Opcode.Add, DataType.Int32, DataType.Variable);
                    else
                        Emit(ctx, Opcode.Sub, DataType.Int32, DataType.Variable);

                    if (leaveOnStack)
                    {
                        // Duplicate value to leave it on stack
                        EmitDup(ctx, DataType.Variable, 1);
                        if (ctx.BaseContext.IsGMS23)
                            EmitDupSwap(ctx, DataType.Int32, 4, usedMagic ? (byte)8 : (byte)4);
                        else
                        {
                            // pre-2.3 swap instruction
                            Emit(ctx, Opcode.Pop, DataType.Int16, DataType.Variable).TypeInst = (InstanceType)5;
                        }
                        ctx.TypeStack.Push(DataType.Variable);
                    }

                    // Store result in original variable
                    EmitVariable(ctx, Opcode.Pop, DataType.Variable, variable, DataType.Int32);
                    break;
                default:
                    ctx.Error("Unsupported prefix operation", expr.Children[1].Token);
                    break;
            }
        }
        else
            ctx.Error("Unsupported prefix operation #2", expr.Token);
    }

    private static void CompileMultiArrayPop(CodeContext ctx, Node variable)
    {
        for (int i = 1; i < variable.Children.Count; i++)
        {
            // Push next array index to stack
            CompileExpression(ctx, variable.Children[i]);
            ConvertTo(ctx, DataType.Int32);

            // Actual array access/pop instructions
            if (i == variable.Children.Count - 1)
            {
                // For the last one, pop
                EmitBreak(ctx, BreakType.popaf);
            }
            else
            {
                // For every other one, push
                EmitBreak(ctx, BreakType.pushac);
            }
        }
    }

    private static void CompileStatic(CodeContext ctx, Node block)
    {
        // Enter the static state
        bool prevStatic = ctx.InStaticBlock;
        ctx.InStaticBlock = true;

        // Jump past static block if already initialized statics
        EmitBreak(ctx, BreakType.isstaticok);
        var conditionJump = new JumpForwardPatch(Emit(ctx, Opcode.Bt));

        // Actually compile assignments
        foreach (Node variable in block.Children)
        {
            // Remove and compile inner expression
            Node expr = variable.Children[0];
            variable.Children.Clear();
            CompileExpression(ctx, expr);

            // Assign to static variable
            CompileAssign(ctx, variable);
        }

        // End of the static block (mark function as having statics initialized)
        conditionJump.Finish(ctx);
        EmitBreak(ctx, BreakType.setstatic);

        // Return to previous static state
        ctx.InStaticBlock = prevStatic;
    }

    private static void CompileFunctionDecl(CodeContext ctx, Node func)
    {
        NodeFunctionInfo info = func.Info as NodeFunctionInfo;

        // When running the outer code, branch past the function contents
        var jump = new JumpForwardPatch(Emit(ctx, Opcode.B));

        // Going into a new scope; need to setup variables and structures
        var outerLocalVars = ctx.LocalVars;
        var outerStaticVars = ctx.StaticVars;
        var outerArgumentVars = ctx.ArgumentVars;
        var outerBytecodeContexts = ctx.BytecodeContexts;
        bool outerInStatic = ctx.InStaticBlock;
        ctx.LocalVars = info.LocalVars;
        ctx.StaticVars = info.StaticVars;
        ctx.ArgumentVars = info.Arguments;
        ctx.BytecodeContexts = new();
        ctx.InStaticBlock = false;

        // Make sure this declaration is linked up for later
        ctx.FunctionDeclsToRegister.Add(new(info.Reference.Name, info.LocalVars.Count, 
                                            info.Arguments.Count, ctx.BytecodeLength, info.IsConstructor));

        if (info.OptionalArgsIndex != -1)
        {
            // Compile optional arguments into if statements
            CompileBlock(ctx, func.Children[info.OptionalArgsIndex]);
        }
        if (info.InheritingIndex != -1)
        {
            // Compile function inheritance
            Node inheritedFuncCall = func.Children[info.InheritingIndex];
            CompileFunctionCall(ctx, inheritedFuncCall, false);

            if (ctx.BaseContext.Functions.TryGetValue((inheritedFuncCall.Token.Value as TokenVariable).Name, out var funcRef))
            {
                // Push inherited function ID
                EmitPushFunc(ctx, new TokenFunction(funcRef.Name, null), funcRef);

                // Using that ID, call @@CopyStatic@@ to have the runner
                // magically copy static variables from inherited function!
                Emit(ctx, Opcode.Conv, DataType.Int32, DataType.Variable);
                EmitCall(ctx, Opcode.Call, DataType.Int32, Builtins.MakeFuncToken(ctx, "@@CopyStatic@@"), 1);
            }
            else
                ctx.Error("Couldn't find inherited/base function", inheritedFuncCall.Token);
        }

        // Actually compile the main function contents
        CompileBlock(ctx, func.Children[^1]);
        Emit(ctx, Opcode.Exit, DataType.Int32);

        // Leaving scope; restore variables from outer scope
        ctx.LocalVars = outerLocalVars;
        ctx.StaticVars = outerStaticVars;
        ctx.ArgumentVars = outerArgumentVars;
        ctx.BytecodeContexts = outerBytecodeContexts;
        ctx.InStaticBlock = outerInStatic;

        // Now, after function contents, emit instructions to register the function
        jump.Finish(ctx);

        // Push function ID
        EmitPushFunc(ctx, new TokenFunction(info.Reference.Name, null), info.Reference);
        Emit(ctx, Opcode.Conv, DataType.Int32, DataType.Variable);

        if (info.IsConstructor)
        {
            // Constructors instantiate a null object in the runner for a context
            EmitCall(ctx, Opcode.Call, DataType.Int32, Builtins.MakeFuncToken(ctx, "@@NullObject@@"), 0);
        }
        else
        {
            // Normal functions either bind to -1 (self) or -16 (static) as a context
            Emit(ctx, Opcode.PushI, DataType.Int16).Value = (short)(ctx.InStaticBlock ? -16 : -1);
            Emit(ctx, Opcode.Conv, DataType.Int32, DataType.Variable);
        }

        // Create the method using the context and function ID
        EmitCall(ctx, Opcode.Call, DataType.Int32, Builtins.MakeFuncToken(ctx, "method"), 2);

        if (func.Children[0].Kind != NodeKind.Empty)
        {
            // This isn't anonymous, so assign the function to a real name
            // Need to duplicate the return value of method(), then store it
            EmitDup(ctx, DataType.Variable, 0);
            ctx.TypeStack.Push(DataType.Variable);

            // For some reason storing here happens with a -6 (builtin?) instance
            // Need to build a chain node for that, I guess...?
            Node store = new(NodeKind.ChainReference);
            store.Children.Add(new(NodeKind.Constant, new Token(ctx, new TokenConstant((double)-6), -1)));
            store.Children.Add(func.Children[0]);
            CompileAssign(ctx, store);
        }

        // The result of method() is still on the stack after this
        ctx.TypeStack.Push(DataType.Variable);
    }

    private static void CompileNew(CodeContext ctx, Node stmt)
    {
        // Push arguments in reverse order
        for (int i = stmt.Children.Count - 1; i >= 1; i--)
        {
            CompileExpression(ctx, stmt.Children[i]);
            ConvertTo(ctx, DataType.Variable);
        }

        // Push function reference
        Node func = stmt.Children[0];
        if (func.Kind == NodeKind.Variable)
        {
            TokenVariable tokenVar = func.Token.Value as TokenVariable;
            if (ctx.BaseContext.Functions.TryGetValue(tokenVar.Name, out FunctionReference reference))
            {
                // Known function ID/reference
                EmitPushFunc(ctx, new TokenFunction(reference.Name, null), reference);
                Emit(ctx, Opcode.Conv, DataType.Int32, DataType.Variable);
            }
            else
            {
                // Unknown function variable
                CompileExpression(ctx, stmt.Children[0]);
                ConvertTo(ctx, DataType.Variable);
            }
        }
        else
        {
            // Unknown function expression
            CompileExpression(ctx, stmt.Children[0]);
            ConvertTo(ctx, DataType.Variable);
        }

        EmitCall(ctx, Opcode.Call, DataType.Int32, Builtins.MakeFuncToken(ctx, "@@NewGMLObject@@"), stmt.Children.Count);
        ctx.TypeStack.Push(DataType.Variable);
    }
}
