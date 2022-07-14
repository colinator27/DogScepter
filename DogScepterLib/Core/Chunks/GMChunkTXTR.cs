using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkTXTR : GMChunk
    {
        public GMUniquePointerList<GMTexturePage> List;
        public bool Checked2022_5 = false;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);
            foreach (GMTexturePage tpe in List)
                tpe.TextureData.Serialize(writer);

            writer.Pad(4);
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            DoFormatCheck(reader);

            // If on an earlier version than 2022.3, don't bother checking 2022.5
            Checked2022_5 = !reader.VersionInfo.IsVersionAtLeast(2022, 3);

            List = new GMUniquePointerList<GMTexturePage>();
            List.Deserialize(reader);
        }

        private static void DoFormatCheck(GMDataReader reader)
        {
            // Perform checks to see if this is 2022.3 or higher
            if (reader.VersionInfo.IsVersionAtLeast(2, 3) && !reader.VersionInfo.IsVersionAtLeast(2022, 3))
            {
                int returnPos = reader.Offset;

                int textureCount = reader.ReadInt32();
                if (textureCount == 1)
                {
                    // If there isn't a 0 after the first texture, then this is 2022.3
                    // (the pointer was shifted back by 4 bytes, where alignment padding used to always be)
                    reader.Offset += 16;
                    if (reader.ReadInt32() != 0)
                        reader.VersionInfo.SetVersion(2022, 3);
                }
                else if (textureCount >= 2)
                {
                    // If the difference between the first two pointers is 16, then this is 2022.3
                    int first = reader.ReadInt32();
                    int second = reader.ReadInt32();
                    if (second - first == 16)
                        reader.VersionInfo.SetVersion(2022, 3);
                }

                reader.Offset = returnPos;
            }
        }
    }
}
