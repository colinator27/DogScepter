using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a list of local variables, per script entry (Assigned to scripts by having the same Name property).
    /// </summary>
    public class GMLocalsEntry : IGMSerializable
    {
        public GMString Name;
        public List<GMLocal> Entries;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Entries.Count);
            writer.WritePointerString(Name);
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i].Serialize(writer);
            }
        }

        public void Deserialize(GMDataReader reader)
        {
            Entries = new List<GMLocal>();

            int count = reader.ReadInt32();
            Name = reader.ReadStringPointerObject();

            for (int i = 0; i < count; i++)
            {
                GMLocal local = new GMLocal();
                local.Deserialize(reader);
                Entries.Add(local);
            }
        }

        public override string ToString()
        {
            return $"Locals for \"{Name.Content}\"";
        }
    }

    [DebuggerDisplay("{Name.Content}")]
    public class GMLocal : IGMSerializable
    {
        public uint Index;
        public GMString Name;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Index);
            writer.WritePointerString(Name);
        }

        public void Deserialize(GMDataReader reader)
        {
            Index = reader.ReadUInt32();
            Name = reader.ReadStringPointerObject();
        }

        public override string ToString()
        {
            return Name.Content;
        }


    }
}
