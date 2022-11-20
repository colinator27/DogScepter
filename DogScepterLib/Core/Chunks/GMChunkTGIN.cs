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
        }
    }
}
