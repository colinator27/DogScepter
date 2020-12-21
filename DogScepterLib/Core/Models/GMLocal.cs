using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMLocalsEntry : GMSerializable
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

        public void Unserialize(GMDataReader reader)
        {
            Entries = new List<GMLocal>();

            int count = reader.ReadInt32();
            Name = reader.ReadStringPointerObject();

            for (int i = 0; i < count; i++)
            {
                GMLocal local = new GMLocal();
                local.Unserialize(reader);
                Entries.Add(local);
            }
        }
    }

    public class GMLocal : GMSerializable
    {
        public uint Index;
        public GMString Name;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Index);
            writer.WritePointerString(Name);
        }

        public void Unserialize(GMDataReader reader)
        {
            Index = reader.ReadUInt32();
            Name = reader.ReadStringPointerObject();
        }
    }
}
