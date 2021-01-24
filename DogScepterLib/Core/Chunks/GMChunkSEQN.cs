using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkSEQN : GMChunk
    {
        public GMPointerList<GMSequence> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Pad(4);
            writer.Write((uint)1);

            List.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            reader.Pad(4);
            if (reader.ReadUInt32() != 1)
                reader.Warnings.Add(new GMWarning("Unknown SEQN version, expected 1."));

            List = new GMPointerList<GMSequence>();
            List.Unserialize(reader);
        }
    }
}
