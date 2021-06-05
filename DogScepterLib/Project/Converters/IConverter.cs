using DogScepterLib.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Converters
{
    public interface IConverter
    {
        // Convert project data to GameMaker data
        public void ConvertProject(ProjectFile pf);

        // Convert GameMaker data to project data
        public void ConvertData(ProjectFile pf);
    }

    public abstract class AssetConverter<T> : IConverter where T : Asset
    {
        protected delegate CachedRefData MakeCachedData(GMNamedSerializable asset);
        protected void EmptyRefsForNamed(IEnumerable<GMNamedSerializable> dataAssets, 
                                         List<AssetRef<T>> projectAssets, 
                                         MakeCachedData makeCachedData = null)
        {
            int index = 0;
            foreach (GMNamedSerializable asset in dataAssets)
            {
                projectAssets.Add(new AssetRef<T>(asset.Name?.Content ?? $"null-{index}", index++, asset)
                {
                    CachedData = makeCachedData?.Invoke(asset) ?? null
                });
            }
        }

        // Convert GameMaker data to project data for one asset
        public abstract void ConvertData(ProjectFile pf, int index);

        public abstract void ConvertData(ProjectFile pf);

        public abstract void ConvertProject(ProjectFile pf);
    }
}
