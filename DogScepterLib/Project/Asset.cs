using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DogScepterLib.Project
{
    /// <summary>
    /// A high-level asset inside a project, such as a sprite or room
    /// </summary>
    public abstract class Asset
    {
        public byte[] Hash;

        protected abstract byte[] WriteInternal(string assetDir, string assetName);

        public void Write(string assetPath, string assetName)
        {
            byte[] buff = WriteInternal(assetPath, assetName);
            if (buff == null)
                return;

            using (SHA1Managed sha1 = new SHA1Managed())
                Hash = sha1.ComputeHash(buff);
        }
    }
}
