using DogScepterLib.Core.Models;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DogScepterLib.Project.Util;
using DogScepterLib.Core;

namespace DogScepterLib.Project.Assets
{
    public class AssetSprite : Asset
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Transparent { get; set; }
        public bool Smooth { get; set; }
        public bool Preload { get; set; }
        public int OriginX { get; set; }
        public int OriginY { get; set; }
        public string TextureGroup { get; set; } = "";
        public bool SeparateTextureGroup { get; set; } = false;
        public CollisionMaskInfo CollisionMask { get; set; }
        public SpriteSpecialInfo SpecialInfo { get; set; }


        public List<GMTextureItem> TextureItems = new List<GMTextureItem>();

        public new static Asset Load(string assetPath)
        {
            byte[] buff = File.ReadAllBytes(assetPath);
            var res = JsonSerializer.Deserialize<AssetSprite>(buff, ProjectFile.JsonOptions);

            string dir = Path.GetDirectoryName(assetPath);

            using (var sha1 = SHA1.Create())
            {
                res.Length = buff.Length;

                int ind = 0;
                string basePath = Path.Combine(dir, res.Name);
                string pngPath = basePath + "_" + ind.ToString() + ".png";
                while (File.Exists(pngPath))
                {
                    // Load PNG file, make new texture item for it
                    DSImage img = new DSImage(pngPath);
                    res.TextureItems.Add(new GMTextureItem(img));

                    sha1.TransformBlock(img.Data, 0, img.Data.Length, null, 0);
                    res.Length += img.Data.Length;

                    pngPath = basePath + "_" + (++ind).ToString() + ".png";
                }

                // Load special info buffer
                if (res.SpecialInfo?.Buffer != null)
                {
                    string path = Path.Combine(dir, res.SpecialInfo.Buffer);
                    if (File.Exists(path))
                    {
                        byte[] specialBuff = File.ReadAllBytes(path);
                        res.SpecialInfo.InternalBuffer = new BufferRegion(specialBuff);

                        sha1.TransformBlock(specialBuff, 0, specialBuff.Length, null, 0);
                        res.Length += specialBuff.Length;
                    }
                }

                // Load raw collision masks
                if ((int)res.CollisionMask.Mode < 0)
                {
                    res.CollisionMask.RawMasks = new List<BufferRegion>();

                    ind = 0;
                    pngPath = basePath + "_mask_" + ind.ToString() + ".png";
                    while (File.Exists(pngPath))
                    {
                        byte[] mask = CollisionMasks.GetMaskFromImage(new DSImage(pngPath));
                        res.CollisionMask.RawMasks.Add(new BufferRegion(mask));

                        sha1.TransformBlock(mask, 0, mask.Length, null, 0);
                        res.Length += mask.Length;

                        pngPath = basePath + "_mask_" + (++ind).ToString() + ".png";
                    }
                }

                sha1.TransformFinalBlock(buff, 0, buff.Length);
                res.Hash = sha1.Hash;
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
            using (var sha1 = SHA1.Create())
            {
                Length = buff.Length;

                // Handle sprite frames
                for (int i = 0; i < TextureItems.Count; i++)
                {
                    GMTextureItem item = TextureItems[i];

                    DSImage img;
                    if (item.TexturePageID == -1)
                        img = item._Image;
                    else
                        img = pf.Textures.GetTextureEntryImage(item, Width, Height);

                    if (actuallyWrite)
                    {
                        using FileStream fs = new FileStream(Path.Combine(dir, Name + "_" + i.ToString() + ".png"), FileMode.Create);
                        img.SavePng(fs);
                    }

                    Length += img.Data.Length;
                    sha1.TransformBlock(img.Data, 0, img.Data.Length, null, 0);
                }

                if (SpecialInfo != null)
                {
                    if (SpecialInfo.InternalBuffer != null &&
                        SpecialInfo.Buffer != null)
                    {
                        byte[] internalBufferArray = SpecialInfo.InternalBuffer.Memory.ToArray();

                        if (actuallyWrite)
                        {
                            using FileStream fs = new FileStream(Path.Combine(dir, SpecialInfo.Buffer), FileMode.Create);
                            fs.Write(internalBufferArray, 0, internalBufferArray.Length);
                        }

                        Length += internalBufferArray.Length;
                        sha1.TransformBlock(internalBufferArray, 0, internalBufferArray.Length, null, 0);
                    }
                }

                // Save raw collision masks
                if (CollisionMask.RawMasks?.Count > 0)
                {
                    for (int i = 0; i < CollisionMask.RawMasks.Count; i++)
                    {
                        byte[] mask = CollisionMask.RawMasks[i].Memory.ToArray();

                        if (actuallyWrite)
                        {
                            using FileStream fs = new FileStream(Path.Combine(dir, Name + "_mask_" + i.ToString() + ".png"), FileMode.Create);
                            CollisionMasks.GetImageFromMask(Width, Height, mask).SavePng(fs);
                        }

                        Length += mask.Length;
                        sha1.TransformBlock(mask, 0, mask.Length, null, 0);
                    }
                }

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

        public class CollisionMaskInfo
        {
            public enum MaskMode : int
            {
                Automatic = 0,
                FullImage = 1,
                Manual = 2,

                RawAutomatic = -1,
                RawFullImage = -2,
                RawManual = -3,
            }

            public enum MaskType
            {
                Rectangle,
                RectangleWithRotation,
                Ellipse,
                Diamond,
                Precise,
                PrecisePerFrame
            }

            public MaskMode Mode { get; set; }
            public MaskType Type { get; set; } = MaskType.Rectangle;
            public byte? AlphaTolerance { get; set; }
            public int? Left { get; set; }
            public int? Right { get; set; }
            public int? Top { get; set; }
            public int? Bottom { get; set; }

            public List<BufferRegion> RawMasks;
        }

        public class SpriteSpecialInfo
        {
            public GMSprite.SpriteType SpriteType { get; set; }
            public string Buffer { get; set; } = null; // filename of buffer in this asset's folder
            public BufferRegion InternalBuffer = null;

            public float? GMS2PlaybackSpeed { get; set; } = null;
            public GMSprite.AnimSpeedType? GMS2PlaybackSpeedType { get; set; } = null;

            public SequenceInfo Sequence { get; set; } = null;
            public NineSliceInfo NineSlice { get; set; } = null;

            public class SequenceInfo
            {
                public string Name { get; set; }
                public List<Frame> Frames { get; set; }
                public List<BroadcastMessage> BroadcastMessages { get; set; }

                public class BroadcastMessage
                {
                    public float Position { get; set; }
                    public string Message { get; set; }
                }

                public class Frame
                {
                    public float Position { get; set; }
                    public float Length { get; set; }
                    public int Index { get; set; }
                }
            }

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
