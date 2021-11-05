using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using DogScepterLib.Core;
using DogScepterLib.Core.Models;
using System.Globalization;
using DogScepterLib.Core.Chunks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    public static class Disassembler
    {
        public static Dictionary<Instruction.DataType, char> DataTypeToChar = new Dictionary<Instruction.DataType, char>()
        {
            { Instruction.DataType.Double, 'd' },
            { Instruction.DataType.Float, 'f' },
            { Instruction.DataType.Int32, 'i' },
            { Instruction.DataType.Int64, 'l' },
            { Instruction.DataType.Boolean, 'b' },
            { Instruction.DataType.Variable, 'v' },
            { Instruction.DataType.String, 's' },
            { Instruction.DataType.Int16, 'e' }
        };
        public static Dictionary<char, Instruction.DataType> CharToDataType = new Dictionary<char, Instruction.DataType>()
        {
            { 'd', Instruction.DataType.Double },
            { 'f', Instruction.DataType.Float },
            { 'i', Instruction.DataType.Int32 },
            { 'l', Instruction.DataType.Int64 },
            { 'b', Instruction.DataType.Boolean },
            { 'v', Instruction.DataType.Variable },
            { 's', Instruction.DataType.String },
            { 'e', Instruction.DataType.Int16}
        };
        public static Dictionary<ushort, string> BreakIDToName = new Dictionary<ushort, string>()
        {
            { 65535, "chkindex" },
            { 65534, "pushaf" },
            { 65533, "popaf" },
            { 65532, "pushac" },
            { 65531, "setowner" },
            { 65530, "isstaticok" },
            { 65529, "setstatic" },
            { 65528, "savearef" },
            { 65527, "restorearef" },
            { 65526, "isnullish" }
        };

        public static string Disassemble(GMCode codeEntry, GMData data)
        {
            GMCode.Bytecode bytecode = codeEntry.BytecodeEntry;
            IList<GMString> strings = ((GMChunkSTRG)data.Chunks["STRG"]).List;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"# Name: {codeEntry.Name.Content}");
            if (codeEntry.BytecodeOffset != 0) // Usually should be 0, but for information sake write this
                sb.AppendLine($"# Offset: {codeEntry.BytecodeOffset}");

            List<int> blocks = FindBlockAddresses(bytecode);
            foreach (var i in bytecode.Instructions)
            {
                int ind = blocks.IndexOf(i.Address);
                if (ind != -1)
                {
                    sb.AppendLine();
                    sb.AppendLine($":[{ind}]");
                }

                if (i.Kind != Instruction.Opcode.Break)
                    sb.Append(i.Kind.ToString().ToLower());

                switch (Instruction.GetInstructionType(i.Kind))
                {
                    case Instruction.InstructionType.SingleType:
                        sb.Append($".{DataTypeToChar[i.Type1]}");

                        if (i.Kind == Instruction.Opcode.Dup || i.Kind == Instruction.Opcode.CallV)
                        {
                            sb.Append($" {i.Extra}");
                        }
                        break;

                    case Instruction.InstructionType.DoubleType:
                        sb.Append($".{DataTypeToChar[i.Type1]}.{DataTypeToChar[i.Type2]}");
                        break;

                    case Instruction.InstructionType.Comparison:
                        sb.Append($".{DataTypeToChar[i.Type1]}.{DataTypeToChar[i.Type2]} {i.ComparisonKind}");
                        break;

                    case Instruction.InstructionType.Branch:
                        if (i.Address + (i.JumpOffset * 4) == codeEntry.Length)
                            sb.Append(" [end]");
                        else if (i.PopenvExitMagic)
                            sb.Append(" [magic]"); // magic popenv instruction when returning early inside a with statement
                        else
                            sb.Append($" [{blocks.IndexOf(i.Address + (i.JumpOffset * 4))}]");
                        break;

                    case Instruction.InstructionType.Pop:
                        sb.Append($".{DataTypeToChar[i.Type1]}.{DataTypeToChar[i.Type2]} ");
                        if (i.Type1 == Instruction.DataType.Int16)
                            sb.Append(((short)i.TypeInst).ToString()); // Special swap instruction
                        else
                        {
                            if (i.Type1 == Instruction.DataType.Variable && i.TypeInst != Instruction.InstanceType.Undefined)
                            {
                                sb.Append($"{i.TypeInst.ToString().ToLower()}.");
                            }

                            sb.Append(StringifyVariableRef(i.Variable));
                        }
                        break;

                    case Instruction.InstructionType.Push:
                        sb.Append($".{DataTypeToChar[i.Type1]} ");
                        if (i.Type1 == Instruction.DataType.Variable)
                        {
                            if (i.TypeInst != Instruction.InstanceType.Undefined)
                                sb.Append($"{i.TypeInst.ToString().ToLower()}.");

                            sb.Append(StringifyVariableRef(i.Variable));
                        }
                        else if (i.Type1 == Instruction.DataType.String)
                            sb.Append($"\"{SanitizeString(strings[(int)i.Value].Content)}\"");
                        else if (i.Function != null)
                            sb.Append(i.Function.Target.Name.Content);
                        else
                            sb.Append((i.Value as IFormattable)?.ToString(null, CultureInfo.InvariantCulture) ?? i.Value.ToString());
                        break;

                    case Instruction.InstructionType.Call:
                        sb.Append($".{DataTypeToChar[i.Type1]} {i.Function.Target.Name.Content} {(short)i.Value}");
                        break;

                    case Instruction.InstructionType.Break:
                        sb.Append($"{BreakIDToName[(ushort)i.Value]}.{DataTypeToChar[i.Type1]}");
                        break;
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.Append(":[end]");

            return sb.ToString();
        }

        public static List<int> FindBlockAddresses(GMCode.Bytecode bytecode, bool slow = true)
        {
            HashSet<int> addresses = new HashSet<int>();

            if (bytecode.Instructions.Count != 0)
            {
                addresses.Add(0);
                for (int i = 0; i < bytecode.Instructions.Count; i++)
                {
                    Instruction instr = bytecode.Instructions[i];
                    switch (instr.Kind)
                    {
                        case Instruction.Opcode.B:
                        case Instruction.Opcode.Bf:
                        case Instruction.Opcode.Bt:
                        case Instruction.Opcode.PushEnv:
                            addresses.Add(instr.Address + 4);
                            addresses.Add(instr.Address + (instr.JumpOffset * 4));
                            break;
                        case Instruction.Opcode.PopEnv:
                            if (!instr.PopenvExitMagic)
                                addresses.Add(instr.Address + (instr.JumpOffset * 4));
                            break;
                        case Instruction.Opcode.Exit:
                        case Instruction.Opcode.Ret:
                            addresses.Add(instr.Address + 4);
                            break;
                        case Instruction.Opcode.Call:
                            if (slow && i >= 4 && instr.Function.Target.Name?.Content == "@@try_hook@@")
                            {
                                int finallyBlock = (int)bytecode.Instructions[i - 4].Value;
                                addresses.Add(finallyBlock);

                                int catchBlock = (int)bytecode.Instructions[i - 2].Value;
                                if (catchBlock != -1)
                                    addresses.Add(catchBlock);

                                // Technically not usually a block here (before/after the call), but for our purposes, 
                                // this is easier to split into its own section to isolate it now.
                                addresses.Add(instr.Address - 24);
                                addresses.Add(instr.Address + 12);
                            }
                            break;
                    }
                }
            }

            List<int> res = addresses.ToList();
            res.Sort();
            return res;
        }

        public static string SanitizeString(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\n");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\v':
                        sb.Append("\\v");
                        break;
                    case '\a':
                        sb.Append("\\a");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string StringifyVariableRef(Instruction.Reference<GMVariable> var)
        {
            if (var.Type != Instruction.VariableType.Normal)
                return $"({var.Type.ToString().ToLower()}){var.Target.Name.Content}";
            else
                return var.Target.Name.Content;
        }
    }
}
