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

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

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
                gVar.Unserialize(reader);
                List.Add(gVar);
            }
        }
    }
}
