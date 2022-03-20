using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Core.Util;

namespace DogScepterLib.Core
{
    public class GMDataWriter : BufferBinaryWriter
    {
        public GMData Data;
        public GMData.GMVersionInfo VersionInfo => Data.VersionInfo;
        public List<GMWarning> Warnings;

        /// Maps used for tracking locations of pointer-referenced objects and the locations to patch
        public Dictionary<GMSerializable, int> PointerOffsets = new Dictionary<GMSerializable, int>();
        public Dictionary<GMSerializable, List<int>> PendingPointerWrites = new Dictionary<GMSerializable, List<int>>();
        public Dictionary<GMString, List<int>> PendingStringPointerWrites = new Dictionary<GMString, List<int>>();

        public Dictionary<GMVariable, List<int>> VariableReferences = new Dictionary<GMVariable, List<int>>();
        public Dictionary<GMFunctionEntry, List<int>> FunctionReferences = new Dictionary<GMFunctionEntry, List<int>>();

        public GMDataWriter(GMData data, Stream stream, string path, int baseSize = 1024 * 1024 * 32) : base(stream, baseSize)
        {
            Data = data;
            Warnings = new List<GMWarning>();

            // Get directory of the data file for later usage
            if (path != null)
                Data.Directory = Path.GetDirectoryName(path);
        }

        /// <summary>
        /// Do the actual work
        /// </summary>
        public void Write()
        {
            // Write the root chunk, FORM
            Write("FORM".ToCharArray());
            Data.FORM.Serialize(this);

            // Handle serialization of pointer offsets
            Parallel.ForEach(PendingPointerWrites, kvp =>
            {
                if (PointerOffsets.TryGetValue(kvp.Key, out int ptr))
                {
                    // Iterate through each reference and write the pointer
                    foreach (int addr in kvp.Value)
                        WriteAt(addr, ptr);
                }
                else
                {
                    // Iterate through each reference and write null
                    foreach (int addr in kvp.Value)
                        WriteAt(addr, 0);
                }
            });
            Parallel.ForEach(PendingStringPointerWrites, kvp =>
            {
                if (PointerOffsets.TryGetValue(kvp.Key, out int ptr))
                {
                    // Adjust offset to string contents beginning
                    ptr += 4;

                    // Iterate through each reference and write the pointer
                    foreach (int addr in kvp.Value)
                        WriteAt(addr, ptr);
                }
                else
                {
                    // Iterate through each reference and write null
                    foreach (int addr in kvp.Value)
                        WriteAt(addr, 0);
                }
            });
        }

        public override void Flush()
        {
            base.Flush();

            // Finalize all other file write operations if any exist
            Data.FileWrites.Complete();
            Data.FileWrites.Completion.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Writes a dummy 32-bit integer to the current location and returns the offset right after it
        /// Return value is meant to be used in conjunction with EndLength(int) to patch lengths
        /// </summary>
        public int BeginLength()
        {
            // Placeholder length value, will be overwritten in the future
            Write(0xBADD0660);
            return Offset;
        }

        /// <summary>
        /// Taking the starting offset of a block, and the current offset, calculates the length,
        /// and writes it as a 32-bit integer right before the block.
        /// </summary>
        public void EndLength(int begin)
        {
            int offset = Offset;
            Offset = begin - 4;

            // Overwrite the aforementioned placeholder value (see above)
            Write(offset - begin);

            Offset = offset;
        }

        /// <summary>
        /// Pads the offset to the next multiple of `alignment`
        /// </summary>
        public void Pad(int alignment)
        {
            if (Offset % alignment != 0)
                Offset += alignment - (Offset % alignment);
        }

        /// <summary>
        /// Write a 32-bit pointer value in this position, for an object
        /// </summary>
        public void WritePointer(GMSerializable obj)
        {
            if (obj == null)
            {
                // This object doesn't exist, so it will never have a pointer value...
                Write(0);
                return;
            }

            // Add this location to a list for this object
            List<int> pending;
            if (PendingPointerWrites.TryGetValue(obj, out pending))
                pending.Add(Offset);
            else
                PendingPointerWrites.Add(obj, new List<int> { Offset });

            // Placeholder pointer value, will be overwritten in the future
            Write(0xBADD0660);
        }

        /// <summary>
        /// Write a 32-bit *string-only* pointer value in this position, for an object
        /// </summary>
        public void WritePointerString(GMString obj)
        {
            if (obj == null)
            {
                // This string object doesn't exist, so it will never have a pointer value...
                Write(0);
                return;
            }

            // Add this location to a list for this string object
            List<int> pending;
            if (PendingStringPointerWrites.TryGetValue(obj, out pending))
                pending.Add(Offset);
            else
                PendingStringPointerWrites.Add(obj, new List<int> { Offset });

            // Placeholder pointer value, will be overwritten in the future
            Write(0xBADD0660);
        }

        /// <summary>
        /// Sets the current offset to be the pointer location for the specified object
        /// </summary>
        public void WriteObjectPointer(GMSerializable obj)
        {
            PointerOffsets.Add(obj, Offset);
        }

        /// <summary>
        /// Writes a 32-bit boolean
        /// </summary>
        public void WriteWideBoolean(bool val)
        {
            Write(val ? 1 : 0);
        }
    }
}
