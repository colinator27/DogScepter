using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using DogScepterLib.Core.Util;

namespace DogScepterLib.Core
{
    public class GMDataWriter : BufferBinaryWriter
    {
        public GMData Data;

        public GMDataWriter(GMData data, Stream stream, int baseSize = 1024 * 1024 * 32) : base(stream, baseSize)
        {
            Data = data;

            // Write the root chunk, FORM
            Write("FORM".ToCharArray());
            Data.FORM.Serialize(this);
        }

        public int BeginLength()
        {
            Write(0xDEADC0DE);
            return Offset;
        }

        public void EndLength(int begin)
        {
            int offset = Offset;
            Offset = begin - 4;
            Write(offset - begin);
            Offset = offset;
        }

        public void Pad(int alignment)
        {
            Offset += alignment - (Offset % alignment);
        }
    }
}
