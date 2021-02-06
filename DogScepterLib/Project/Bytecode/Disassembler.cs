using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using DogScepterLib.Core;
using DogScepterLib.Core.Models;
using System.Globalization;
using DogScepterLib.Core.Chunks;

namespace DogScepterLib.Project.Bytecode
{
    public static class Disassembler
    {
        public static Dictionary<GMCode.Bytecode.Instruction.DataType, char> DataTypeToChar = new Dictionary<GMCode.Bytecode.Instruction.DataType, char>()
        {
            { GMCode.Bytecode.Instruction.DataType.Double, 'd' },
            { GMCode.Bytecode.Instruction.DataType.Float, 'f' },
            { GMCode.Bytecode.Instruction.DataType.Int32, 'i' },
            { GMCode.Bytecode.Instruction.DataType.Int64, 'l' },
            { GMCode.Bytecode.Instruction.DataType.Boolean, 'b' },
            { GMCode.Bytecode.Instruction.DataType.Variable, 'v' },
            { GMCode.Bytecode.Instruction.DataType.String, 's' },
            { GMCode.Bytecode.Instruction.DataType.Int16, 'e' }
        };
        public static Dictionary<char, GMCode.Bytecode.Instruction.DataType> CharToDataType = new Dictionary<char, GMCode.Bytecode.Instruction.DataType>()
        {
            { 'd', GMCode.Bytecode.Instruction.DataType.Double },
            { 'f', GMCode.Bytecode.Instruction.DataType.Float },
            { 'i', GMCode.Bytecode.Instruction.DataType.Int32 },
            { 'l', GMCode.Bytecode.Instruction.DataType.Int64 },
            { 'b', GMCode.Bytecode.Instruction.DataType.Boolean },
            { 'v', GMCode.Bytecode.Instruction.DataType.Variable },
            { 's', GMCode.Bytecode.Instruction.DataType.String },
            { 'e', GMCode.Bytecode.Instruction.DataType.Int16}
        };
        public static Dictionary<short, string> BreakIDToName = new Dictionary<short, string>()
        {
            { -1, "chkindex" },
            { -2, "pushaf" },
            { -3, "popaf" },
            { -4, "pushac" },
            { -5, "setowner" },
            { -6, "isstaticok" },
            { -7, "setstatic" }
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

                if (i.Kind != GMCode.Bytecode.Instruction.Opcode.Break)
                    sb.Append(i.Kind.ToString().ToLower());

                switch (GMCode.Bytecode.Instruction.GetInstructionType(i.Kind))
                {
                    case GMCode.Bytecode.Instruction.InstructionType.SingleType:
                        sb.Append($".{DataTypeToChar[i.Type1]}");

                        if (i.Kind == GMCode.Bytecode.Instruction.Opcode.Dup || 
                            i.Kind == GMCode.Bytecode.Instruction.Opcode.CallV)
                        {
                            sb.Append($" {i.Extra}");
                        }
                        break;

                    case GMCode.Bytecode.Instruction.InstructionType.DoubleType:
                        sb.Append($".{DataTypeToChar[i.Type1]}.{DataTypeToChar[i.Type2]}");
                        break;

                    case GMCode.Bytecode.Instruction.InstructionType.Comparison:
                        sb.Append($".{DataTypeToChar[i.Type1]}.{DataTypeToChar[i.Type2]} {i.ComparisonKind}");
                        break;

                    case GMCode.Bytecode.Instruction.InstructionType.Branch:
                        if (i.Address + (i.JumpOffset * 4) == codeEntry.Length)
                            sb.Append(" [end]");
                        else if (i.PopenvExitMagic)
                            sb.Append(" [magic]"); // magic popenv instruction when returning early inside a with statement
                        else
                            sb.Append($" [{blocks.IndexOf(i.Address + (i.JumpOffset * 4))}]");
                        break;

                    case GMCode.Bytecode.Instruction.InstructionType.Pop:
                        sb.Append($".{DataTypeToChar[i.Type1]}.{DataTypeToChar[i.Type2]} ");
                        if (i.Type1 == GMCode.Bytecode.Instruction.DataType.Int16)
                            sb.Append(i.SwapExtra.ToString()); // Special swap instruction
                        else
                        {
                            if (i.Type1 == GMCode.Bytecode.Instruction.DataType.Variable && 
                                i.TypeInst != GMCode.Bytecode.Instruction.InstanceType.Undefined)
                            {
                                sb.Append($"{i.TypeInst.ToString().ToLower()}.");
                            }

                            sb.Append(StringifyVariableRef(i.Variable));
                        }
                        break;

                    case GMCode.Bytecode.Instruction.InstructionType.Push:
                        sb.Append($".{DataTypeToChar[i.Type1]} ");
                        if (i.Type1 == GMCode.Bytecode.Instruction.DataType.Variable)
                        {
                            if (i.TypeInst != GMCode.Bytecode.Instruction.InstanceType.Undefined)
                                sb.Append($"{i.TypeInst.ToString().ToLower()}.");

                            sb.Append(StringifyVariableRef(i.Variable));
                        }
                        else if (i.Type1 == GMCode.Bytecode.Instruction.DataType.String)
                            sb.Append($"\"{SanitizeString(strings[(int)i.Value].Content)}\"");
                        else if (i.Function != null)
                            sb.Append(i.Function.Target.Name.Content);
                        else
                            sb.Append((i.Value as IFormattable)?.ToString(null, CultureInfo.InvariantCulture) ?? i.Value.ToString());
                        break;

                    case GMCode.Bytecode.Instruction.InstructionType.Call:
                        sb.Append($".{DataTypeToChar[i.Type1]} {i.Function.Target.Name.Content} {i.ArgumentsCount}");
                        break;

                    case GMCode.Bytecode.Instruction.InstructionType.Break:
                        sb.Append($"{BreakIDToName[(short)i.Value]}.{DataTypeToChar[i.Type1]}");
                        break;
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.Append(":[end]");

            return sb.ToString();
        }

        public static List<int> FindBlockAddresses(GMCode.Bytecode bytecode)
        {
            HashSet<int> addresses = new HashSet<int>();

            if (bytecode.Instructions.Count != 0)
                addresses.Add(0);

            foreach (var i in bytecode.Instructions)
            {
                switch (i.Kind)
                {
                    case GMCode.Bytecode.Instruction.Opcode.B:
                    case GMCode.Bytecode.Instruction.Opcode.Bf:
                    case GMCode.Bytecode.Instruction.Opcode.Bt:
                    case GMCode.Bytecode.Instruction.Opcode.PushEnv:
                        addresses.Add(i.Address + 4);
                        addresses.Add(i.Address + (i.JumpOffset * 4));
                        break;
                    case GMCode.Bytecode.Instruction.Opcode.PopEnv:
                        if (!i.PopenvExitMagic)
                            addresses.Add(i.Address + (i.JumpOffset * 4));
                        break;
                    case GMCode.Bytecode.Instruction.Opcode.Exit:
                    case GMCode.Bytecode.Instruction.Opcode.Ret:
                        addresses.Add(i.Address + 4);
                        break;
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

        private static string StringifyVariableRef(GMCode.Bytecode.Instruction.Reference<GMVariable> var)
        {
            if (var.Type != GMCode.Bytecode.Instruction.Reference<GMVariable>.VariableType.Normal)
                return $"({var.Type.ToString().ToLower()}){var.Target.Name.Content}";
            else
                return var.Target.Name.Content;
        }
    }
}
