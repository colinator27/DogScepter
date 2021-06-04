using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Drawing2D;

namespace DogScepterLib.Project.Util
{
    public static class BitmapExtensions
    {
        public static byte[] GetReadOnlyByteArray(this Bitmap bitmap)
        {
            var data = bitmap.BasicLockBits();

            int buffLength = data.Stride * data.Height;
            byte[] buff = new byte[buffLength];
            Marshal.Copy(data.Scan0, buff, 0, buffLength);

            bitmap.UnlockBits(data);

            return buff;
        }

        public static BitmapData BasicLockBits(this Bitmap bitmap, ImageLockMode mode = ImageLockMode.ReadOnly)
        {
            return bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), mode, PixelFormat.Format32bppArgb);
        }

        public static unsafe Bitmap FullCopy(this Bitmap bitmap)
        {
            BitmapData data = bitmap.BasicLockBits();
            Bitmap copy = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
            BitmapData copyData = copy.BasicLockBits(ImageLockMode.ReadWrite);
            long len = data.Stride * data.Height;
            Buffer.MemoryCopy(data.Scan0.ToPointer(), copyData.Scan0.ToPointer(), len, len);
            bitmap.UnlockBits(data);
            copy.UnlockBits(copyData);
            return copy;
        }
    }
}
