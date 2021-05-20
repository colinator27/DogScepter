using DogScepterLib.Core.Chunks;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker sound file.
    /// </summary>
    public class GMSound : GMNamedSerializable
    {
        [Flags]
        public enum AudioEntryFlags : uint
        {
            IsEmbedded = 0x1,
            IsCompressed = 0x2,
            Regular = 0x64,
        }

        public GMString Name { get; set; }
        public AudioEntryFlags Flags;
        public GMString Type;
        public GMString File;
        public uint Effects;
        public float Volume;
        public float Pitch;
        public int AudioID;
        public int GroupID; // In older versions this can also be a "preload" boolean, but it's always true and for now we don't care

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

        public override string ToString()
        {
            return $"Sound: \"{Name.Content}\"";
        }
    }
}
