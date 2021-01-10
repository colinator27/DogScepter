using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMScript : GMSerializable
    {
        public GMString Name;
        public int CodeID;
        public bool Constructor;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            if (Constructor)
                writer.Write((uint)CodeID | 2147483648u);
            else
                writer.Write(CodeID);
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            CodeID = reader.ReadInt32();
            if (CodeID < -1)
            {
                // New GMS 2.3 constructor scripts
                Constructor = true;
                CodeID = (int)((uint)CodeID & 2147483647u);
            }
        }
    }
}
