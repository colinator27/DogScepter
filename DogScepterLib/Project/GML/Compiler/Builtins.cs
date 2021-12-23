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

        public BuiltinVariable(string name)
        {
            Name = name;
            CanSet = true;
            CanGet = true;
        }

        public BuiltinVariable(string name, bool canSet, bool canGet)
        {
            Name = name;
            CanSet = canSet;
            CanGet = canGet;
        }
    }

    public class BuiltinFunction
    {
        public string Name { get; init; }
        public int ArgumentCount { get; init; }
        // todo: function classifications?

        public BuiltinFunction(string name, int argumentCount)
        {
            Name = name;
            ArgumentCount = argumentCount;
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

        public Builtins(CompileContext ctx)
        {
            Context = ctx;

            VarInstanceDefine("x");
            VarInstanceDefine("y");

            FunctionDefine("show_message", 1);
        }

        private void VarInstanceDefine(string name, bool canSet = true, bool canGet = true)
        {
            VarInstance[name] = new BuiltinVariable(name, canSet, canGet);
        }

        private void FunctionDefine(string name, int argCount)
        {
            Functions[name] = new BuiltinFunction(name, argCount);
        }
    }
}
