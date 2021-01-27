using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Project
{
    public class AssetRef<T>
    {
        public T Asset;
        public string RelativePath;
        public bool OnDisk;
    }

    public class AssetList<T> : List<AssetRef<T>>
    {
        private Dictionary<int, AssetRef<T>> Assets = new Dictionary<int, AssetRef<T>>();

        private List<AssetRef<T>> UnsortedAssets = new List<AssetRef<T>>();
        private int MaxIndex = -1;

        public void Put(int index, T asset, string relativePath, bool onDisk)
        {
            MaxIndex = Math.Max(MaxIndex, index);
            if (Assets.ContainsKey(index))
                throw new Exception($"Overlapping index {index} for asset \"{relativePath}\"");
            Assets.Add(index, new AssetRef<T>()
            {
                Asset = asset,
                RelativePath = relativePath,
                OnDisk = onDisk
            });
        }

        public void Put(T asset, string relativePath, bool onDisk)
        {
            UnsortedAssets.Add(new AssetRef<T>()
            {
                Asset = asset,
                RelativePath = relativePath,
                OnDisk = onDisk
            });
        }

        public void Finish()
        {
            Capacity = MaxIndex + UnsortedAssets.Count + 1;
            for (int i = 0; i <= MaxIndex; i++)
                Add(Assets[i]);
            foreach (var a in UnsortedAssets)
                Add(a);

            Assets = null;
            UnsortedAssets = null;
        }
    }
}
