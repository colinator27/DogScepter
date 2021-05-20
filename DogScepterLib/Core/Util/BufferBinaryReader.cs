using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DogScepterLib.Core.Util
{
    public class BufferBinaryReader
    {
        private readonly byte[] buffer;
        public Encoding Encoding { get; private set; }

        public int Offset { get; set; }
        public int Length { get; private set; }
        public byte[] Buffer { get => buffer; }

        public BufferBinaryReader(Stream stream)
        {
            Length = (int)stream.Length;
            buffer = new byte[Length];
            Offset = 0;

            if (stream.Position != 0)
                stream.Seek(0, SeekOrigin.Begin);
            stream.Read(buffer, 0, Length);
            stream.Close();

            Encoding = new UTF8Encoding(false);
        }

        public byte ReadByte()
        {
#if DEBUG
            if (Offset < 0 || Offset + 1 > Length)
                throw new IOException("Reading out of bounds");
#endif
            return buffer[Offset++];
        }

        public virtual bool ReadBoolean()
        {
            return ReadByte() != 0;
        }

        public string ReadChars(int count)
        {
#if DEBUG
            if (Offset < 0 || Offset + count > Length)
                throw new IOException("Reading out of bounds");
#endif
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < count; i++)
                sb.Append(Convert.ToChar(buffer[Offset++]));
            return sb.ToString();
        }

        public byte[] ReadBytes(int count)
        {
#if DEBUG
            if (Offset < 0 || Offset + count > Length)
                throw new IOException("Reading out of bounds");
#endif
            byte[] val = new byte[count];
            System.Buffer.BlockCopy(buffer, Offset, val, 0, count);
            Offset += count;
            return val;
        }

        public short ReadInt16()
        {
#if DEBUG
            if (Offset < 0 || Offset + 2 > Length)
                throw new IOException("Reading out of bounds");
#endif
            return (short)(buffer[Offset++] | buffer[Offset++] << 8);
        }

        public ushort ReadUInt16()
        {
#if DEBUG
            if (Offset < 0 || Offset + 2 > Length)
                throw new IOException("Reading out of bounds");
#endif
            return (ushort)(buffer[Offset++] | buffer[Offset++] << 8);
        }

        public int ReadInt24()
        {
#if DEBUG
            if (Offset < 0 || Offset + 3 > Length)
                throw new IOException("Reading out of bounds");
#endif
            return (int)(buffer[Offset++] | buffer[Offset++] << 8 | (sbyte)buffer[Offset++] << 16);
        }

        public uint ReadUInt24()
        {
#if DEBUG
            if (Offset < 0 || Offset + 3 > Length)
                throw new IOException("Reading out of bounds");
#endif
            return (uint)(buffer[Offset++] | buffer[Offset++] << 8 | buffer[Offset++] << 16);
        }

        public int ReadInt32()
        {
#if DEBUG
            if (Offset < 0 || Offset + 4 > Length)
                throw new IOException("Reading out of bounds");
#endif
            return (int)(buffer[Offset++] | buffer[Offset++] << 8 | 
                         buffer[Offset++] << 16 | (sbyte)buffer[Offset++] << 24);
        }

        public uint ReadUInt32()
        {
#if DEBUG
            if (Offset < 0 || Offset + 4 > Length)
                throw new IOException("Reading out of bounds");
#endif
            return (uint)(buffer[Offset++] | buffer[Offset++] << 8 | 
                          buffer[Offset++] << 16 | buffer[Offset++] << 24);
        }

        public float ReadSingle()
        {
#if DEBUG
            if (Offset < 0 || Offset + 4 > Length)
                throw new IOException("Reading out of bounds");
#endif
            float val = BitConverter.ToSingle(buffer, Offset);
            Offset += 4;
            return val;
        }

        public double ReadDouble()
        {
#if DEBUG
            if (Offset < 0 || Offset + 8 > Length)
                throw new IOException("Reading out of bounds");
#endif
            double val = BitConverter.ToDouble(buffer, Offset);
            Offset += 8;
            return val;
        }

        public long ReadInt64()
        {
#if DEBUG
            if (Offset < 0 || Offset + 8 > Length)
                throw new IOException("Reading out of bounds");
#endif
            long val = BitConverter.ToInt64(buffer, Offset);
            Offset += 8;
            return val;
        }

        public ulong ReadUInt64()
        {
#if DEBUG
            if (Offset < 0 || Offset + 8 > Length)
                throw new IOException("Reading out of bounds");
#endif
            ulong val = BitConverter.ToUInt64(buffer, Offset);
            Offset += 8;
            return val;
        }
    }
}