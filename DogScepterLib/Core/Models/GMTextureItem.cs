using DogScepterLib.Project;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker texture item.
    /// A texture item is the location and size of a single graphic within a texture page.
    /// </summary>
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
        public ushort TargetWidth; // The dimensions to scale the image to.
        public ushort TargetHeight;

        public ushort BoundWidth; // The image's dimensions.
        public ushort BoundHeight;

        public short TexturePageID = -1; // -1 means this is a user-created item

        // Used for convenience in the project system primarily
        public bool _TileHorizontally = false;
        public bool _TileVertically = false;
        public bool _HasExtraBorder = false;
        public bool _EmptyBorder = false;
        public GMTextureItem _DuplicateOf = null;
        public TexturePacker.Page.Item _PackItem = null;
        public SKBitmap _Bitmap = null;

        public GMTextureItem()
        {
        }

        // Creates a new texture entry from a bitmap
        public GMTextureItem(SKBitmap bitmap)
        {
            _Bitmap = bitmap;

#if DEBUG
            Debug.Assert(bitmap.ColorType == SKColorType.Rgba8888 || bitmap.ColorType == SKColorType.Bgra8888, "expected 32-bit rgba/bgra");
            Debug.Assert(bitmap.RowBytes % 4 == 0, "expected bytes per row to be divisible by 4");
#endif

            BoundWidth = (ushort)bitmap.Width;
            BoundHeight = (ushort)bitmap.Height;
            SourceX = 0;
            SourceY = 0;
            SourceWidth = BoundWidth;
            SourceHeight = BoundHeight;
            TargetX = 0;
            TargetY = 0;
            TargetWidth = BoundWidth;
            TargetHeight = BoundHeight;
        }

        public unsafe void Crop()
        {
            int left = BoundWidth, top = BoundHeight, right = 0, bottom = 0; 
            
            int stride = (_Bitmap.RowBytes / 4);
            int* ptr = (int*)_Bitmap.GetPixels().ToPointer();
            int* basePtr = ptr;

            for (int y = 0; y < BoundHeight; y++)
            {
                for (int x = 0; x < BoundWidth; x++)
                {
                    if (*((byte*)ptr + 3) != 0)
                    {
                        if (x < left)
                            left = x;
                        if (y < top)
                            top = y;
                        if (x > right)
                            right = x;
                        if (y > bottom)
                            bottom = y;
                    }
                    ptr++;
                }
                ptr += stride - BoundWidth;
            }

            if (left == BoundWidth && top == BoundHeight)
            {
                // This is fully transparent, just grab one pixel
                SourceWidth = 1;
                SourceHeight = 1;
            } else if (left != 0 || top != 0 || right != BoundWidth - 1 || bottom != BoundHeight - 1)
            {
                // We can crop this image
                right++;
                bottom++;
                SourceWidth = (ushort)(right - left);
                SourceHeight = (ushort)(bottom - top);
                TargetWidth = SourceWidth;
                TargetHeight = SourceHeight;
                TargetX = (ushort)left;
                TargetY = (ushort)top;
                _Bitmap.ExtractSubset(_Bitmap, new SKRectI(left, top, right, bottom));
            }
        }

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
