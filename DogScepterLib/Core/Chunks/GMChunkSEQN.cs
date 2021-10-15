using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkSEQN : GMChunk
    {
        public GMUniquePointerList<GMSequence> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            if (List == null)
                return;

            writer.Write((uint)1);

            List.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);
            
            // This chunk can just be empty sometimes
            if (Length == 0)
                return;

            int chunkVersion = reader.ReadInt32();
            if (chunkVersion != 1)
                reader.Warnings.Add(new GMWarning($"SEQN version is {chunkVersion}, expected 1"));

            List = new GMUniquePointerList<GMSequence>();
            List.Unserialize(reader);
        }
    }
}
