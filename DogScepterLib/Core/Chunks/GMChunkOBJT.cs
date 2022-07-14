using System;
using System.Collections.Generic;
using System.Text;
using DogScepterLib.Core.Models;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkOBJT : GMChunk
    {
        public GMUniquePointerList<GMObject> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            DoFormatCheck(reader);

            List = new GMUniquePointerList<GMObject>();
            List.Deserialize(reader);
        }

        private void DoFormatCheck(GMDataReader reader)
        {
            // Do a length check on first object to see if 2022.5+
            if (reader.VersionInfo.IsVersionAtLeast(2, 3) && !reader.VersionInfo.IsVersionAtLeast(2022, 5))
            {
                int returnTo = reader.Offset;

                int objectCount = reader.ReadInt32();
                if (objectCount > 0)
                {
                    int firstObjectPtr = reader.ReadInt32();
                    reader.Offset = firstObjectPtr + 64;

                    int vertexCount = reader.ReadInt32();
                    int jumpAmount = 12 + (vertexCount * 8);

                    if (reader.Offset + jumpAmount >= EndOffset || jumpAmount < 0)
                    {
                        // Failed bounds check; 2022.5+
                        reader.VersionInfo.SetVersion(2022, 5);
                    }
                    else
                    {
                        // Jump ahead to the rest of the data
                        reader.Offset += jumpAmount;
                        int eventCount = reader.ReadInt32();
                        if (eventCount != 15)
                        {
                            // Failed event list count check; 2022.5+
                            reader.VersionInfo.SetVersion(2022, 5);
                        }
                        else
                        {
                            int firstEventPtr = reader.ReadInt32();
                            if (reader.Offset + 56 != firstEventPtr)
                            {
                                // Failed first event pointer check (should be right after pointers); 2022.5+
                                reader.VersionInfo.SetVersion(2022, 5);
                            }
                        }
                    }
                }

                reader.Offset = returnTo;
            }
        }
    }
}
