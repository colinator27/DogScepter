using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkEXTN : GMChunk
    {
        public GMUniquePointerList<GMExtension> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);

            foreach (GMExtension e in List)
            {
                if (e.ProductID != null)
                    writer.Write(e.ProductID?.ToByteArray());
            }
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            DoFormatCheck(reader);

            List = new GMUniquePointerList<GMExtension>();
            List.Deserialize(reader);

            // Product ID information for each extension
            if (reader.VersionInfo.IsVersionAtLeast(1, 0, 0, 9999))
            {
                for (int i = 0; i < List.Count; i++)
                    List[i].ProductID = new Guid(reader.ReadBytes(16).Memory.ToArray());
            }
        }

        private void DoFormatCheck(GMDataReader reader)
        {
            if (reader.VersionInfo.IsVersionAtLeast(2, 3) && !reader.VersionInfo.IsVersionAtLeast(2022, 6))
            {
                // Check for 2022.6, if possible
                bool definitely2022_6 = true;
                int returnTo = reader.Offset;

                int extCount = reader.ReadInt32();
                if (extCount > 0)
                {
                    int firstExtPtr = reader.ReadInt32();
                    int firstExtEndPtr = (extCount >= 2) ? reader.ReadInt32() /* second ptr */ : EndOffset;

                    reader.Offset = firstExtPtr + 12;
                    int newPointer1 = reader.ReadInt32();
                    int newPointer2 = reader.ReadInt32();

                    if (newPointer1 != reader.Offset)
                        definitely2022_6 = false; // first pointer mismatch
                    else if (newPointer2 <= reader.Offset || newPointer2 >= EndOffset)
                        definitely2022_6 = false; // second pointer out of bounds
                    else
                    {
                        // Check ending position
                        reader.Offset = newPointer2;
                        int optionCount = reader.ReadInt32();
                        if (optionCount > 0)
                        {
                            reader.Offset += 4 * (optionCount - 1);
                            reader.Offset = reader.ReadInt32() + 12; // jump past last option
                        }
                        if (extCount == 1)
                        {
                            reader.Offset += 16; // skip GUID data (only one of them)
                            reader.Pad(16); // align to chunk end
                        }
                        if (reader.Offset != firstExtEndPtr)
                            definitely2022_6 = false;
                    }
                }
                else
                    definitely2022_6 = false;

                reader.Offset = returnTo;

                if (definitely2022_6)
                    reader.VersionInfo.SetVersion(2022, 6);
            }
        }
    }
}
