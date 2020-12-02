using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkSTRG : GMChunk
    {
        public GMPointerList<GMString> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer, (writer) =>
            {
                // Align to 4 byte offsets if necessary
                if (writer.VersionInfo.AlignStringsTo4)
                    writer.Pad(4);
            });
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            List = new GMPointerList<GMString>();
            List.Unserialize(reader, (reader) => 
            {
                // Check if strings are aligned to 4 byte offsets
                reader.VersionInfo.AlignStringsTo4 &= (reader.Offset % 4 == 0);
            });
        }
    }
}
