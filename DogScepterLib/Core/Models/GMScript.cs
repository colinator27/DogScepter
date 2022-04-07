using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker script.
    /// </summary>
    public class GMScript : IGMSerializable
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

        public void Deserialize(GMDataReader reader)
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

        public override string ToString()
        {
            return $"Script: \"{Name.Content}\"";
        }
    }
}
