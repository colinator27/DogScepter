using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Compiler
{
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
        // todo: function classifications?

        public BuiltinFunction(Builtins ctx, string name, int argumentCount)
        {
            Name = name;
            ArgumentCount = argumentCount;
            ID = ctx.ID++;
        }
    }

    public class Builtins
    {
        public CompileContext Context { get; set; }

        public Dictionary<string, double> Constants { get; init; } = new();
        public Dictionary<string, BuiltinVariable> VarGlobal { get; init; } = new();
        public Dictionary<string, BuiltinVariable> VarGlobalArray { get; init; } = new();
        public Dictionary<string, BuiltinVariable> VarInstance { get; init; } = new();
        public Dictionary<string, BuiltinFunction> Functions { get; init; } = new();

        public int ID = 0;

        public Builtins(CompileContext ctx)
        {
            Context = ctx;

            // Should always have ID 0
            VarGlobalDefine("undefined", false);

            VarInstanceDefine("x");
            VarInstanceDefine("y");

            FunctionDefine("show_message", 1);
        }

        private void VarGlobalDefine(string name, bool canSet = true, bool canGet = true)
        {
            VarGlobal[name] = new BuiltinVariable(this, name, canSet, canGet);
        }

        private void VarInstanceDefine(string name, bool canSet = true, bool canGet = true)
        {
            VarInstance[name] = new BuiltinVariable(this, name, canSet, canGet);
        }

        private void FunctionDefine(string name, int argCount)
        {
            Functions[name] = new BuiltinFunction(this, name, argCount);
        }
    }
}
