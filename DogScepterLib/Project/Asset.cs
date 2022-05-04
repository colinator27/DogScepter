using DogScepterLib.Core;
using Microsoft.Toolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DogScepterLib.Project
{
    /// <summary>
    /// A high-level asset inside a project, such as a sprite or room
    /// </summary>
    public abstract class Asset
    {
        public string Name { get; set; }
        public byte[] Hash;
        public int Length;
        public bool Dirty = false;

        protected abstract byte[] WriteInternal(ProjectFile pf, string assetPath, bool actuallyWrite);

        public void Write(ProjectFile pf, string assetPath)
        {
            byte[] buff = WriteInternal(pf, assetPath, true);

            if (buff == null)
                return;
            ComputeHash(this, buff);
        }

        public static Asset Load(string assetPath)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Stores and computes the length and hash of a given asset's buffer.
        /// </summary>
        public static void ComputeHash(Asset asset, byte[] buff)
        {
            asset.Length = buff.Length;
            using var sha1 = SHA1.Create();
            asset.Hash = sha1.ComputeHash(buff);
        }

        /// <summary>
        /// Stores and computes the length and hash of a given asset's buffer.
        /// Takes in a BufferRegion instead of a byte[]
        /// </summary>
        public static void ComputeHash(Asset asset, BufferRegion buff)
        {
            asset.Length = buff.Length;
            using var sha1 = SHA1.Create();
            using Stream s = buff.Memory.AsStream();
            asset.Hash = sha1.ComputeHash(s);
        }

        /// <summary>
        /// Computes an asset's hash by writing it to memory, and then discarding of it
        /// </summary>
        public void ComputeHash(ProjectFile pf)
        {
            byte[] buff = WriteInternal(pf, null, false);
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

    public class AssetRefList<T> : List<AssetRef<T>> where T : Asset
    {
        public AssetRef<T> Find(string name)
        {
            return Find(a => a.Name == name);
        }

        public int FindIndex(string name)
        {
            return FindIndex(a => a.Name == name);
        }
    }

    public class AssetRef<T> where T : Asset
    {
        public string Name { get; set; }
        public T Asset { get; set; } = null;
        public int DataIndex { get; set; } = -1;
        public IGMSerializable DataAsset { get; set; } = null;
        public CachedRefData CachedData { get; set; } = null;

        public AssetRef(string name)
        {
            Name = name;
        }

        public AssetRef(string name, T asset, int dataIndex = -1)
        {
            Name = name;
            Asset = asset;
            DataIndex = dataIndex;
        }

        public AssetRef(string name, int dataIndex, IGMSerializable dataAsset = null)
        {
            Name = name;
            DataIndex = dataIndex;
            DataAsset = dataAsset;
        }
    }

    public interface CachedRefData
    {
    }
}
