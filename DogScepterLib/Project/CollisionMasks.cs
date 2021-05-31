using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using DogScepterLib.Project.Util;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Project.Assets.AssetSprite.CollisionMaskInfo;

namespace DogScepterLib.Project
{
    public static class CollisionMasks
    {
        private static byte GetHighestAlphaAt(List<(byte[], int)> items, int x, int y)
        {
            byte highest = 0;

            foreach (var item in items)
            {
                byte alpha = item.Item1[(x * 4) + (y * item.Item2) + 3];
                if (alpha > highest)
                    highest = alpha;
            }

            return highest;
        }

        public static AssetSprite.CollisionMaskInfo GetInfoForSprite(ProjectFile pf, GMSprite spr, out List<SKBitmap> bitmaps, bool suggestPrecise = false)
        {
            bitmaps = new List<SKBitmap>(spr.TextureItems.Count);

            var info = new AssetSprite.CollisionMaskInfo
            {
                Mode = (MaskMode)spr.BBoxMode
            };

            if (spr.SepMasks == GMSprite.SepMaskType.AxisAlignedRect)
                info.Type = MaskType.Rectangle;
            else if (spr.SepMasks == GMSprite.SepMaskType.RotatedRect)
                info.Type = MaskType.RectangleWithRotation;

            // Some basic conditions to bail
            if (spr.CollisionMasks.Count != 1 && spr.CollisionMasks.Count != spr.TextureItems.Count)
                return info;
            if (spr.CollisionMasks.Count == 0)
                return info;

            // Make copies of all the entries for reference
            foreach (var item in spr.TextureItems)
            {
                if (item == null)
                    return info; // bail
                if (item.TexturePageID != -1)
                    bitmaps.Add(pf.Textures.GetTextureEntryBitmap(item, true));
                else
                    bitmaps.Add(item._Bitmap); // Probably not ever needed, but just in case
            }

#if DEBUG
            // Check that all frames are the same size
            foreach (var item in bitmaps)
            {
                if (item.Width != spr.Width || item.Height != spr.Height)
                    throw new Exception("nonmatching sprite bitmap dimensions??");
            }
#endif

            List<(byte[], int)> bitmapData = new List<(byte[], int)>(bitmaps.Count);
            foreach (var item in bitmaps)
                bitmapData.Add((item.Bytes, item.RowBytes));

            int boundLeft = Math.Clamp(spr.MarginLeft, 0, spr.Width),
                boundRight = Math.Clamp(spr.MarginRight, 0, spr.Width - 1),
                boundTop = Math.Clamp(spr.MarginTop, 0, spr.Height),
                boundBottom = Math.Clamp(spr.MarginBottom, 0, spr.Height - 1);

            switch (spr.SepMasks)
            {
                case GMSprite.SepMaskType.AxisAlignedRect:
                case GMSprite.SepMaskType.RotatedRect:
                    switch (info.Mode)
                    {
                        case MaskMode.Automatic:
                            // Scan for the lowest alpha value in the bounding box
                            // When comparing each pixel, compare to the one in that spot with the highest alpha in every frame

                            bool foundNonzero = false;
                            byte lowest = 0;
                            byte highest = 0;

                            int stride = ((spr.Width + 7) / 8) * 8;

                            FastBitArray mask = new FastBitArray(spr.CollisionMasks[0]);
                            int strideFactor = boundTop * stride;

                            for (int y = boundTop; y <= boundBottom; y++)
                            {
                                for (int x = boundLeft; x <= boundRight; x++)
                                {
                                    if (mask.GetReverse(x + strideFactor))
                                    {
                                        byte highestAlpha = GetHighestAlphaAt(bitmapData, x, y);
                                        if (highestAlpha > highest)
                                            highest = highestAlpha;
                                        if (highestAlpha != 0 && (!foundNonzero || highestAlpha < lowest))
                                        {
                                            lowest = highestAlpha;
                                            foundNonzero = true;
                                        }
                                    }
                                }

                                strideFactor += stride;
                            }

                            if (foundNonzero)
                            {
                                if (lowest == highest)
                                    lowest = 0; // Could be anything
                                else
                                    --lowest;
                            }
                            info.AlphaTolerance = lowest;
                            break;
                        case MaskMode.Manual:
                            info.Left = spr.MarginLeft;
                            info.Right = spr.MarginRight;
                            info.Top = spr.MarginTop;
                            info.Bottom = spr.MarginBottom;
                            break;
                    }
                    break;
                case GMSprite.SepMaskType.Precise:
                    {
                        if (info.Mode != MaskMode.FullImage)
                        {
                            info.Left = spr.MarginLeft;
                            info.Right = spr.MarginRight;
                            info.Top = spr.MarginTop;
                            info.Bottom = spr.MarginBottom;
                        }

                        int stride = ((spr.Width + 7) / 8) * 8;

                        bool foundNonzero = false;
                        byte lowest = 0;
                        byte highest = 0;

                        if (spr.CollisionMasks.Count > 1 && spr.CollisionMasks.Count == spr.TextureItems.Count)
                        {
                            info.Type = MaskType.PrecisePerFrame;

                            for (int i = 0; i < spr.CollisionMasks.Count; i++)
                            {
                                byte[] bitmapBytes = bitmaps[i].Bytes;
                                int bitmapStride = bitmaps[i].RowBytes;
                                FastBitArray mask = new FastBitArray(spr.CollisionMasks[i]);
                                int strideFactor = boundTop * stride;
                                for (int y = boundTop; y <= boundBottom; y++)
                                {
                                    for (int x = boundLeft; x <= boundRight; x++)
                                    {
                                        if (mask.GetReverse(x + strideFactor))
                                        {
                                            byte val = bitmapBytes[(x * 4) + (y * bitmapStride) + 3];
                                            if (val > highest)
                                                highest = val;
                                            if (val != 0 && (!foundNonzero || val < lowest))
                                            {
                                                lowest = val;
                                                foundNonzero = true;
                                            }
                                        }
                                    }

                                    strideFactor += stride;
                                }
                            }
                        }
                        else
                        {
                            info.Type = MaskType.Precise;

                            // Scan for highest alpha, as well as diamond/ellipses
                            FastBitArray mask = new FastBitArray(spr.CollisionMasks[0]);

                            bool isDiamond = true, isEllipse = true;
                            float centerX = ((spr.MarginLeft + spr.MarginRight) / 2);
                            float centerY = ((spr.MarginTop + spr.MarginBottom) / 2);
                            float radiusX = centerX - spr.MarginLeft + 0.5f;
                            float radiusY = centerY - spr.MarginTop + 0.5f;

                            int strideFactor = boundTop * stride;

                            if (!suggestPrecise && radiusX > 0f && radiusY > 0f)
                            {
                                for (int y = boundTop; y <= boundBottom; y++)
                                {
                                    for (int x = boundLeft; x <= boundRight; x++)
                                    {
                                        float normalX = (x - centerX) / radiusX;
                                        float normalY = (y - centerY) / radiusY;
                                        bool inDiamond = Math.Abs(normalX) + Math.Abs(normalY) <= 1f;
                                        bool inEllipse = Math.Pow(normalX, 2.0d) + Math.Pow(normalY, 2.0d) <= 1.0d;

                                        if (mask.GetReverse(x + strideFactor))
                                        {
                                            isDiamond &= inDiamond;
                                            isEllipse &= inEllipse;

                                            byte highestAlpha = GetHighestAlphaAt(bitmapData, x, y);
                                            if (highestAlpha > highest)
                                                highest = highestAlpha;
                                            if (highestAlpha != 0 && (!foundNonzero || highestAlpha < lowest))
                                            {
                                                lowest = highestAlpha;
                                                foundNonzero = true;
                                            }
                                        }
                                        // Can't eliminate based on this, they can be split into pieces with multiple frames
                                        //else
                                        //{
                                        //    isDiamond &= !inDiamond;
                                        //    isEllipse &= !inEllipse;
                                        //}
                                    }

                                    strideFactor += stride;
                                }
                            }
                            else
                            {
                                // Version without diamond/ellipse checks
                                isDiamond = false;
                                isEllipse = false;

                                for (int y = boundTop; y <= boundBottom; y++)
                                {
                                    for (int x = boundLeft; x <= boundRight; x++)
                                    {
                                        if (mask.GetReverse(x + strideFactor))
                                        {
                                            byte highestAlpha = GetHighestAlphaAt(bitmapData, x, y);
                                            if (highestAlpha > highest)
                                                highest = highestAlpha;
                                            if (highestAlpha != 0 && (!foundNonzero || highestAlpha < lowest))
                                            {
                                                lowest = highestAlpha;
                                                foundNonzero = true;
                                            }
                                        }
                                    }

                                    strideFactor += stride;
                                }
                            }

                            if (isDiamond)
                                info.Type = MaskType.Diamond;
                            else if (isEllipse)
                                info.Type = MaskType.Ellipse;
                        }

                        if (info.Mode == MaskMode.Automatic || info.Type == MaskType.Precise ||
                            (info.Mode == MaskMode.Manual && info.Type == MaskType.PrecisePerFrame))
                        {
                            if (foundNonzero)
                            {
                                if (lowest == highest)
                                    lowest = 0; // Could be anything
                                else
                                    --lowest;
                            }
                            info.AlphaTolerance = lowest;
                        }
                    }
                    break;
            }

            return info;
        }

        public static unsafe SKRectI GetBBoxForBitmap(SKBitmap bmp, AssetSprite spr)
        {
            var info = spr.CollisionMask;

            switch (info.Mode)
            {
                case MaskMode.Automatic:
                    {
                        int left = spr.Width - 1, top = spr.Height - 1, right = 0, bottom = 0;

                        int stride = (bmp.RowBytes / 4);
                        int* ptr = (int*)bmp.GetPixels().ToPointer();
                        int* basePtr = ptr;

                        for (int y = 0; y < bmp.Height; y++)
                        {
                            for (int x = 0; x < bmp.Width; x++)
                            {
                                if (*((byte*)ptr + 3) > info.AlphaTolerance)
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
                            ptr += stride - bmp.Width;
                        }

                        return new SKRectI(
                            Math.Max(0, left),
                            Math.Max(0, top),
                            Math.Min(spr.Width - 1, right),
                            Math.Min(spr.Height - 1, bottom));
                    }

                case MaskMode.FullImage:
                    return new SKRectI(0, 0, spr.Width - 1, spr.Height - 1);

                case MaskMode.Manual:
                    return new SKRectI(
                        Math.Clamp((int)info.Left, 0, spr.Width - 1),
                        Math.Clamp((int)info.Top, 0, spr.Height - 1),
                        Math.Clamp((int)info.Right, 0, spr.Width - 1),
                        Math.Clamp((int)info.Bottom, 0, spr.Height - 1));
            }

            throw new ArgumentException("invalid sprite mask mode");
        }

        public static unsafe FastBitArray GetMaskForBitmap(SKBitmap bmp, AssetSprite spr, FastBitArray existingMask = null)
        {
            int stride = ((spr.Width + 7) / 8) * 8;
            FastBitArray res = existingMask ?? new FastBitArray(stride * spr.Height);

            SKRectI bbox = GetBBoxForBitmap(bmp, spr);

            var info = spr.CollisionMask;
            int sprLeft, sprTop, sprRight, sprBottom;

            int strideFactor = bbox.Top * stride;
            switch (info.Type)
            {
                case MaskType.Rectangle:
                case MaskType.RectangleWithRotation:
                    for (int y = bbox.Top; y <= bbox.Bottom; y++)
                    {
                        for (int x = bbox.Left; x <= bbox.Right; x++)
                        {
                            res.SetTrueReverse(x + strideFactor);
                        }

                        strideFactor += stride;
                    }
                    break;
                case MaskType.Precise:
                case MaskType.PrecisePerFrame:
                    int tolerance = info.AlphaTolerance ?? 0;
                    fixed (byte* ptr = &bmp.Bytes[0])
                    {
                        int bmpStride = bmp.RowBytes;

                        for (int y = bbox.Top; y <= bbox.Bottom; y++)
                        {
                            for (int x = bbox.Left; x <= bbox.Right; x++)
                            {
                                if (*(ptr + (x * 4) + (y * bmpStride) + 3) > tolerance)
                                    res.SetTrueReverse(x + strideFactor);
                            }

                            strideFactor += stride;
                        }
                    }
                    break;
                case MaskType.Diamond:
                    {
                        if (info.Mode == MaskMode.FullImage)
                        {
                            sprLeft = 0;
                            sprTop = 0;
                            sprRight = spr.Width - 1;
                            sprBottom = spr.Height - 1;
                        }
                        else
                        {
                            sprLeft = (int)info.Left;
                            sprTop = (int)info.Top;
                            sprRight = (int)info.Right;
                            sprBottom = (int)info.Bottom;
                        }

                        float centerX = (sprLeft + sprRight) / 2;
                        float centerY = (sprTop + sprBottom) / 2;
                        float radiusX = centerX - sprLeft + 0.5f;
                        float radiusY = centerY - sprTop + 0.5f;

                        if (radiusX <= 0 || radiusY <= 0)
                            break;

                        for (int y = bbox.Top; y <= bbox.Bottom; y++)
                        {
                            for (int x = bbox.Left; x <= bbox.Right; x++)
                            {
                                float normalX = (x - centerX) / radiusX;
                                float normalY = (y - centerY) / radiusY;
                                if (Math.Abs(normalX) + Math.Abs(normalY) <= 1f)
                                    res.SetTrueReverse(x + strideFactor);
                            }

                            strideFactor += stride;
                        }
                        break;
                    }
                case MaskType.Ellipse:
                    {
                        if (info.Mode == MaskMode.FullImage)
                        {
                            sprLeft = 0;
                            sprTop = 0;
                            sprRight = spr.Width - 1;
                            sprBottom = spr.Height - 1;
                        }
                        else
                        {
                            sprLeft = (int)info.Left;
                            sprTop = (int)info.Top;
                            sprRight = (int)info.Right;
                            sprBottom = (int)info.Bottom;
                        }

                        float centerX = (sprLeft + sprRight) / 2;
                        float centerY = (sprTop + sprBottom) / 2;
                        float radiusX = centerX - sprLeft + 0.5f;
                        float radiusY = centerY - sprTop + 0.5f;

                        if (radiusX <= 0 || radiusY <= 0)
                            break;

                        for (int y = bbox.Top; y <= bbox.Bottom; y++)
                        {
                            for (int x = bbox.Left; x <= bbox.Right; x++)
                            {
                                float normalX = (x - centerX) / radiusX;
                                float normalY = (y - centerY) / radiusY;
                                if (Math.Pow(normalX, 2.0d) + Math.Pow(normalY, 2.0d) <= 1.0d)
                                    res.SetTrueReverse(x + strideFactor);
                            }

                            strideFactor += stride;
                        }
                        break;
                    }
            }

            return res;
        }

        public static List<byte[]> GetMasksForSprite(AssetSprite spr, List<SKBitmap> bitmaps)
        {
            if (bitmaps.Count == 0)
                return new List<byte[]>();

            if (spr.CollisionMask.Type == MaskType.PrecisePerFrame)
            {
                // Get masks for individual frames
                List<byte[]> res = new List<byte[]>(bitmaps.Count);
                for (int i = 0; i < bitmaps.Count; i++)
                    res.Add(GetMaskForBitmap(bitmaps[i], spr).ToByteArray());
                return res;
            }
            else
            {
                // Get the mask for the first frame, then add following frames
                FastBitArray mask = GetMaskForBitmap(bitmaps[0], spr);
                for (int i = 1; i < bitmaps.Count; i++)
                    GetMaskForBitmap(bitmaps[i], spr, mask);
                return new List<byte[]> { mask.ToByteArray() };
            }
        }

        public static unsafe bool CompareMasks(List<byte[]> a, List<byte[]> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                byte[] arrayA = a[i];
                byte[] arrayB = b[i];

                if (arrayA.Length != arrayB.Length)
                    return false;
                if (arrayA.Length == 0)
                    continue;

                fixed (byte* ptrA = &arrayA[0])
                {
                    fixed (byte* ptrB = &arrayB[0])
                    {
                        byte* posA = ptrA;
                        byte* posB = ptrB;

                        for (int j = 0; j < arrayA.Length; j++)
                        {
                            if (*(posA++) != *(posB++))
                                return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}
