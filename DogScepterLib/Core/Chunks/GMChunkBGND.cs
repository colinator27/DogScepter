using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkBGND : GMChunk
    {
        public GMPointerList<GMBackground> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            List = new GMPointerList<GMBackground>();
            List.Unserialize(reader);
        }
    }
}
