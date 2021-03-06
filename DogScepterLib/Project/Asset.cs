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
        public byte[] Hash;
        public int Length;

        // Note: This is handled by Fody.PropertyChanged entirely, so no manual work has to be done
        public event PropertyChangedEventHandler PropertyChanged;

        protected abstract byte[] WriteInternal(string assetPath, bool actuallyWrite);

        public void Write(string assetPath)
        {
            byte[] buff = WriteInternal(assetPath, true);

            if (buff == null)
                return;
            Length = buff.Length;
            using (SHA1Managed sha1 = new SHA1Managed())
                Hash = sha1.ComputeHash(buff);
        }

        public void ComputeHash()
        {
            byte[] buff = WriteInternal(null, false);
            if (buff == null)
                return;
            Length = buff.Length;
            using (SHA1Managed sha1 = new SHA1Managed())
                Hash = sha1.ComputeHash(buff);
        }

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
