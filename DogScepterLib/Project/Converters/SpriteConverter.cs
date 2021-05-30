using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Converters
{
    public class SpriteConverter : AssetConverter<AssetSprite>
    {
        public override void ConvertData(ProjectFile pf, int index)
        {
            GMSprite asset = (GMSprite)pf.Sprites[index].DataAsset;

            AssetSprite projectAsset = new AssetSprite()
            {
                Name = asset.Name.Content,
                Transparent = asset.Transparent,
                Smooth = asset.Smooth,
                Preload = asset.Preload
            };

            if (pf.DataHandle.VersionInfo.IsNumberAtLeast(2))
            {
                // todo
            }

            pf.Sprites[index].Asset = projectAsset;
        }

        public override void ConvertData(ProjectFile pf)
        {
            EmptyRefsForNamed(pf.DataHandle.GetChunk<GMChunkSPRT>().List, pf.Sprites);
        }

        public override void ConvertProject(ProjectFile pf)
        {
            var dataAssets = pf.DataHandle.GetChunk<GMChunkSPRT>().List;

            // Assemble dictionary of group names to actual Group classes
            Dictionary<string, Textures.Group> groupNames = new Dictionary<string, Textures.Group>();
            foreach (var g in pf.Textures.TextureGroups)
                groupNames[g.Name] = g;

            List<GMSprite> newList = new List<GMSprite>();
            for (int i = 0; i < pf.Sprites.Count; i++)
            {
                AssetSprite projectAsset = pf.Sprites[i].Asset;
                if (projectAsset == null)
                {
                    // This asset was never converted, so handle references and re-add it
                    GMSprite s = (GMSprite)pf.Sprites[i].DataAsset;
                    s.Name = pf.DataHandle.DefineString(s.Name.Content);
                    newList.Add(s);
                    continue;
                }

                // Add texture item to proper texture group, TODO

                GMSprite dataAsset = new GMSprite()
                {
                    Name = pf.DataHandle.DefineString(projectAsset.Name),
                    Transparent = projectAsset.Transparent,
                    Smooth = projectAsset.Smooth,
                    Preload = projectAsset.Preload
                };

                if (pf.DataHandle.VersionInfo.IsNumberAtLeast(2))
                {
                    // todo
                }

                newList.Add(dataAsset);
            }

            dataAssets.Clear();
            foreach (var obj in newList)
                dataAssets.Add(obj);
        }
    }
}
