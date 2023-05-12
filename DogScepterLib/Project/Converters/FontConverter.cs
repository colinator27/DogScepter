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
    public class FontConverter : AssetConverter<AssetFont>
    {
        public int GetFirstPage(ProjectFile pf, int index)
        {
            var assetRef = pf.Fonts[index];
            GMTextureItem item;
            if (assetRef.Asset != null)
                item = assetRef.Asset.TextureItem;
            else
                item = (assetRef.DataAsset as GMFont).TextureItem;

            if (item != null)
                return item.TexturePageID;
            return -1;
        }

        public override void ConvertData(ProjectFile pf, int index)
        {
            GMFont asset = (GMFont)pf.Fonts[index].DataAsset;

            AssetFont projectAsset = new AssetFont()
            {
                Name = asset.Name?.Content,
                DisplayName = asset.DisplayName?.Content,
                Bold = asset.Bold,
                Italic = asset.Italic,
                Charset = asset.Charset,
                AntiAlias = asset.AntiAlias,
                ScaleX = asset.ScaleX,
                ScaleY = asset.ScaleY,
                TextureItem = asset.TextureItem,
                TextureGroup =
                    pf.Textures.TextureGroups[pf.Textures.PageToGroup[asset.TextureItem.TexturePageID]].Name,
                Glyphs = asset.Glyphs.ToList()
            };

            if (asset.Size < 0)
                projectAsset.SizeFloat = asset.SizeFloat;
            else
                projectAsset.Size = asset.Size;

            if (pf.DataHandle.VersionInfo.FormatID >= 17)
            {
                projectAsset.AscenderOffset = asset.AscenderOffset;
                if (pf.DataHandle.VersionInfo.IsVersionAtLeast(2022, 2))
                    projectAsset.Ascender = asset.Ascender;
            }

            pf.Fonts[index].Asset = projectAsset;
        }

        public override void ConvertData(ProjectFile pf)
        {
            EmptyRefsForNamed(pf.DataHandle.GetChunk<GMChunkFONT>().List, pf.Fonts);
        }

        public override void ConvertProject(ProjectFile pf)
        {
            var dataAssets = pf.DataHandle.GetChunk<GMChunkFONT>().List;

            // Assemble dictionary of group names to actual Group classes
            Dictionary<string, Textures.Group> groupNames = new Dictionary<string, Textures.Group>();
            foreach (var g in pf.Textures.TextureGroups)
                groupNames[g.Name] = g;

            List<GMFont> newList = new List<GMFont>();
            for (int i = 0; i < pf.Fonts.Count; i++)
            {
                // Assign new data index to this asset ref
                pf.Fonts[i].DataIndex = newList.Count;

                // Get project-level asset
                AssetFont projectAsset = pf.Fonts[i].Asset;
                if (projectAsset == null)
                {
                    // This asset was never converted
                    newList.Add((GMFont)pf.Fonts[i].DataAsset);
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

                GMFont dataAsset = new GMFont()
                {
                    Name = pf.DataHandle.DefineString(projectAsset.Name),
                    DisplayName = pf.DataHandle.DefineString(projectAsset.DisplayName),
                    Bold = projectAsset.Bold,
                    Italic = projectAsset.Italic,
                    Charset = projectAsset.Charset,
                    AntiAlias = projectAsset.AntiAlias,
                    ScaleX = projectAsset.ScaleX,
                    ScaleY = projectAsset.ScaleY,
                    TextureItem = projectAsset.TextureItem,

                    Glyphs = new Core.GMUniquePointerList<GMGlyph>()
                };

                if (projectAsset.Size != null)
                    dataAsset.Size = (int)projectAsset.Size;
                else
                    dataAsset.SizeFloat = (float)projectAsset.SizeFloat;

                foreach (var g in projectAsset.Glyphs.OrderBy(g => g.Character))
                    dataAsset.Glyphs.Add(g);
                if (dataAsset.Glyphs.Count != 0)
                {
                    dataAsset.RangeStart = dataAsset.Glyphs[0].Character;
                    dataAsset.RangeEnd = dataAsset.Glyphs[^1].Character;
                }
                else
                {
                    dataAsset.RangeStart = 0;
                    dataAsset.RangeEnd = 0;
                }

                if (projectAsset.AscenderOffset != null)
                    dataAsset.AscenderOffset = (int)projectAsset.AscenderOffset;
                if (projectAsset.Ascender != null)
                    dataAsset.Ascender = (int)projectAsset.Ascender;

                newList.Add(dataAsset);
            }

            dataAssets.Clear();
            foreach (var obj in newList)
                dataAssets.Add(obj);
        }
    }
}
