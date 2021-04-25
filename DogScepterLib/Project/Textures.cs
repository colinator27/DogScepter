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
            public Dictionary<long, List<GMTextureItem>> HashedItems = new Dictionary<long, List<GMTextureItem>>();

            public int Border { get; set; } = 2;
            public bool AllowCrop { get; set; } = true;

            public bool Dirty = false;
            public List<(TexturePacker.Page, byte[])> GeneratedPages = new List<(TexturePacker.Page, byte[])>();

            public Group()
            {
            }

            public Group(int page)
            {
                Pages.Add(page);
            }

            public void AddNewEntry(Textures textures, GMTextureItem entry)
            {
                Dirty = true;

                Items.Add(entry);
                if (entry.TexturePageID == -1 && AllowCrop)
                    entry.Crop();

                long key = textures.GetHashKeyForEntry(entry);

                if (HashedItems.TryGetValue(key, out List<GMTextureItem> list))
                    list.Add(entry);
                else
                    HashedItems[key] = new List<GMTextureItem>() { entry };
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
            FillHashTable();
        }

        public void RegenerateTextures()
        {
            // Figure out the groups that need to be regenerated
            List<Group> toRegenerate = TextureGroups.FindAll(g => g.Dirty);

            // Do the tough work first
            Parallel.ForEach(toRegenerate, g =>
            {
                g.Dirty = false;
                g.GeneratedPages.Clear();
                ResolveDuplicates(g);
                foreach (var page in TexturePacker.Pack(g, Project.DataHandle))
                {
                    g.GeneratedPages.Add((page, DrawPage(g, page).Encode(SKEncodedImageFormat.Png, 0).ToArray()));
                }
            });

            // Now handle all data file references
            var tpagList = Project.DataHandle.GetChunk<GMChunkTPAG>().List;
            var txtrList = Project.DataHandle.GetChunk<GMChunkTXTR>().List;

            void handlePage(int dataIndex, (TexturePacker.Page, byte[]) page)
            {
                txtrList[dataIndex].TextureData.Data = page.Item2;
                foreach (TexturePacker.Page.Item item in page.Item1.Items)
                {
                    if (item.TextureItem.TexturePageID == -1)
                        tpagList.Add(item.TextureItem); // this is a custom entry; add it to the data list
                    item.TextureItem.TexturePageID = (short)dataIndex;
                }
            }

            List<int> freePages = new List<int>();

            foreach (var g in toRegenerate)
            {
                int count = g.GeneratedPages.Count;
                int remaining = count;
                int groupIndex = TextureGroups.IndexOf(g);

                var pages = g.Pages.SortedMerge(OrderByDirection.Ascending, freePages);

                foreach (int pageInd in pages)
                {
                    if (remaining <= 0)
                    {
                        // This page can be used by other groups after this
                        freePages.Add(pageInd);
                    }
                    else
                    {
                        if (freePages.Contains(pageInd))
                            freePages.Remove(pageInd);

                        // Work on the pages inline
                        handlePage(pageInd, g.GeneratedPages[count - remaining]);
                        PageToGroup[pageInd] = groupIndex;
                        CachedTextures.Remove(pageInd);

                        remaining--;
                    }
                }

                while (remaining > 0)
                {
                    // Add new pages to the end
                    int pageInd = txtrList.Count;
                    txtrList.Add(new GMTexturePage() { TextureData = new GMTextureData() });

                    handlePage(pageInd, g.GeneratedPages[count - remaining]);
                    PageToGroup[pageInd] = groupIndex;
                    CachedTextures.Remove(pageInd);
                    remaining--;
                }
            }
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
                {
                    fnt.TextureItem._EmptyBorder = true;
                    findGroupWithPage(fnt.TextureItem.TexturePageID);
                }
            }

            // Find any others that haven't been made yet (happens with unreferenced textures/modded games)
            var tpagList = Project.DataHandle.GetChunk<GMChunkTPAG>().List;
            if (PageToGroup.Count != tpagList.Count)
            {
                for (int i = 0; i < tpagList.Count; i++)
                {
                    if (!PageToGroup.ContainsKey(i))
                        PageToGroup[i] = findGroupWithPage(i);
                }
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
                if (entry.TexturePageID == -1)
                    continue;
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
                        Debug.Assert(texture.ColorType == SKColorType.Rgba8888 || texture.ColorType == SKColorType.Bgra8888, "expected 32-bit rgba/bgra");
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

        public unsafe long GetHashKeyForEntry(GMTextureItem entry)
        {
            if (entry.TexturePageID == -1)
            {
                // This is hand-crafted, and has a custom bitmap at the moment
                SKBitmap texture = entry._Bitmap;

#if DEBUG
                Debug.Assert(texture.ColorType == SKColorType.Rgba8888 || texture.ColorType == SKColorType.Bgra8888, "expected 32-bit rgba/bgra");
                Debug.Assert(texture.RowBytes % 4 == 0, "expected bytes per row to be divisible by 4");
#endif

                long key = (long)((entry.SourceWidth * 65535) + entry.SourceHeight) << 32;

                int stride = (texture.RowBytes / 4);
                int* ptr = (int*)texture.GetPixels().ToPointer();
                int w = entry.SourceWidth - 1, h = ((entry.SourceHeight - 1) * stride);

                key |= ((long)(*((byte*)ptr + 3)) << 24) |
                       ((long)(*((byte*)(ptr + w) + 3)) << 16) |
                       ((long)(*((byte*)(ptr + h) + 3)) << 8) |
                       (*((byte*)(ptr + w + h) + 3));

                return key;
            }
            else
            {
                // This is normal
                SKBitmap texture = GetTexture(entry.TexturePageID);

#if DEBUG
                Debug.Assert(texture.ColorType == SKColorType.Rgba8888 || texture.ColorType == SKColorType.Bgra8888, "expected 32-bit rgba/bgra");
                Debug.Assert(texture.RowBytes % 4 == 0, "expected bytes per row to be divisible by 4");
#endif

                long key = (long)((entry.SourceWidth * 65535) + entry.SourceHeight) << 32;

                int stride = (texture.RowBytes / 4);
                int* ptr = (int*)texture.GetPixels().ToPointer()
                                    + entry.SourceX + (entry.SourceY * stride);
                int w = entry.SourceWidth - 1, h = ((entry.SourceHeight - 1) * stride);

                key |= ((long)(*((byte*)ptr + 3)) << 24) |
                       ((long)(*((byte*)(ptr + w) + 3)) << 16) |
                       ((long)(*((byte*)(ptr + h) + 3)) << 8) |
                       (*((byte*)(ptr + w + h) + 3));

                return key;
            }
        }

        public void FillHashTable()
        {
            foreach (var group in TextureGroups)
            {
                foreach (var entry in group.Items)
                {
                    long key = GetHashKeyForEntry(entry);

                    if (group.HashedItems.TryGetValue(key, out List<GMTextureItem> list))
                        list.Add(entry);
                    else
                        group.HashedItems[key] = new List<GMTextureItem>() { entry };
                }
            }
        }

        // Compares contents of textures with the same hash key
        // Notably, the widths/heights of every item are the same
        public unsafe int CompareHashedTextures(GMTextureItem self, List<GMTextureItem> list)
        {
            SKBitmap texture;
            int stride;
            byte[] data;
            if (self.TexturePageID == -1)
                texture = self._Bitmap; // Custom bitmap
            else
                texture = GetTexture(self.TexturePageID);
            stride = (texture.RowBytes / 4);
            data = texture.Bytes;

#if DEBUG
            Debug.Assert(texture.ColorType == SKColorType.Rgba8888 || texture.ColorType == SKColorType.Bgra8888, "expected 32-bit rgba/bgra");
            Debug.Assert(texture.RowBytes % 4 == 0, "expected bytes per row to be divisible by 4");
#endif

            for (int i = 0; i < list.Count; i++)
            {
                GMTextureItem entry = list[i];

                // Eliminate or choose based off of basic factors
                if (entry == self ||
                    entry._HasExtraBorder != self._HasExtraBorder ||
                    entry._TileHorizontally != self._TileHorizontally ||
                    entry._TileVertically != self._TileVertically)
                    continue;
                if (self.TexturePageID != -1)
                {
                    if (self.TexturePageID == entry.TexturePageID)
                    {
                        if (self.SourceX == entry.SourceX &&
                            self.SourceY == entry.SourceY)
                            return i;
                        continue; // These are on the same page but different X/Y, so ignore
                    } else if (entry.TexturePageID != -1)
                    {
                        // These aren't on the same page...
                        continue;
                    }
                }

                // Otherwise, get the raw data
                SKBitmap entryTexture;
                int entryStride;
                byte[] entryData;
                if (entry.TexturePageID == -1)
                    entryTexture = entry._Bitmap; // Custom bitmap
                else
                    entryTexture = GetTexture(entry.TexturePageID);
                entryStride = (entryTexture.RowBytes / 4);
                entryData = entryTexture.Bytes;

#if DEBUG
                Debug.Assert(entryTexture.ColorType == SKColorType.Rgba8888 || entryTexture.ColorType == SKColorType.Bgra8888, "expected 32-bit rgba/bgra");
                Debug.Assert(entryTexture.RowBytes % 4 == 0, "expected bytes per row to be divisible by 4");
#endif

                fixed (byte* ptr = &data[0])
                {
                    fixed (byte* entryPtr = &entryData[0])
                    {
                        int* ptrInt = (int*)ptr + (self.SourceX + (self.SourceY * stride));
                        int* entryPtrInt = (int*)entryPtr + (entry.SourceX + (entry.SourceY * entryStride));

                        bool checking = true;
                        for (int y = 0; y < self.SourceHeight && checking; y++)
                        {
                            for (int x = 0; x < self.SourceWidth; x++)
                            {
                                if (*ptrInt != *entryPtrInt)
                                {
                                    checking = false;
                                    break;
                                }
                                ptrInt++;
                                entryPtrInt++;
                            }
                            int offsetToNext = stride - self.SourceWidth;
                            ptrInt += offsetToNext;
                            entryPtrInt += offsetToNext;
                        }
                        if (checking)
                            return i;
                    }
                }
            }
            return -1;
        }

        // After every element has been properly added to HashedItems, including any new ones
        public void ResolveDuplicates(Group group)
        {
            foreach (var entry in group.Items)
            {
                long key = GetHashKeyForEntry(entry);
                List<GMTextureItem> list;
                if (!group.HashedItems.TryGetValue(key, out list))
                {
                    lock (group.HashedItems)
                    {
                        group.HashedItems[key] = new List<GMTextureItem>() { entry };
                    }
                }
                else if (list.Count != 1)
                {
                    int same = CompareHashedTextures(entry, list);
                    if (same != -1)
                    {
                        entry._DuplicateOf = list[same];
                        while (entry._DuplicateOf._DuplicateOf != null)
                        {
                            entry._DuplicateOf = entry._DuplicateOf._DuplicateOf;
                            if (entry._DuplicateOf == entry)
                            {
                                // This is recursive, break the chain
                                entry._DuplicateOf = null;
                                break;
                            }
                        }
                    }
                }
            }
        }

        public SKBitmap DrawPage(Group group, TexturePacker.Page page)
        {
            // This is pretty much a complete guess here
            bool alwaysCropBorder = !Project.DataHandle.VersionInfo.IsNumberAtLeast(2);

            SKBitmap bmp = new SKBitmap(page.Width, page.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

            using (SKCanvas canvas = new SKCanvas(bmp))
            {
                canvas.Clear();
                foreach (var item in page.Items)
                {
                    GMTextureItem entry = item.TextureItem;

                    if (entry._DuplicateOf != null)
                    {
                        entry.SourceX = (ushort)item.X;
                        entry.SourceY = (ushort)item.Y;
                        continue;
                    }

                    SKBitmap entryBitmap;
                    if (entry.TexturePageID == -1)
                        entryBitmap = entry._Bitmap; // Custom bitmap
                    else
                        entryBitmap = GetTextureEntryBitmapFast(entry);

                    entry.SourceX = (ushort)item.X;
                    entry.SourceY = (ushort)item.Y;

                    canvas.DrawBitmap(entryBitmap, item.X, item.Y);

                    if (!entry._EmptyBorder)
                    { 
                        int border = group.Border;
                        if (border != 0)
                        {
                            if (entry._TileHorizontally)
                            {
                                // left
                                canvas.DrawBitmap(entryBitmap, new SKRectI(entryBitmap.Width - border, 0,
                                                                            entryBitmap.Width, entryBitmap.Height),
                                                                new SKRectI(item.X - border, item.Y, item.X, item.Y + entryBitmap.Height));
                                // right
                                canvas.DrawBitmap(entryBitmap, new SKRectI(0, 0, border, entryBitmap.Height),
                                                                new SKRectI(item.X + entryBitmap.Width, item.Y,
                                                                            item.X + entryBitmap.Width + border, item.Y + entryBitmap.Height));
                                // top left
                                canvas.DrawBitmap(entryBitmap, new SKRectI(entryBitmap.Width - border, entryBitmap.Height - border, 
                                                                            entryBitmap.Width, entryBitmap.Height),
                                                                new SKRectI(item.X - border, item.Y - border, item.X, item.Y));
                                // top right
                                canvas.DrawBitmap(entryBitmap, new SKRectI(0, entryBitmap.Height - border, border, entryBitmap.Height),
                                                                new SKRectI(item.X + entryBitmap.Width, item.Y - border,
                                                                            item.X + entryBitmap.Width + border, item.Y));
                                // bottom left
                                canvas.DrawBitmap(entryBitmap, new SKRectI(entryBitmap.Width - border, 0, entryBitmap.Width, border),
                                                                new SKRectI(item.X - border, item.Y + entryBitmap.Height,
                                                                            item.X, item.Y + entryBitmap.Height + border));
                                // bottom right
                                canvas.DrawBitmap(entryBitmap, new SKRectI(0, 0, border, border),
                                                                new SKRectI(item.X + entryBitmap.Width, item.Y + entryBitmap.Height,
                                                                            item.X + entryBitmap.Width + border, item.Y + entryBitmap.Height + border));
                            }
                            else if (!entry._TileVertically)
                            {
                                if (alwaysCropBorder || entry.TargetX == 0)
                                {
                                    // left
                                    canvas.DrawBitmap(entryBitmap, new SKRectI(0, 0, 1, entryBitmap.Height),
                                                                    new SKRectI(item.X - border, item.Y, item.X, item.Y + entryBitmap.Height));
                                    // top left
                                    canvas.DrawBitmap(entryBitmap, new SKRectI(0, 0, 1, 1),
                                                                    new SKRectI(item.X - border, item.Y - border, item.X, item.Y));
                                    // bottom left
                                    canvas.DrawBitmap(entryBitmap, new SKRectI(0, entryBitmap.Height - 1, 1, entryBitmap.Height),
                                                                    new SKRectI(item.X - border, item.Y + entryBitmap.Height,
                                                                                item.X, item.Y + entryBitmap.Height + border));
                                }
                                if (alwaysCropBorder || entry.TargetX + entry.TargetWidth == entry.BoundWidth)
                                {
                                    // right
                                    canvas.DrawBitmap(entryBitmap, new SKRectI(entryBitmap.Width - 1, 0, entryBitmap.Width, entryBitmap.Height),
                                                                    new SKRectI(item.X + entryBitmap.Width, item.Y,
                                                                                item.X + entryBitmap.Width + border, item.Y + entryBitmap.Height));
                                    // top right
                                    canvas.DrawBitmap(entryBitmap, new SKRectI(entryBitmap.Width - 1, 0, entryBitmap.Width, 1),
                                                                    new SKRectI(item.X + entryBitmap.Width, item.Y - border,
                                                                                item.X + entryBitmap.Width + border, item.Y));
                                    // bottom right
                                    canvas.DrawBitmap(entryBitmap, new SKRectI(entryBitmap.Width - 1, entryBitmap.Height - 1,
                                                                                entryBitmap.Width, entryBitmap.Height),
                                                                    new SKRectI(item.X + entryBitmap.Width, item.Y + entryBitmap.Height,
                                                                                item.X + entryBitmap.Width + border, item.Y + entryBitmap.Height + border));
                                }
                            }

                            if (entry._TileVertically)
                            {
                                // top
                                canvas.DrawBitmap(entryBitmap, new SKRectI(0, entryBitmap.Height - border, entryBitmap.Width, entryBitmap.Height),
                                                                new SKRectI(item.X, item.Y - border, item.X + entryBitmap.Width, item.Y));
                                // bottom
                                canvas.DrawBitmap(entryBitmap, new SKRectI(0, 0, entryBitmap.Width, border),
                                                                new SKRectI(item.X, item.Y + entryBitmap.Height,
                                                                            item.X + entryBitmap.Width, item.Y + entryBitmap.Height + border));
                            }
                            else
                            {
                                if (alwaysCropBorder || entry.TargetY == 0)
                                {
                                    // top
                                    canvas.DrawBitmap(entryBitmap, new SKRectI(0, 0, entryBitmap.Width, 1),
                                                                    new SKRectI(item.X, item.Y - border, item.X + entryBitmap.Width, item.Y));
                                }
                                if (alwaysCropBorder || entry.TargetY + entry.TargetHeight == entry.BoundHeight)
                                {
                                    // bottom
                                    canvas.DrawBitmap(entryBitmap, new SKRectI(0, entryBitmap.Height - 1, entryBitmap.Width, entryBitmap.Height),
                                                                    new SKRectI(item.X, item.Y + entryBitmap.Height,
                                                                                item.X + entryBitmap.Width, item.Y + entryBitmap.Height + border));
                                }
                            }
                        }
                    }
                }
            }

            return bmp;
        }
    }
}
