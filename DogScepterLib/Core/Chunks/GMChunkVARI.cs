using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkVARI : GMChunk
    {
        public List<GMVariable> List;

        public int VarCount1;
        public int VarCount2;
        public int MaxLocalVarCount;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            if (writer.VersionInfo.FormatID > 14)
            {
                // Count instance/global variables
                if (writer.VersionInfo.DifferentVarCounts)
                {
                    VarCount1 = 0;
                    VarCount2 = 0;
                    foreach (GMVariable v in List)
                    {
                        if (v.VariableType == GMCode.Bytecode.Instruction.InstanceType.Global)
                            VarCount1++;
                        else if (v.VariableID >= 0 && v.VariableType == GMCode.Bytecode.Instruction.InstanceType.Self)
                            VarCount2++;
                    }
                }
                else
                {
                    VarCount1 = -1;
                    foreach (GMVariable v in List)
                    {
                        if (v.VariableType == GMCode.Bytecode.Instruction.InstanceType.Global || 
                            v.VariableType == GMCode.Bytecode.Instruction.InstanceType.Self)
                            VarCount1 = Math.Max(VarCount1, v.VariableID);
                    }
                    VarCount1 += 1;
                    VarCount2 = VarCount1;
                }
                writer.Write(VarCount1);
                writer.Write(VarCount2);

                // Set MaxLocalVarCount to highest amount of locals within all entires
                MaxLocalVarCount = 0;
                foreach (GMLocalsEntry item in ((GMChunkFUNC)writer.Data.Chunks["FUNC"]).Locals)
                {
                    MaxLocalVarCount = Math.Max(MaxLocalVarCount, item.Entries.Count);
                }

                writer.Write(MaxLocalVarCount);
            }

            foreach (GMVariable variable in List)
            {
                variable.Serialize(writer);
            }
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            if (reader.VersionInfo.FormatID > 14)
            {
                VarCount1 = reader.ReadInt32();
                VarCount2 = reader.ReadInt32();
                MaxLocalVarCount = reader.ReadInt32();

                if (VarCount1 != VarCount2)
                    reader.VersionInfo.DifferentVarCounts = true;
            }

            int varLength = (reader.VersionInfo.FormatID > 14) ? 20 : 12;

            List = new List<GMVariable>();
            while (reader.Offset + varLength <= StartOffset + Length)
            {
                GMVariable gVar = new GMVariable();
                gVar.Deserialize(reader);
                List.Add(gVar);
            }
        }

        public GMVariable FindOrDefine(string name, GMCode.Bytecode.Instruction.InstanceType type, bool builtin, GMData data)
        {
#if DEBUG
            if (type == GMCode.Bytecode.Instruction.InstanceType.Local)
                throw new Exception("Use FindOrDefineLocal for local variables");
#endif

            // Search for an existing variable entry
            foreach (var variable in List)
            {
                if (variable.VariableType == type && variable.Name.Content == name)
                    return variable;
            }

            // No entry was found, so generate a new entry.
            // Starting with its ID:
            int id = 0;
            if (data.VersionInfo.FormatID > 14)
            {
                if (builtin)
                {
                    // All builtin variables have ID -6
                    id = (int)GMCode.Bytecode.Instruction.InstanceType.Builtin;
                }
                else
                {
                    if (data.VersionInfo.DifferentVarCounts)
                    {
                        // Variable counts are different depending on instance/global
                        if (type == GMCode.Bytecode.Instruction.InstanceType.Self)
                            id = VarCount2++;
                        else if (type == GMCode.Bytecode.Instruction.InstanceType.Global)
                            id = VarCount1++;
                    }
                    else
                    {
                        // Variable counts are the same
                        VarCount1++;
                        VarCount2 = VarCount1;
                    }
                }
            }

            // Create the new variable, add to list, and return it
            GMVariable res = new()
            {
                Name = data.DefineString(name),
                VariableType = type,
                VariableID = id
            };
            List.Add(res);
            return res;
        }

        public GMVariable FindOrDefineLocal(string name, int id, GMData data, List<GMVariable> originalReferencedLocals)
        {
            if (data.VersionInfo.FormatID <= 14)
            {
                // Search for existing variable
                foreach (var variable in List)
                {
                    if (variable.VariableType == GMCode.Bytecode.Instruction.InstanceType.Local && variable.Name.Content == name)
                        return variable;
                }
            }

            // Attempt to look for original variable to reuse
            GMVariable original = originalReferencedLocals.Find(v => v.Name.Content == name && v.VariableID == id);
            if (original != null)
                return original;

            // Create a new variable, add to list, and return it
            GMVariable res = new()
            {
                Name = data.DefineString(name),
                VariableType = GMCode.Bytecode.Instruction.InstanceType.Local,
                VariableID = id
            };
            List.Add(res);
            return res;
        }
    }
}
