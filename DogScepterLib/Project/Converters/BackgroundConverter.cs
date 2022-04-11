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
    public class BackgroundConverter : AssetConverter<AssetBackground>
    {
        public int GetFirstPage(ProjectFile pf, int index)
        {
            var assetRef = pf.Backgrounds[index];
            GMTextureItem item;
            if (assetRef.Asset != null)
                item = assetRef.Asset.TextureItem;
            else
                item = (assetRef.DataAsset as GMBackground).TextureItem;

            if (item != null)
                return item.TexturePageID;
            return -1;
        }

        public override void ConvertData(ProjectFile pf, int index)
        {
            GMBackground asset = (GMBackground)pf.Backgrounds[index].DataAsset;

            AssetBackground projectAsset = new AssetBackground()
            {
                Name = asset.Name?.Content,
                Transparent = asset.Transparent,
                Smooth = asset.Smooth,
                Preload = asset.Preload,
                TextureItem = asset.TextureItem,
                TextureGroup =
                    pf.Textures.TextureGroups[pf.Textures.PageToGroup[asset.TextureItem.TexturePageID]].Name
            };

            if (pf.DataHandle.VersionInfo.IsVersionAtLeast(2))
            {
                projectAsset.GMS2Tiles = new AssetBackground.TileInfo()
                {
                    Width = asset.TileWidth,
                    Height = asset.TileHeight,
                    BorderX = asset.TileOutputBorderX,
                    BorderY = asset.TileOutputBorderY,
                    Columns = asset.TileColumns,
                    FrameLength = asset.TileFrameLength,
                    Tiles = asset.Tiles
                };
            }

            pf.Backgrounds[index].Asset = projectAsset;
        }

        public override void ConvertData(ProjectFile pf)
        {
            EmptyRefsForNamed(pf.DataHandle.GetChunk<GMChunkBGND>().List, pf.Backgrounds);
        }

        public override void ConvertProject(ProjectFile pf)
        {
            var dataAssets = pf.DataHandle.GetChunk<GMChunkBGND>().List;

            // Assemble dictionary of group names to actual Group classes
            Dictionary<string, Textures.Group> groupNames = new Dictionary<string, Textures.Group>();
            foreach (var g in pf.Textures.TextureGroups)
                groupNames[g.Name] = g;

            List<GMBackground> newList = new List<GMBackground>();
            for (int i = 0; i < pf.Backgrounds.Count; i++)
            {
                AssetBackground projectAsset = pf.Backgrounds[i].Asset;
                if (projectAsset == null)
                {
                    // This asset was never converted
                    newList.Add((GMBackground)pf.Backgrounds[i].DataAsset);
                    continue;
                }

                // Add texture item to proper texture group
                if (projectAsset.TextureGroup != null && 
                    groupNames.TryGetValue(projectAsset.TextureGroup, out var group))
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

                if (pf.DataHandle.VersionInfo.IsVersionAtLeast(2))
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
