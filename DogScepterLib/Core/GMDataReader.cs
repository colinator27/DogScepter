using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DogScepterLib.Core.Models;
using DogScepterLib.Core.Util;

namespace DogScepterLib.Core
{
    public class GMDataReader : BufferBinaryReader
    {
        public GMData Data;
        public GMData.GMVersionInfo VersionInfo => Data.VersionInfo;
        public List<GMWarning> Warnings;

        public Dictionary<int, GMSerializable> PointerOffsets;
        public Dictionary<int, GMCode.Bytecode.Instruction> Instructions;

        public GMDataReader(Stream stream, string path) : base(stream)
        {
            Data = new GMData();
            Data.WorkingBuffer = Buffer;
            
            // Get hash for comparing later
            using (SHA1Managed sha1 = new SHA1Managed())
                Data.Hash = sha1.ComputeHash(Buffer);
            Data.Length = Buffer.Length;

            // Get directory of the data file for later usage
            if (path != null)
            {
                Data.Directory = Path.GetDirectoryName(path);
                Data.Filename = Path.GetFileName(path);
            }

            Warnings = new List<GMWarning>();
            PointerOffsets = new Dictionary<int, GMSerializable>(65536);
            Instructions = new Dictionary<int, GMCode.Bytecode.Instruction>(1024 * 1024);
        }

        public void Unserialize(bool clearData = true)
        {
#if DEBUG
            Stopwatch s = new Stopwatch();
            s.Start();
#endif

            // Parse the root chunk of the file, FORM
            if (ReadChars(4) != "FORM")
                throw new GMException("Root chunk is not \"FORM\"; invalid file.");
            Data.FORM = new GMChunkFORM();
            Data.FORM.Unserialize(this);

            if (clearData)
            {
                PointerOffsets.Clear();
                Instructions.Clear();
            }

#if DEBUG
            s.Stop();
            Data.Logger?.Invoke($"Finished reading WAD in {s.ElapsedMilliseconds} ms");
#endif
        }

        /// <summary>
        /// Returns (a possibly empty) object of the object type, at the specified pointer address
        /// </summary>
        public T ReadPointer<T>(int ptr) where T : GMSerializable, new()
        {
            if (ptr == 0)
                return default;
            if (PointerOffsets.ContainsKey(ptr))
                return (T)PointerOffsets[ptr];
            T res = new T();
            PointerOffsets[ptr] = res;
            return res;
        }

        /// <summary>
        /// Returns (a possibly empty) object of the object type, at the pointer in the file
        /// </summary>
        public T ReadPointer<T>() where T : GMSerializable, new()
        {
            return ReadPointer<T>(ReadInt32());
        }

        /// <summary>
        /// Follows the specified pointer for an object type, unserializes it and returns it
        /// </summary>
        public T ReadPointerObject<T>(int ptr) where T : GMSerializable, new()
        {
            if (ptr <= 0)
                return default;

            T res;
            if (PointerOffsets.ContainsKey(ptr))
                res = (T)PointerOffsets[ptr];
            else
            {
                res = new T();
                PointerOffsets[ptr] = res;
            }

            int returnTo = Offset;
            Offset = ptr;

            res.Unserialize(this);

            Offset = returnTo;

            return res;
        }

        /// <summary>
        /// Follows the specified pointer for an object type, unserializes it and returns it.
        /// Also has helper callbacks for list reading.
        /// </summary>
        public T ReadPointerObject<T>(int ptr, bool returnAfter = true) where T : GMSerializable, new()
        {
            if (ptr == 0)
                return default;

            T res;
            if (PointerOffsets.ContainsKey(ptr))
                res = (T)PointerOffsets[ptr];
            else
            {
                res = new T();
                PointerOffsets[ptr] = res;
            }

            int returnTo = Offset;
            Offset = ptr;

            res.Unserialize(this);

            if (returnAfter)
                Offset = returnTo;

            return res;
        }

        /// <summary>
        /// Follows the specified pointer for an object type, unserializes it and returns it.
        /// Also has helper callbacks for list reading.
        /// 
        /// This version of the function should only be used when a specific pointer is used *once*, to waste less resources.
        /// This does not add any information or use any information from the pointer map.
        /// </summary>
        public T ReadPointerObjectUnique<T>(int ptr, bool returnAfter = true) where T : GMSerializable, new()
        {
            if (ptr == 0)
                return default;

            T res = new T();

            int returnTo = Offset;
            Offset = ptr;

            res.Unserialize(this);

            if (returnAfter)
                Offset = returnTo;

            return res;
        }

        /// <summary>
        /// Follows a pointer (in the file) for an object type, unserializes it and returns it.
        /// </summary>
        public T ReadPointerObject<T>() where T : GMSerializable, new()
        {
            return ReadPointerObject<T>(ReadInt32());
        }

        /// <summary>
        /// Follows a pointer (in the file) for an object type, unserializes it and returns it.
        /// Uses the unique variant function internally, which does not get involved with the pointer map at all.
        /// </summary>
        public T ReadPointerObjectUnique<T>() where T : GMSerializable, new()
        {
            return ReadPointerObjectUnique<T>(ReadInt32());
        }

        /// <summary>
        /// Reads a string without parsing it
        /// </summary>
        public GMString ReadStringPointer()
        {
            return ReadPointer<GMString>(ReadInt32() - 4);
        }

        /// <summary>
        /// Reads a string AND parses it
        /// </summary>
        public GMString ReadStringPointerObject()
        {
            return ReadPointerObject<GMString>(ReadInt32() - 4);
        }

        /// <summary>
        /// Reads a GameMaker-style string
        /// </summary>
        public string ReadGMString()
        {
            Offset += 4; // Skip length; unreliable
            int baseOffset = Offset;
            while (Buffer[Offset] != 0)
                Offset++;
            int length = Offset - baseOffset;
            string res = Encoding.GetString(Buffer, baseOffset, length);
            Offset++; // go past null terminator
            return res;
        }

        /// <summary>
        /// Reads a 32-bit boolean
        /// </summary>
        public bool ReadWideBoolean()
        {
#if DEBUG
            int val = ReadInt32();
            if (val == 0)
                return false;
            if (val == 1)
                return true;
            Warnings.Add(new GMWarning("Wide boolean is not 0 or 1 before " + Offset.ToString(), GMWarning.WarningLevel.Bad));
            return true;
#else
            return ReadInt32() != 0;
#endif
        }

        /// <summary>
        /// Pads the offset to the next multiple of `alignment`
        /// </summary>
        public void Pad(int alignment)
        {
            if (Offset % alignment != 0)
                Offset += alignment - (Offset % 4);
        }
    }

    /// <summary>
    /// Represents a part of a buffer. Keeps a reference to the source array for its lifetime.
    /// </summary>
    public class BufferRegion
    {
        private readonly byte[] _internalRef;
        public Memory<byte> Memory;
        public int Length => Memory.Length;

        public BufferRegion(byte[] data)
        {
            _internalRef = data;
            Memory = _internalRef.AsMemory();
        }

        public BufferRegion(byte[] source, int start, int count)
        {
            _internalRef = source;
            Memory = _internalRef.AsMemory().Slice(start, count);
        }
    }
}
