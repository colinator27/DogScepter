using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using SkiaSharp;
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
                Width = asset.Width,
                Height = asset.Height,
                OriginX = asset.OriginX,
                OriginY = asset.OriginY,
                TextureItems = asset.TextureItems.ToList()
            };

            projectAsset.TextureItems.RemoveAll(i => i == null);

            // Determine texture group
            if (projectAsset.TextureItems.Count == 0)
                projectAsset.TextureGroup = null;
            else
            {
                var group = pf.Textures.TextureGroups[
                                pf.Textures.PageToGroup[projectAsset.TextureItems[0].TexturePageID]];
                
                // If this group only has this sprite, and also has a page for
                // each item, then this is a separate group
                if (new HashSet<GMTextureItem>(group.Items).SetEquals(projectAsset.TextureItems) &&
                    group.Pages.Count == projectAsset.TextureItems.Count)
                    projectAsset.SeparateTextureGroup = true;
                
                projectAsset.TextureGroup = group.Name;
            }

            // Determine collision mask info
            List<SKBitmap> bitmaps;
            projectAsset.CollisionMask = CollisionMasks.GetInfoForSprite(pf, asset, out bitmaps);
            List<byte[]> regenerated = CollisionMasks.GetMasksForSprite(projectAsset, bitmaps);
            if (!CollisionMasks.CompareMasks(asset.CollisionMasks, regenerated))
            {
                bool manual = true;
                if (projectAsset.CollisionMask.Type == AssetSprite.CollisionMaskInfo.MaskType.Diamond ||
                    projectAsset.CollisionMask.Type == AssetSprite.CollisionMaskInfo.MaskType.Ellipse)
                {
                    // This may be a false positive diamond/ellipse, try suggesting Precise
                    projectAsset.CollisionMask = CollisionMasks.GetInfoForSprite(pf, asset, out bitmaps, true);
                    regenerated = CollisionMasks.GetMasksForSprite(projectAsset, bitmaps);
                    manual = !CollisionMasks.CompareMasks(asset.CollisionMasks, regenerated);
                }

                if (manual)
                {
                    // Need to generate manually
                    projectAsset.CollisionMask.Mode = AssetSprite.CollisionMaskInfo.MaskMode.Raw;
                    projectAsset.CollisionMask.AlphaTolerance = null;
                    projectAsset.CollisionMask.Left = asset.MarginLeft;
                    projectAsset.CollisionMask.Top = asset.MarginTop;
                    projectAsset.CollisionMask.Right = asset.MarginRight;
                    projectAsset.CollisionMask.Bottom = asset.MarginBottom;
                }
            }

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
