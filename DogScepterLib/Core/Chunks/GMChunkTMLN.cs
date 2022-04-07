using System;
using System.Collections.Generic;
using System.Text;
using DogScepterLib.Core.Models;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkTMLN : GMChunk
    {
        public GMUniquePointerList<GMTimeline> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            List = new GMUniquePointerList<GMTimeline>();
            List.Deserialize(reader);
        }
    }
}
