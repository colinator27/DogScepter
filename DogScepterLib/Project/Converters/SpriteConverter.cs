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
                Preload = asset.Preload,
                TextureItem = asset.TextureItem,
                TextureGroup =
                    pf.Textures.TextureGroups[pf.Textures.PageToGroup[asset.TextureItem.TexturePageID]].Name
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
                AssetBackground projectAsset = pf.Backgrounds[i].Asset;
                if (projectAsset == null)
                {
                    // This asset was never converted, so handle references and re-add it
                    GMBackground b = (GMBackground)pf.Backgrounds[i].DataAsset;
                    b.Name = pf.DataHandle.DefineString(b.Name.Content);
                    newList.Add(b);
                    continue;
                }

                // Add texture item to proper texture group
                if (groupNames.TryGetValue(projectAsset.TextureGroup, out var group))
                {
                    group.AddNewEntry(pf.Textures, projectAsset.TextureItem);
                }
                else
                {
                    // Make a new texture group for this
                    var g = new Textures.Group()
                    {
                        Dirty = true,
                        Border = 0,
                        AllowCrop = false,
                        Name = $"__DS_AUTO_GEN_{projectAsset.Name}__{pf.Textures.TextureGroups.Count}"
                    };
                    g.AddNewEntry(pf.Textures, projectAsset.TextureItem);
                    pf.Textures.TextureGroups.Add(g);
                }

                GMBackground dataAsset = new GMBackground()
                {
                    Name = pf.DataHandle.DefineString(projectAsset.Name),
                    Transparent = projectAsset.Transparent,
                    Smooth = projectAsset.Smooth,
                    Preload = projectAsset.Preload,
                    TextureItem = projectAsset.TextureItem
                };

                if (pf.DataHandle.VersionInfo.IsNumberAtLeast(2))
                {
                    dataAsset.TileWidth = projectAsset.GMS2Tiles.Width;
                    dataAsset.TileHeight = projectAsset.GMS2Tiles.Height;
                    dataAsset.TileOutputBorderX = projectAsset.GMS2Tiles.BorderX;
                    dataAsset.TileOutputBorderY = projectAsset.GMS2Tiles.BorderY;
                    dataAsset.TileColumns = projectAsset.GMS2Tiles.Columns;
                    dataAsset.TileFrameLength = projectAsset.GMS2Tiles.FrameLength;
                    dataAsset.Tiles = projectAsset.GMS2Tiles.Tiles;
                }

                newList.Add(dataAsset);
            }

            dataAssets.Clear();
            foreach (var obj in newList)
                dataAssets.Add(obj);
        }
    }
}
