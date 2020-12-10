using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkTGIN : GMChunk
    {
        public GMPointerList<GMTextureGroupInfo> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(1);

            List.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            if (reader.ReadInt32() != 1)
                reader.Warnings.Add(new GMWarning("Unexpected TGIN version, != 1", GMWarning.WarningLevel.Severe));

            List = new GMPointerList<GMTextureGroupInfo>();
            List.Unserialize(reader);
        }
    }
}
