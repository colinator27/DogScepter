using DogScepterLib.Core.Chunks;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMSound : GMSerializable
    {
        [Flags]
        public enum AudioEntryFlags : uint
        {
            IsEmbedded = 0x1,
            IsCompressed = 0x2,
            Regular = 0x64,
        }

        public GMString Name;
        public AudioEntryFlags Flags;
        public GMString Type;
        public GMString File;
        public uint Effects;
        public float Volume;
        public float Pitch;
        public int AudioID;
        public int GroupID;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.Write((uint)Flags);
            writer.WritePointerString(Type);
            writer.WritePointerString(File);
            writer.Write(Effects);
            writer.Write(Volume);
            writer.Write(Pitch);
            writer.Write(GroupID);
            writer.Write(AudioID);
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            Flags = (AudioEntryFlags)reader.ReadUInt32();
            Type = reader.ReadStringPointerObject();
            File = reader.ReadStringPointerObject();
            Effects = reader.ReadUInt32();
            Volume = reader.ReadSingle();
            Pitch = reader.ReadSingle();
            GroupID = reader.ReadInt32();
            AudioID = reader.ReadInt32();
        }
    }
}
