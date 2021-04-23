using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using MoreLinq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project
{
    public class Textures
    {
        public class Group
        {
            public HashSet<int> Pages = new HashSet<int>();
            public List<GMTextureItem> Items = new List<GMTextureItem>();

            public int Border { get; set; } = 2;
            public bool AllowCrop { get; set; } = true;

            public Group()
            {
            }

            public Group(int page)
            {
                Pages.Add(page);
            }
        }

        public ProjectFile Project;
        public List<Group> TextureGroups = new List<Group>();
        public Dictionary<int, int> PageToGroup = new Dictionary<int, int>();
        public Dictionary<int, SKBitmap> CachedTextures = new Dictionary<int, SKBitmap>();

        public Textures(ProjectFile project)
        {
            Project = project;

            FindTextureGroups();

            // Finds borders and tiled entries to at least somewhat recreate the original texture
            // This isn't totally accurate to every version, but it's hopefully close enough to look normal
            DetermineGroupBorders();
            DetermineTiledEntries();
        }

        public void FindTextureGroups()
        {
            // Attempts to find a group ID with a page; makes a new group when necessary
            int findGroupWithPage(int page)
            {
                int i;
                for (i = 0; i < TextureGroups.Count; i++)
                {
                    if (TextureGroups[i].Pages.Contains(page))
                        return i;
                }
                TextureGroups.Add(new Group(page));
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

                    for (int i = 0; i < TextureGroups.Count; i++)
                    {
                        if (i == group)
                        {
                            TextureGroups[i].Pages.Add(item.TexturePageID);
                            continue;
                        }

                        if (TextureGroups[i].Pages.Contains(item.TexturePageID))
                        {
                            TextureGroups[group].Pages.UnionWith(TextureGroups[i].Pages);
                            if (group > i)
                                group--;
                            TextureGroups.RemoveAt(i);
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
                {
                    bg.TextureItem._HasExtraBorder = true;
                    findGroupWithPage(bg.TextureItem.TexturePageID);
                }
            }
            foreach (GMFont fnt in Project.DataHandle.GetChunk<GMChunkFONT>().List)
            {
                if (fnt.TextureItem != null)
                    findGroupWithPage(fnt.TextureItem.TexturePageID);
            }

            // Now quickly sort texture groups in case this algorithm gets adjusted
            TextureGroups = TextureGroups.OrderBy(x => x.Pages.Min()).ToList();

            // Assemble dictionary from page IDs to group IDs for convenience
            for (int i = 0; i < TextureGroups.Count; i++)
            {
                foreach (int page in TextureGroups[i].Pages)
                    PageToGroup[page] = i;
            }

            // Assign all texture entries to groups for further processing
            foreach (GMTextureItem entry in Project.DataHandle.GetChunk<GMChunkTPAG>().List)
            {
                TextureGroups[PageToGroup[entry.TexturePageID]].Items.Add(entry);
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

        public void DetermineGroupBorders()
        {
            // Unknown if this check is exactly accurate
            int extraBorder = Project.DataHandle.VersionInfo.IsNumberAtLeast(2) ? 1 : 2;

            foreach (Group group in TextureGroups)
            {
                List<GMTextureItem> entries = group.Items;

                int border = 2;
                bool allowCrop = false;

                if (entries.Count != 0)
                {
                    // Find entry closest to top left of sheet
                    GMTextureItem entry = entries.MinBy((entry) => entry.SourceX * entry.SourceX + entry.SourceY * entry.SourceY).First();
                    border = Math.Max(entry.SourceX, entry.SourceY);
                    if (entry._HasExtraBorder)
                        border -= extraBorder; // additional border

                    // Check if any entries are cropped
                    foreach (var item in entries)
                    {
                        if (item.TargetX != 0 || item.TargetY != 0 ||
                            item.SourceWidth != item.BoundWidth ||
                            item.SourceHeight != item.BoundHeight)
                        {
                            allowCrop = true;
                            break;
                        }
                    }
                }

                group.Border = border;
                group.AllowCrop = allowCrop;
            }
        }

        public unsafe void DetermineTiledEntries()
        {
            foreach (Group group in TextureGroups)
            {
                // If this group's border is > 0
                // check each entry to see if it either wraps around or duplicates the current side
                if (group.Border > 0)
                {
                    foreach (GMTextureItem entry in group.Items)
                    {
                        SKBitmap texture = GetTexture(entry.TexturePageID);

#if DEBUG
                        Debug.Assert(texture.BytesPerPixel == 4, "expected 32 bits per pixel");
                        Debug.Assert(texture.RowBytes % 4 == 0, "expected bytes per row to be divisible by 4");
#endif

                        int stride = (texture.RowBytes / 4);
                        int* ptr = (int*)texture.GetPixels().ToPointer() 
                                            + entry.SourceX + (entry.SourceY * stride);
                        int* basePtr = ptr;

                        // Horizontal check
                        bool tileHoriz = true;
                        for (int y = 0; y < entry.SourceHeight; y++)
                        {
                            if (*(ptr - 1) != *(ptr + (entry.SourceWidth - 1)) ||
                                *ptr != *(ptr + entry.SourceWidth))
                            {
                                tileHoriz = false;
                                break;
                            }
                            ptr += stride;
                        }
                        if (tileHoriz)
                        {
                            // Check again to see if it's just the same on the edges anyway
                            ptr = basePtr;
                            bool notSame = false;
                            for (int y = 0; y < entry.SourceHeight; y++)
                            {
                                if (*ptr != *(ptr - 1) ||
                                    *(ptr + (entry.SourceWidth - 1)) != *(ptr + entry.SourceWidth))
                                {
                                    notSame = true;
                                    break;
                                }
                                ptr += stride;
                            }
                            tileHoriz = notSame;
                        }

                        // Vertical check
                        ptr = basePtr;
                        bool tileVert = true;
                        int bottom = ((entry.SourceHeight - 1) * stride);
                        for (int x = 0; x < entry.SourceWidth; x++)
                        {
                            if (*(ptr - stride) != *(ptr + bottom) ||
                                *ptr != *(ptr + bottom + stride))
                            {
                                tileVert = false;
                                break;
                            }
                            ptr++;
                        }
                        if (tileVert)
                        {
                            // Check again to see if it's just the same on the edges anyway
                            ptr = basePtr;
                            bool notSame = false;
                            for (int x = 0; x < entry.SourceWidth; x++)
                            {
                                if (*ptr != *(ptr - stride) ||
                                    *(ptr + bottom) != *(ptr + bottom + stride))
                                {
                                    notSame = true;
                                    break;
                                }
                                ptr++;
                            }
                            tileVert = notSame;
                        }

                        entry._TileHorizontally = tileHoriz;
                        entry._TileVertically = tileVert;
                    }
                }
            }
        }
    }
}
