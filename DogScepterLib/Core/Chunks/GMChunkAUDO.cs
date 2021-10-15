using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkAUDO : GMChunk
    {
        public GMUniquePointerList<GMAudio> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer, (writer, i, count) =>
            {
                // Align each entry to 4 bytes
                writer.Pad(4);
            });
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            List = new GMUniquePointerList<GMAudio>();
            List.Unserialize(reader);
        }
    }
}
