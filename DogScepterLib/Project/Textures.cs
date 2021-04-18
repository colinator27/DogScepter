using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project
{
    public class Textures
    {
        public ProjectFile Project;
        public List<HashSet<int>> TextureGroupPages = new List<HashSet<int>>();
        public Dictionary<int, int> PageToGroup = new Dictionary<int, int>();
        public Dictionary<int, SKBitmap> CachedTextures = new Dictionary<int, SKBitmap>();

        public Textures(ProjectFile project)
        {
            Project = project;

            FindTextureGroups();
        }

        public void FindTextureGroups()
        {
            // Attempts to find a group ID with a page; makes a new group when necessary
            int findGroupWithPage(int page)
            {
                int i;
                for (i = 0; i < TextureGroupPages.Count; i++)
                {
                    if (TextureGroupPages[i].Contains(page))
                        return i;
                }
                TextureGroupPages.Add(new HashSet<int>() { page });
                return i;
            }

            // Function to create and merge groups based on texture entries known to be on the same group
            void iterateEntries(IList<GMTextureItem> entries)
            {
                if (entries == null || entries.Count == 0 || entries[0] == null)
                    return;

                int group = findGroupWithPage(entries[0].TexturePageID);
                foreach (var item in entries.Skip(1))
                {
                    if (item == null)
                        continue;

                    for (int i = 0; i < TextureGroupPages.Count; i++)
                    {
                        if (i == group)
                        {
                            TextureGroupPages[i].Add(item.TexturePageID);
                            continue;
                        }

                        if (TextureGroupPages[i].Contains(item.TexturePageID))
                        {
                            TextureGroupPages[group].UnionWith(TextureGroupPages[i]);
                            if (group > i)
                                group--;
                            TextureGroupPages.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            // Iterate over all assets to find groups (using above functions)
            foreach (GMSprite spr in Project.DataHandle.GetChunk<GMChunkSPRT>().List)
                iterateEntries(spr.TextureItems);
            foreach (GMBackground bg in Project.DataHandle.GetChunk<GMChunkBGND>().List)
            {
                if (bg.TextureItem != null)
                    findGroupWithPage(bg.TextureItem.TexturePageID);
            }
            foreach (GMFont fnt in Project.DataHandle.GetChunk<GMChunkFONT>().List)
            {
                if (fnt.TextureItem != null)
                    findGroupWithPage(fnt.TextureItem.TexturePageID);
            }

            // Assemble dictionary from page IDs to group IDs for convenience
            for (int i = 0; i < TextureGroupPages.Count; i++)
            {
                foreach (int page in TextureGroupPages[i])
                    PageToGroup[page] = i;
            }
        }

        public SKBitmap GetTexture(int ind)
        {
            if (CachedTextures.TryGetValue(ind, out SKBitmap res))
                return res;
            return CachedTextures[ind] = SKBitmap.Decode(Project.DataHandle.GetChunk<GMChunkTXTR>().List[ind].TextureData.Data);
        }

        public SKBitmap GetTextureEntryBitmap(GMTextureItem entry)
        {
            if (entry.TargetX == 0 && entry.TargetY == 0 &&
                entry.SourceWidth == entry.BoundWidth &&
                entry.SourceHeight == entry.BoundHeight)
            {
                // This can be done without copying
                return GetTextureEntryBitmapFast(entry);
            }

            SKBitmap texture = GetTexture(entry.TexturePageID);
            SKBitmap res = new SKBitmap(entry.BoundWidth, entry.BoundHeight, texture.ColorType, texture.AlphaType);
            using (SKCanvas canvas = new SKCanvas(res))
            {
                canvas.Clear();
                canvas.DrawBitmap(texture, new SKRectI(entry.SourceX, entry.SourceY,
                                                       entry.SourceX + entry.SourceWidth, entry.SourceY + entry.SourceHeight),
                                           new SKRectI(entry.TargetX, entry.TargetY,
                                                       entry.TargetX + entry.TargetWidth, entry.TargetY + entry.TargetHeight));
            }

            return res;
        }

        // Quickly gets a view into a subset of the bitmap, rather than copying and making room for potential padding
        public SKBitmap GetTextureEntryBitmapFast(GMTextureItem entry)
        {
            SKBitmap texture = GetTexture(entry.TexturePageID);
            SKBitmap res = new SKBitmap();
            if (texture.ExtractSubset(res, new SKRectI(entry.SourceX, entry.SourceY, 
                                                       entry.SourceX + entry.SourceWidth, entry.SourceY + entry.SourceHeight)))
                return res;
            return null;
        }
    }
}
