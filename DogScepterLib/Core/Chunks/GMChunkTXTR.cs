using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkTXTR : GMChunk
    {
        public GMUniquePointerList<GMTexturePage> List;

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

            List = new GMUniquePointerList<GMTexturePage>();
            List.Deserialize(reader);
        }

        private static void DoFormatCheck(GMDataReader reader)
        {
            // Perform several version checks
            if (reader.VersionInfo.IsVersionAtLeast(2, 3))
            {
                int returnPos = reader.Offset;

                if (!reader.VersionInfo.IsVersionAtLeast(2022, 3))
                {
                    // Check for 2022.3+
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
                }

                if (reader.VersionInfo.IsVersionAtLeast(2022, 3) && !reader.VersionInfo.IsVersionAtLeast(2022, 5))
                {
                    // Check for 2022.5+ by looking for discrepancies in bz2 format
                    reader.Offset = returnPos;

                    int textureCount = reader.ReadInt32();
                    for (int i = 0; i < textureCount; i++)
                    {
                        // Go to each texture, and then to each texture's data
                        reader.Offset = returnPos + 4 + (i * 4);
                        reader.Offset = reader.ReadInt32() + 12; // go to texture, at an offset
                        reader.Offset = reader.ReadInt32(); // go to texture data
                        byte[] header = reader.ReadBytes(4).Memory.ToArray();
                        if (header.SequenceEqual(GMTextureData.QOIandBZip2Header))
                        {
                            reader.Offset += 4; // skip width/height

                            // Now check the actual bz2 headers
                            if (reader.ReadByte() != (byte)'B')
                                reader.VersionInfo.SetVersion(2022, 5);
                            else if (reader.ReadByte() != (byte)'Z')
                                reader.VersionInfo.SetVersion(2022, 5);
                            else if (reader.ReadByte() != (byte)'h')
                                reader.VersionInfo.SetVersion(2022, 5);
                            else
                            {
                                reader.ReadByte();
                                if (reader.ReadUInt24() != 0x594131) // digits of pi... (block header)
                                    reader.VersionInfo.SetVersion(2022, 5);
                                else if (reader.ReadUInt24() != 0x595326)
                                    reader.VersionInfo.SetVersion(2022, 5);
                            }

                            // Checked one QOI+BZ2 texture. No need to check any more
                            break;
                        }
                    }
                }

                reader.Offset = returnPos;
            }
        }
    }
}
