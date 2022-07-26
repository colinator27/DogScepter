using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DogScepterLib.Core.Chunks.GMChunkGEN8;

namespace DogScepterLib.Project.GML.Compiler;

public class BuiltinVariable
{
    public string Name { get; init; }
    public bool CanSet { get; init; }
    public bool CanGet { get; init; }
    public int ID { get; init; }

    public BuiltinVariable(Builtins ctx, string name)
    {
        Name = name;
        CanSet = true;
        CanGet = true;
        ID = ctx.ID++;
    }

    public BuiltinVariable(Builtins ctx, string name, bool canSet, bool canGet)
    {
        Name = name;
        CanSet = canSet;
        CanGet = canGet;
        ID = ctx.ID++;
    }
}

public class BuiltinFunction
{
    public string Name { get; init; }
    public int ArgumentCount { get; init; }
    public int ID { get; init; }
    public FunctionClassification Classification { get; init; }

    public BuiltinFunction(Builtins ctx, string name, int argumentCount, FunctionClassification classification)
    {
        Name = name;
        ArgumentCount = argumentCount;
        ID = ctx.ID++;
        Classification = classification;
    }
}

public partial class Builtins
{
    public CompileContext Context { get; set; }

    public Dictionary<string, double> Constants { get; init; } = new();
    public Dictionary<string, BuiltinVariable> VarGlobal { get; init; } = new();
    public Dictionary<string, BuiltinVariable> VarGlobalArray { get; init; } = new();
    public Dictionary<string, BuiltinVariable> VarInstance { get; init; } = new();
    public Dictionary<string, BuiltinFunction> Functions { get; init; } = new();
    public List<string> Arguments { get; init; } = new();

    public int ID = 0;

    public Builtins(CompileContext ctx)
    {
        Context = ctx;

        // Should always have ID 0
        VarGlobalDefine("undefined", false);

        ConstantDefine("self", -1);
        ConstantDefine("other", -2);
        ConstantDefine("all", -3);
        ConstantDefine("noone", -4);
        ConstantDefine("global", -5);
        ConstantDefine("false", 0);
        ConstantDefine("true", 1);

        FunctionDefine("@@NullObject@@", 0, FunctionClassification.None);
        FunctionDefine("@@CopyStatic@@", 1, FunctionClassification.None);

        for (int i = 0; i <= 15; i++)
            Arguments.Add($"argument{i}");
        Arguments.Add("argument");
        if (!ctx.IsGMS23)
        {
            foreach (string arg in Arguments)
                VarInstanceDefine(arg);
        }

        InitializeData(ctx.IsGMS2);
    }

    private void VarGlobalDefine(string name, bool canSet = true, bool canGet = true)
    {
        VarGlobal[name] = new BuiltinVariable(this, name, canSet, canGet);
    }

    private void VarInstanceDefine(string name, bool canSet = true, bool canGet = true)
    {
        VarInstance[name] = new BuiltinVariable(this, name, canSet, canGet);
    }

    private void FunctionDefine(string name, int argCount, FunctionClassification classification)
    {
        Functions[name] = new BuiltinFunction(this, name, argCount, classification);
    }

    private void ConstantDefine(string name, double val)
    {
        Constants[name] = val;
    }

    public static TokenFunction MakeFuncToken(CodeContext ctx, string name)
    {
        return new TokenFunction(name, ctx.BaseContext.Builtins.Functions[name]);
    }
}
