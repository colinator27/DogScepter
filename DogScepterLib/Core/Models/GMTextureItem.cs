using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMTextureItem : GMSerializable
    {
        /*
         * The way this works is:
         * It renders in a box of size BoundWidth x BoundHeight at some position.
         * TargetX/Y/W/H is relative to the bounding box, anything outside of that is just transparent.
         * SourceX/Y/W/H is part of TexturePage that is drawn over TargetX/Y/W/H
         */

        public ushort SourceX; // The position in the texture sheet.
        public ushort SourceY;
        public ushort SourceWidth; // The dimensions of the image in the texture sheet.
        public ushort SourceHeight;

        public ushort TargetX; // The offset of the image, to account for trimmed 
        public ushort TargetY;
        public ushort TargetWidth; // The dimensions to scale the image to. (Is this BoundingWidth - TargetX)?
        public ushort TargetHeight;

        public ushort BoundWidth; // The image's dimensions.
        public ushort BoundHeight;

        public short TexturePageID;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(SourceX);
            writer.Write(SourceY);
            writer.Write(SourceWidth);
            writer.Write(SourceHeight);
            writer.Write(TargetX);
            writer.Write(TargetY);
            writer.Write(TargetWidth);
            writer.Write(TargetHeight);
            writer.Write(BoundWidth);
            writer.Write(BoundHeight);
            writer.Write(TexturePageID);
        }

        public void Unserialize(GMDataReader reader)
        {
            SourceX = reader.ReadUInt16();
            SourceY = reader.ReadUInt16();
            SourceWidth = reader.ReadUInt16();
            SourceHeight = reader.ReadUInt16();
            TargetX = reader.ReadUInt16();
            TargetY = reader.ReadUInt16();
            TargetWidth = reader.ReadUInt16();
            TargetHeight = reader.ReadUInt16();
            BoundWidth = reader.ReadUInt16();
            BoundHeight = reader.ReadUInt16();
            TexturePageID = reader.ReadInt16();
        }
    }
}
