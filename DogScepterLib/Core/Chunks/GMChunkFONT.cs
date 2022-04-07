using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkFONT : GMChunk
    {
        public GMUniquePointerList<GMFont> List;

        public BufferRegion Padding;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);

            if (Padding == null)
            {
                for (ushort i = 0; i < 0x80; i++)
                    writer.Write(i);
                for (ushort i = 0; i < 0x80; i++)
                    writer.Write((ushort)0x3f);
            }
            else
                writer.Write(Padding);
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            DoFormatCheck(reader);

            List = new GMUniquePointerList<GMFont>();
            List.Deserialize(reader);

            Padding = reader.ReadBytes(512);
        }

        private void DoFormatCheck(GMDataReader reader)
        {
            // Check for new "Ascender" field introduced in 2022.2, by attempting to parse old font data format
            if (reader.VersionInfo.IsNumberAtLeast(2, 3) && !reader.VersionInfo.IsNumberAtLeast(2022, 2))
            {
                int returnTo = reader.Offset;

                int fontCount = reader.ReadInt32();
                if (fontCount > 0)
                {
                    int lowerBound = reader.Offset + (fontCount * 4);
                    int upperBound = EndOffset - 512;

                    int firstFontPtr = reader.ReadInt32();
                    int endPtr = (fontCount >= 2 ? reader.ReadInt32() : upperBound);

                    reader.Offset = firstFontPtr + (11 * 4);

                    int glyphCount = reader.ReadInt32();
                    bool invalidFormat = false;
                    if (glyphCount > 0)
                    {
                        int glyphPtrOffset = reader.Offset;

                        if (glyphCount >= 2)
                        {
                            // Check validity of first glyph
                            int firstGlyph = reader.ReadInt32() + (7 * 2);
                            int secondGlyph = reader.ReadInt32();
                            if (firstGlyph < lowerBound || firstGlyph > upperBound ||
                                secondGlyph < lowerBound || secondGlyph > upperBound)
                            {
                                invalidFormat = true;
                            }

                            if (!invalidFormat)
                            {
                                // Check the length of the end of this glyph
                                reader.Offset = firstGlyph;
                                int kerningLength = (reader.ReadUInt16() * 4);
                                reader.Offset += kerningLength;

                                if (reader.Offset != secondGlyph)
                                    invalidFormat = true;
                            }
                        }

                        if (!invalidFormat)
                        {
                            // Check last glyph
                            reader.Offset = glyphPtrOffset + ((glyphCount - 1) * 4);

                            int lastGlyph = reader.ReadInt32() + (7 * 2);
                            if (lastGlyph < lowerBound || lastGlyph > upperBound)
                                invalidFormat = true;
                            if (!invalidFormat)
                            {
                                // Check the length of the end of this glyph (done when checking endPtr below)
                                reader.Offset = lastGlyph;
                                int kerningLength = (reader.ReadUInt16() * 4);
                                reader.Offset += kerningLength;
                            }
                        }
                    }

                    if (invalidFormat || reader.Offset != endPtr)
                    {
                        // We didn't end up where we expected! This is most likely 2022.2+ font data
                        reader.VersionInfo.SetNumber(2022, 2);
                    }
                }

                reader.Offset = returnTo;
            }
        }
    }
}
