using DogScepterLib.Core.Chunks;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains information about a GameMaker variable.
    /// </summary>
    public class GMVariable : IGMSerializable
    {
        public GMString Name;
        public GMCode.Bytecode.Instruction.InstanceType VariableType;
        public int VariableID;
        public int Occurrences;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);

            if (writer.VersionInfo.FormatID > 14)
            {
                writer.Write((int)VariableType);
                writer.Write(VariableID);
            }

            List<(int, GMCode.Bytecode.Instruction.VariableType)> references;
            if (writer.VariableReferences.TryGetValue(this, out references))
                Occurrences = references.Count;
            else
                Occurrences = 0;

            writer.Write(Occurrences);
            if (Occurrences > 0)
            {
                writer.Write(references[0].Item1);

                int returnTo = writer.Offset;
                for (int i = 0; i < references.Count; i++)
                {
                    int curr = references[i].Item1;

                    int nextDiff;
                    if (i < references.Count - 1)
                        nextDiff = references[i + 1].Item1 - curr;
                    else
                        nextDiff = ((GMChunkSTRG)writer.Data.Chunks["STRG"]).List.IndexOf(Name);

                    writer.Offset = curr + 4;
                    writer.Write((nextDiff & 0x07FFFFFF) | (((int)references[i].Item2 & 0xF8) << 24));
                }
                writer.Offset = returnTo;
            }
            else
                writer.Write((int)-1);
        }

        public void Deserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();

            if (reader.VersionInfo.FormatID > 14)
            {
                VariableType = (GMCode.Bytecode.Instruction.InstanceType)reader.ReadInt32();

                // Handle max struct ID detection (for GML compilation in 2.3)
                if (VariableType == GMCode.Bytecode.Instruction.InstanceType.Static)
                {
                    if (Name.Content.StartsWith("___struct___"))
                    {
                        if (int.TryParse(Name.Content[12..], out int id))
                        {
                            if (id > reader.Data.Stats.LastStructID)
                                reader.Data.Stats.LastStructID = id;
                        }
                    }
                }

                VariableID = reader.ReadInt32();
            }
            Occurrences = reader.ReadInt32();
            if (Occurrences > 0)
            {
                int addr = reader.ReadInt32();

                // Parse reference chain
                GMCode.Bytecode.Instruction curr;
                for (int i = Occurrences; i > 0; i--)
                {
                    curr = reader.Instructions[addr];
                    curr.Variable.Target = this;
                    addr += curr.Variable.NextOccurrence;
                }
            }
            else
            {
                if (reader.ReadInt32() != -1)
                    reader.Warnings.Add(new GMWarning("Variable with no occurrences, but still has a first occurrence address"));
            }
        }

        public override string ToString()
        {
            return $"Variable: \"{Name.Content}\"";
        }
    }
}
