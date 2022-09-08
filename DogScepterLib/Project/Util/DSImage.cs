using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Runtime.InteropServices;
using System.IO;

namespace DogScepterLib.Project.Util;

public class DSImage
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public byte[] Data { get; private set; }
    public int OffsetX { get; private set; } = 0;
    public int OffsetY { get; private set; } = 0;
    public int RealWidth { get; private set; }
    public int RealHeight { get; private set; }
    public DSImage Parent { get; private set; } = null;

    public DSImage(int width, int height)
    {
        Width = width;
        Height = height;
        RealWidth = width;
        RealHeight = height;
        Data = new byte[Width * Height * 4];
    }

    public DSImage(Stream s)
    {
        using Image<Bgra32> img = Image.Load<Bgra32>(s, new PngDecoder { });
        Width = img.Width;
        Height = img.Height;
        RealWidth = img.Width;
        RealHeight = img.Height;
        BuildPixelData(img);
    }

    public DSImage(ReadOnlySpan<byte> data)
    {
        using Image<Bgra32> img = Image.Load<Bgra32>(data, new PngDecoder { });
        Width = img.Width;
        Height = img.Height;
        RealWidth = img.Width;
        RealHeight = img.Height;
        BuildPixelData(img);
    }

    public DSImage(string pngPath)
    {
        using FileStream fs = new FileStream(pngPath, FileMode.Open);
        using Image<Bgra32> img = Image.Load<Bgra32>(fs, new PngDecoder { });
        Width = img.Width;
        Height = img.Height;
        RealWidth = img.Width;
        RealHeight = img.Height;
        BuildPixelData(img);
    }

    // Get a "view" into another image
    public DSImage(DSImage toClone, int x, int y, int width, int height)
    {
        Width = width;
        Height = height;
        RealWidth = toClone.RealWidth;
        RealHeight = toClone.RealHeight;
        Data = toClone.Data;
        Parent = toClone;
        OffsetX = x;
        OffsetY = y;
        while (toClone.Parent != null)
        {
            OffsetX += toClone.OffsetX;
            OffsetY += toClone.OffsetY;
            toClone = toClone.Parent;
        }
    }

    private void BuildPixelData(Image<Bgra32> img)
    {
        if (img.TryGetSinglePixelSpan(out var pixelSpan))
        {
            Data = MemoryMarshal.AsBytes(pixelSpan).ToArray();
        }
        else
            throw new Exception("Image too big?");
    }

    private byte[] GetExportPixelData()
    {
        if (Parent != null)
        {
            // This is a subset of another image; need to build new data here
            byte[] res = new byte[(Width * Height) << 2];
            int pos = (OffsetX + (OffsetY * RealWidth)) << 2;
            int jump = RealWidth << 2;
            int destPos = 0;
            int destJump = Width << 2;
            for (int y = 0; y < Height; y++)
            {
                Buffer.BlockCopy(Data, pos, res, destPos, destJump /* same here */);
                pos += jump;
                destPos += destJump;
            }
            return res;
        }
        return Data;
    }

    public void SavePng(Stream s)
    {
        using var img = Image.LoadPixelData<Bgra32>(GetExportPixelData(), Width, Height);
        img.SaveAsPng(s);
    }

    public void SavePng(string path)
    {
        using var img = Image.LoadPixelData<Bgra32>(GetExportPixelData(), Width, Height);
        img.SaveAsPng(path);
    }

    public void CopyTo(DSImage dest, int destX, int destY) => CopyPartTo(dest, destX, destY, Width, Height, 0, 0);
        
    public void CopyPartTo(DSImage dest, int destX, int destY, int width, int height, int sourceX, int sourceY)
    {
        sourceX += OffsetX;
        sourceY += OffsetY;
        destX += dest.OffsetX;
        destY += dest.OffsetY;
        int pos = (sourceX + (sourceY * RealWidth)) << 2;
        int jump = RealWidth << 2;
        int destPos = (destX + (destY * dest.RealWidth)) << 2;
        int destJump = dest.RealWidth << 2;
        for (int y = 0; y < height; y++)
        {
            Buffer.BlockCopy(Data, pos, dest.Data, destPos, width << 2);
            pos += jump;
            destPos += destJump;
        }
    }

    public unsafe void CopyBorderPixelTo(DSImage dest, int destX, int destY, int border, int sourceX, int sourceY)
    {
        fixed (byte* bSrcPtr = &Data[0], bDestPtr = &dest.Data[0])
        {
            int pixel = *((int*)bSrcPtr + OffsetX + sourceX + ((OffsetY + sourceY) * RealWidth));
            int* destPtr = (int*)bDestPtr + dest.OffsetX + destX + ((dest.OffsetY + destY) * dest.RealWidth);
            for (int y = 0; y < border; y++)
            {
                for (int x = 0; x < border; x++)
                    *destPtr++ = pixel;
                destPtr += (dest.RealWidth - border);
            }
        }
    }

    public unsafe void CopyBorderVertTo(DSImage dest, int destX, int destY, int border, int x)
    {
        int sourceX = OffsetX + x;
        int sourceY = OffsetY;
        destX += dest.OffsetX;
        destY += dest.OffsetY;
        fixed (byte* bSrcPtr = &Data[0], bDestPtr = &dest.Data[0])
        {
            int* srcPtr = (int*)bSrcPtr + (sourceX + (sourceY * RealWidth));
            int* destPtr = (int*)bDestPtr + (destX + (destY * dest.RealWidth));
            for (int y = 0; y < Height; y++)
            {
                int curr = *srcPtr;
                for (int i = 0; i < border; i++)
                    *destPtr++ = curr;
                srcPtr += RealWidth;
                destPtr += (dest.RealWidth - border);
            }
        }
    }

    public unsafe void CopyTiledBorderVertTo(DSImage dest, int destX, int destY, int border, int x)
    {
        if (Width < border)
        {
            // This won't work, so as a failsafe, stretch instead (TODO?)
            CopyBorderHorzTo(dest, destX, destY, border, x > 0 ? Width - 1 : 0);
            return;
        }

        int sourceX = OffsetX + x;
        int sourceY = OffsetY;
        destX += dest.OffsetX;
        destY += dest.OffsetY;
        fixed (byte* bSrcPtr = &Data[0], bDestPtr = &dest.Data[0])
        {
            int* srcPtr = (int*)bSrcPtr + (sourceX + (sourceY * RealWidth));
            int* destPtr = (int*)bDestPtr + (destX + (destY * dest.RealWidth));
            for (int y = 0; y < Height; y++)
            {
                for (int i = 0; i < border; i++)
                    *destPtr++ = *srcPtr++;
                srcPtr += (RealWidth - border);
                destPtr += (dest.RealWidth - border);
            }
        }
    }

    public unsafe void CopyBorderHorzTo(DSImage dest, int destX, int destY, int border, int y)
    {
        int sourceX = OffsetX;
        int sourceY = OffsetY + y;
        destX += dest.OffsetX;
        destY += dest.OffsetY;

        int pos = (sourceX + (sourceY * RealWidth)) << 2;
        int destPos = (destX + (destY * dest.RealWidth)) << 2;
        int destJump = dest.RealWidth << 2;
        for (int i = 0; i < border; i++)
        {
            Buffer.BlockCopy(Data, pos, dest.Data, destPos, Width << 2);
            destPos += destJump;
        }
    }

    public unsafe void CopyTiledBorderHorzTo(DSImage dest, int destX, int destY, int border, int y)
    {
        if (Height < border)
        {
            // This won't work, so as a failsafe, stretch instead (TODO?)
            CopyBorderHorzTo(dest, destX, destY, border, y > 0 ? Height - 1 : 0);
            return;
        }

        int sourceX = OffsetX;
        int sourceY = OffsetY + y;
        destX += dest.OffsetX;
        destY += dest.OffsetY;

        int pos = (sourceX + (sourceY * RealWidth)) << 2;
        int jump = RealWidth << 2;
        int destPos = (destX + (destY * dest.RealWidth)) << 2;
        int destJump = dest.RealWidth << 2;
        for (int i = 0; i < border; i++)
        {
            Buffer.BlockCopy(Data, pos, dest.Data, destPos, Width << 2);
            pos += jump;
            destPos += destJump;
        }
    }
}
