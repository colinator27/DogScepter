using DogScepterLib.Core.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Assets
{
    public class AssetSprite : Asset
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int MarginLeft { get; set; }
        public int MarginRight { get; set; }
        public int MarginBottom { get; set; }
        public int MarginTop { get; set; }
        public bool Transparent { get; set; }
        public bool Smooth { get; set; }
        public bool Preload { get; set; }
        public uint BBoxMode { get; set; }
        public GMSprite.SepMaskType SepMasks { get; set; }
        public int OriginX { get; set; }
        public int OriginY { get; set; }
        public SpriteSpecialInfo SpecialInfo { get; set; }

        public string TextureGroup { get; set; } = "";

        public List<GMTextureItem> TextureItems;

        public new static Asset Load(string assetPath)
        {
            byte[] buff = File.ReadAllBytes(assetPath);
            var res = JsonSerializer.Deserialize<AssetBackground>(buff, ProjectFile.JsonOptions);

            string pngPath = Path.Combine(Path.GetDirectoryName(assetPath), res.Name + ".png");
            if (File.Exists(pngPath))
            {
                // Load PNG file, make new texture item for it
                byte[] pngBuff = File.ReadAllBytes(pngPath);
                SKBitmap imgBitmap = SKBitmap.Decode(pngBuff);
                if (imgBitmap.ColorType != SKColorType.Bgra8888 &&
                    imgBitmap.ColorType != SKColorType.Rgba8888)
                    throw new Exception("Expected BGRA8888 or RGBA8888 color format in PNG.");
                byte[] imgBuff = imgBitmap.Bytes;
                res.TextureItem = new GMTextureItem(imgBitmap);

                // Compute hash manually here
                using (SHA1Managed sha1 = new SHA1Managed())
                {
                    sha1.TransformBlock(buff, 0, buff.Length, null, 0);
                    sha1.TransformFinalBlock(imgBuff, 0, imgBuff.Length);
                    res.Length = buff.Length + imgBuff.Length;
                    res.Hash = sha1.Hash;
                }
            }
            else
            {
                // Compute hash manually here, but without image.
                // This isn't intended but we won't error out just yet
                using (SHA1Managed sha1 = new SHA1Managed())
                {
                    sha1.TransformFinalBlock(buff, 0, buff.Length);
                    res.Length = buff.Length;
                    res.Hash = sha1.Hash;
                }
            }

            return res;
        }

        protected override byte[] WriteInternal(ProjectFile pf, string assetPath, bool actuallyWrite)
        {
            byte[] buff = JsonSerializer.SerializeToUtf8Bytes(this, ProjectFile.JsonOptions);

            string dir = null;
            if (actuallyWrite)
            {
                dir = Path.GetDirectoryName(assetPath);
                Directory.CreateDirectory(dir);
                using (FileStream fs = new FileStream(assetPath, FileMode.Create))
                    fs.Write(buff, 0, buff.Length);
            }

            // Compute hash manually here
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                Length = buff.Length;

                // Handle sprite frames
                for (int i = 0; i < TextureItems.Count; i++)
                {
                    GMTextureItem item = TextureItems[i];

                    byte[] imgBuff;
                    SKBitmap imgBitmap;
                    if (item.TexturePageID == -1)
                        imgBitmap = item._Bitmap;
                    else
                        imgBitmap = pf.Textures.GetTextureEntryBitmap(item, true);
                    imgBuff = imgBitmap.Bytes;

                    if (actuallyWrite)
                    {
                        byte[] pngBuff = imgBitmap.Encode(SKEncodedImageFormat.Png, 0).ToArray();
                        using (FileStream fs = new FileStream(Path.Combine(dir, Name + "_" + i.ToString() + ".png"), FileMode.Create))
                            fs.Write(pngBuff, 0, pngBuff.Length);
                    }

                    Length += imgBuff.Length;
                    sha1.TransformBlock(imgBuff, 0, imgBuff.Length, null, 0);
                }

                if (SpecialInfo != null)
                {
                    if (SpecialInfo.InternalBuffer != null &&
                        SpecialInfo.Buffer != null)
                    {
                        if (actuallyWrite)
                        {
                            using (FileStream fs = new FileStream(Path.Combine(dir, "buffer.bin"), FileMode.Create))
                                fs.Write(SpecialInfo.InternalBuffer, 0, SpecialInfo.InternalBuffer.Length);
                        }

                        Length += SpecialInfo.InternalBuffer.Length;
                        sha1.TransformBlock(SpecialInfo.InternalBuffer, 0, SpecialInfo.InternalBuffer.Length, null, 0);
                    }
                }    

                // TODO: Handle sequence

                sha1.TransformFinalBlock(buff, 0, buff.Length);
                Hash = sha1.Hash;
            }
            return null;
        }

        public override void Delete(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        public class SpriteSpecialInfo
        {
            public GMSprite.SpriteType SpriteType { get; set; }
            public string Buffer { get; set; } = null; // filename of buffer in this asset's folder
            public byte[] InternalBuffer = null;

            public float GMS2PlaybackSpeed { get; set; }
            public GMSprite.AnimSpeedType GMS2PlaybackSpeedType { get; set; }

            public string Sequence { get; set; } = null; // filename of sequence asset in this asset's folder
            public NineSliceInfo NineSlice { get; set; } = null;

            public class NineSliceInfo
            {
                public int Left { get; set; }
                public int Top { get; set; }
                public int Right { get; set; }
                public int Bottom { get; set; }
                public bool Enabled { get; set; }
                public List<GMSprite.NineSlice.TileMode> TileModes { get; set; }
            }
        }
    }
}
