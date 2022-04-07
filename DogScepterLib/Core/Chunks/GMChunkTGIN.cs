using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkTGIN : GMChunk
    {
        public GMUniquePointerList<GMTextureGroupInfo> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(1);

            List.Serialize(writer);
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            int chunkVersion = reader.ReadInt32();
            if (chunkVersion != 1)
                reader.Warnings.Add(new GMWarning($"TGIN version is {chunkVersion}, expected 1"));

            List = new GMUniquePointerList<GMTextureGroupInfo>();
            List.Deserialize(reader);
        }
    }
}
