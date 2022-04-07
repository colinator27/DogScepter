using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkBGND : GMChunk
    {
        public GMUniquePointerList<GMBackground> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer, (writer, i, count) =>
            {
                // Align to 8 byte offsets if necessary
                if (writer.VersionInfo.AlignBackgroundsTo8)
                    writer.Pad(8);
            });
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            List = new GMUniquePointerList<GMBackground>();
            reader.VersionInfo.AlignBackgroundsTo8 = reader.VersionInfo.IsNumberAtLeast(2, 3); // only occurs on newer 2.3.1 versions
            List.Unserialize(reader, null, null, (GMDataReader reader, bool notLast) =>
            {
                int ptr = reader.ReadInt32();

                // Check if backgrounds are aligned to 8 byte offsets
                reader.VersionInfo.AlignBackgroundsTo8 &= (ptr % 8 == 0);

                return reader.ReadPointerObject<GMBackground>(ptr, notLast);
            });
        }
    }
}
