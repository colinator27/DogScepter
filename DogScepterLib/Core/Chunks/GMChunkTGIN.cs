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

            DoFormatCheck(reader);

            List = new GMUniquePointerList<GMTextureGroupInfo>();
            List.Deserialize(reader);
        }

        private void DoFormatCheck(GMDataReader reader)
        {
            // Do a length check on first entry to see if this is 2022.9
            if (reader.VersionInfo.IsVersionAtLeast(2, 3) && !reader.VersionInfo.IsVersionAtLeast(2022, 9))
            {
                int returnTo = reader.Offset;

                int tginCount = reader.ReadInt32();
                if (tginCount > 0)
                {
                    int tginPtr = reader.ReadInt32();
                    int secondTginPtr = (tginCount >= 2) ? reader.ReadInt32() : EndOffset;
                    reader.Offset = tginPtr + 4;

                    // Check to see if the pointer located at this address points within this object
                    // If not, then we know we're using a new format!
                    int ptr = reader.ReadInt32();
                    if (ptr < tginPtr || ptr >= secondTginPtr)
                    {
                        reader.VersionInfo.SetVersion(2022, 9);
                    }
                }

                reader.Offset = returnTo;
            }

            if (reader.VersionInfo.IsVersionAtLeast(2022, 9) && !reader.VersionInfo.IsVersionAtLeast(2023, 1))
            {
                int returnTo = reader.Offset;
                reader.Offset += 4; // Skip count.

                uint firstPtr = reader.ReadUInt32();

                // Navigate to the fourth list pointer, which is different
                // depending on whether this is 2023.1+ or not (either "FontIDs"
                // or "SpineSpriteIDs").
                reader.Offset = (int)(firstPtr + 16 + (sizeof(uint) * 3));
                uint fourthPtr = reader.ReadUInt32();

                // We read either the "TexturePageIDs" count or the pointer to
                // the fifth list pointer. If it's a count, it will be less
                // than the previous pointer. Similarly, we can rely on the next
                // pointer being greater than the fourth pointer. This lets us
                // safely assume that this is a 2023.1+ file.
                if (reader.ReadUInt32() <= fourthPtr)
                    reader.VersionInfo.SetVersion(2023, 1);

                reader.Offset = returnTo;
            }
        }
    }
}
