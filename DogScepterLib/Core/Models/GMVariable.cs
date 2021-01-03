using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    class GMVariable : GMSerializable
    {
        public enum VariableTypeEnum : short
        {
            Self = -1,
            Global = -5,
            NotSpecified = -6,
            Local = -7,
            Static = -16
        }

        public GMString Name;
        public VariableTypeEnum VariableType;
        public int VariableID;
        public uint Occurrences;
        public uint FirstAddress; // TODO: Should be a bytecode instruction

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);

            if (writer.VersionInfo.FormatID > 14)
            {
                writer.Write((int)VariableType);
                writer.Write(VariableID);
            }
            writer.Write(Occurrences);
            if (Occurrences > 0)
                writer.Write(FirstAddress);
            else
                writer.Write((int)-1);
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();

            if (reader.VersionInfo.FormatID > 14)
            {
                VariableType = (VariableTypeEnum)reader.ReadInt32();
                VariableID = reader.ReadInt32();
            }
            Occurrences = reader.ReadUInt32();
            if (Occurrences > 0)
            {
                FirstAddress = reader.ReadUInt32();
            }
            else
            {
                if (reader.ReadInt32() != -1)
                    reader.Warnings.Add(new GMWarning("Variable with no occurrences, but still has a first occurrence address"));
                FirstAddress = 0;
            }
        }
    }
}
