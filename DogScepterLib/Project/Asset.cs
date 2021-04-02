using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace DogScepterLib.Project
{
    /// <summary>
    /// A high-level asset inside a project, such as a sprite or room
    /// </summary>
    public abstract class Asset : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public byte[] Hash;
        public int Length;
        public bool Dirty = false;

        // Note: This is handled by Fody.PropertyChanged entirely, so no manual work has to be done
        public event PropertyChangedEventHandler PropertyChanged;

        protected abstract byte[] WriteInternal(string assetPath, bool actuallyWrite);

        public void Write(string assetPath)
        {
            byte[] buff = WriteInternal(assetPath, true);

            if (buff == null)
                return;
            ComputeHash(this, buff);
        }

        public static Asset Load(string assetPath)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Stores and computes the length and hash of a given asset's buffer
        /// </summary>
        public static void ComputeHash(Asset asset, byte[] buff)
        {
            asset.Length = buff.Length;
            using (SHA1Managed sha1 = new SHA1Managed())
                asset.Hash = sha1.ComputeHash(buff);
        }

        /// <summary>
        /// Computes an asset's hash by writing it to memory, and then discarding of it
        /// </summary>
        public void ComputeHash()
        {
            byte[] buff = WriteInternal(null, false);
            if (buff == null)
                return;
            ComputeHash(this, buff);
        }

        /// <summary>
        /// Deletes on-disk files for this asset at this path
        /// </summary>
        /// <param name="assetPath"></param>
        public abstract void Delete(string assetPath);

        /// <summary>
        /// Somewhat quickly compares the hash and length of one asset's buffer to another
        /// </summary>
        /// <returns>Whether the buffers are equivalent</returns>
        public unsafe bool CompareHash(Asset other)
        {
            unsafe
            {
                fixed (byte* a = Hash, b = other.Hash)
                {
                    int* ai = (int*)a, bi = (int*)b;
                    return Length == other.Length && ai[0] == bi[0] && ai[1] == bi[1] && ai[2] == bi[2] && ai[3] == bi[3] && ai[4] == bi[4];
                }
            }
        }
    }
}
