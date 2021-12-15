using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using DogScepterLib.Project.Util;
using System.IO;
using Microsoft.Toolkit.HighPerformance;

namespace DogScepterLib.Project
{
    public class Textures : IDisposable
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
            
            // Set for easy access
            public string Name;

            public bool FillTGIN = true; // whether this gets filled out with assets in TGIN chunk

            public Group()
            {
            }

            public Group(int page)
            {
                Pages.Add(page);
            }

            public void AddNewEntry(Textures textures, GMTextureItem entry)
            {
                if (Items.Contains(entry))
                    return;

                Dirty = true;

                Items.Add(entry);
                if (entry.TexturePageID == -1 && !entry._EmptyBorder && AllowCrop)
                    entry.Crop();

                long key = textures.GetHashKeyForEntry(entry);

                if (HashedItems.TryGetValue(key, out List<GMTextureItem> list))
                    list.Add(entry);
                else
                    HashedItems[key] = new List<GMTextureItem>() { entry };
            }

            public void RemoveEntry(Textures textures, GMTextureItem entry)
            {
                Items.Remove(entry);
                if (HashedItems.TryGetValue(textures.GetHashKeyForEntry(entry), out List<GMTextureItem> list))
                    list.Remove(entry);
            }
        }

        public ProjectFile Project;
        public List<Group> TextureGroups = new List<Group>();
        public Dictionary<int, int> PageToGroup = new Dictionary<int, int>();
        public DSImage[] CachedTextures = null;

        public bool FullyInitialized = false;

        public Textures(ProjectFile project, bool fast = false)
        {
            Project = project;

            FindTextureGroups();

            if (fast)
                return;

            FinishInitialize();
        }

        public void FinishInitialize()
        {
            // Finds borders and tiled entries to at least somewhat recreate the original texture
            // This isn't totally accurate to every version, but it's hopefully close enough to look normal
            DetermineGroupBorders();
            ParseAllTextures();
            DetermineTiledEntries();
            FillHashTable();

            // Clean up memory now so we're not wasting as much, especially when not even using textures
            DisposeAllTextures();

            FullyInitialized = true;
        }

        public void RegenerateTextures()
        {
            // Figure out the groups that need to be regenerated
            List<Group> toRegenerate = TextureGroups.FindAll(g => g.Dirty);

            Project.DataHandle.Logger?.Invoke($"Regenerating textures for {toRegenerate.Count} groups...");

            int maxWidth = Project.TextureGroupSettings.MaxTextureWidth,
                maxHeight = Project.TextureGroupSettings.MaxTextureHeight;
            if ((maxWidth & (maxWidth - 1)) != 0 ||
                (maxHeight & (maxHeight - 1)) != 0 ||
                maxWidth < 4 || maxHeight < 4)
            {
                Project.DataHandle.Logger?.Invoke($"Warning: Invalid texture dimensions: {maxWidth} by {maxHeight}. Using defaults.");
                maxWidth = 2048;
                maxHeight = 2048;
            }

            bool failure = false;

            // Do the tough work first
            Parallel.ForEach(toRegenerate, g =>
            {
                g.Dirty = false;
                g.GeneratedPages.Clear();
                ResolveDuplicates(g);
                var result = TexturePacker.Pack(g, Project.DataHandle, maxWidth, maxHeight);
                if (result == null)
                {
                    failure = true;
                }
                else
                {
                    // Parallel in case there are few groups being generated, to save time
                    Parallel.ForEach(result, page =>
                    {
                        byte[] res;
                        if (Project.DataHandle.VersionInfo.UseQoiFormat)
                        {
                            res = QoiConverter.GetArrayFromImage(DrawPage(g, page));
                        }
                        else
                        {
                            using (var ms = new MemoryStream())
                            {
                                DrawPage(g, page).SavePng(ms);
                                res = ms.ToArray();
                            }
                        }
                        lock (g)
                        {
                            g.GeneratedPages.Add((page, res));
                        }
                    });
                    Project.DataHandle.Logger?.Invoke($"Finished \"{g.Name}\" -> {g.GeneratedPages.Count} page" + (g.GeneratedPages.Count != 1 ? "s" : ""));
                }
            });

            if (failure)
            {
                Project.DataHandle.Logger?.Invoke($"ERROR: Failed to fit entries onto texture pages. The max width/height are likely too small.");
                throw new Exception("Failed to fit entries onto texture pages");
            }

            Project.DataHandle.Logger?.Invoke("Handling texture references...");

            // Now handle all data file references
            var tpagList = Project.DataHandle.GetChunk<GMChunkTPAG>().List;
            var txtrList = Project.DataHandle.GetChunk<GMChunkTXTR>().List;

            void handlePage(int dataIndex, (TexturePacker.Page, byte[]) page)
            {
                var texData = txtrList[dataIndex].TextureData;
                texData.Data = new BufferRegion(page.Item2);
                if (Project.DataHandle.VersionInfo.UseQoiFormat)
                {
                    texData.IsQoi = true;
                    texData.QoiWidth = (short)page.Item1.Width;
                    texData.QoiHeight = (short)page.Item1.Height;
                }
                foreach (TexturePacker.Page.Item item in page.Item1.Items)
                {
                    if (item.TextureItem.TexturePageID == -1)
                        tpagList.Add(item.TextureItem); // this is a custom entry; add it to the data list
                    item.TextureItem.TexturePageID = (short)dataIndex;
                }
            }

            HashSet<int> freePages = new HashSet<int>();

            for (int i = 0; i < toRegenerate.Count; i++)
            {
                Group g = toRegenerate[i];

                int count = g.GeneratedPages.Count;
                int remaining = count;
                int groupIndex = TextureGroups.IndexOf(g);

                var pages = g.Pages.Union(freePages).ToList();
                pages.Sort();
                g.Pages.Clear();

                foreach (int pageInd in pages)
                {
                    if (remaining <= 0)
                    {
                        // This page can be used by other groups after this
                        freePages.Add(pageInd);
                    }
                    else
                    {
                        freePages.Remove(pageInd);

                        // Work on the pages inline
                        g.Pages.Add(pageInd);
                        handlePage(pageInd, g.GeneratedPages[count - remaining]);
                        PageToGroup[pageInd] = groupIndex;
                        CachedTextures[pageInd] = null;

                        remaining--;
                    }
                }

                while (remaining > 0)
                {
                    // Add new pages to the end
                    int pageInd = txtrList.Count;
                    txtrList.Add(new GMTexturePage() { TextureData = new GMTextureData() });

                    g.Pages.Add(pageInd);
                    handlePage(pageInd, g.GeneratedPages[count - remaining]);
                    PageToGroup[pageInd] = groupIndex;
                    CachedTextures[pageInd] = null;
                    remaining--;
                }
            }

            Project.DataHandle.Logger?.Invoke("Texture regeneration complete.");
        }

        // Thorough algorithm to remove all unreferenced texture items.
        public void PurgeUnreferencedItems()
        {
            Project.DataHandle.Logger?.Invoke("Purging unreferenced texture items...");

            List<GMTextureItem> referencedItems = new List<GMTextureItem>(2048);

            foreach (GMSprite spr in Project.DataHandle.GetChunk<GMChunkSPRT>().List)
                referencedItems.AddRange(spr.TextureItems);
            foreach (GMBackground bg in Project.DataHandle.GetChunk<GMChunkBGND>().List)
                referencedItems.Add(bg.TextureItem);
            foreach (GMFont fnt in Project.DataHandle.GetChunk<GMChunkFONT>().List)
                referencedItems.Add(fnt.TextureItem);
            var embi = Project.DataHandle.GetChunk<GMChunkEMBI>();
            if (embi != null)
            {
                foreach (var img in embi.List)
                    referencedItems.Add(img.TextureItem);
            }

            var tpagList = Project.DataHandle.GetChunk<GMChunkTPAG>().List;

            // Find and remove unreferenced items
            List<GMTextureItem> unreferenced = tpagList.Except(referencedItems).ToList();
            if (unreferenced.Count != 0)
            {
                for (int i = tpagList.Count - 1; i >= 0; i--)
                {
                    var item = tpagList[i];
                    int ind = unreferenced.IndexOf(item);
                    if (ind != -1)
                    {
                        unreferenced.RemoveAt(ind);
                        tpagList.RemoveAt(i);
                        if (item.TexturePageID != -1)
                            Project.Textures.TextureGroups[Project.Textures.PageToGroup[item.TexturePageID]]
                                .RemoveEntry(this, item);
                        if (unreferenced.Count == 0)
                            break;
                    }
                }
            }
        }

        // Removes empty texture groups
        public void RefreshTextureGroups()
        {
            Project.DataHandle.Logger?.Invoke("Refreshing texture groups...");

            // Remove empty texture groups
            for (int i = TextureGroups.Count - 1; i >= 0; i--)
            {
                if (TextureGroups[i].Items.Count == 0)
                    TextureGroups.RemoveAt(i);
            }
        }

        // Thorough algorithm to remove all unreferenced texture pages.
        public void PurgeUnreferencedPages()
        {
            Project.DataHandle.Logger?.Invoke("Purging unreferenced texture pages...");

            HashSet<int> referencedPages = new HashSet<int>();

            void addPages(params GMTextureItem[] items)
            {
                foreach (var item in items)
                {
                    if (item != null && item.TexturePageID != -1)
                        referencedPages.Add(item.TexturePageID);
                }
            }

            foreach (GMSprite spr in Project.DataHandle.GetChunk<GMChunkSPRT>().List)
                addPages(spr.TextureItems.ToArray());
            foreach (GMBackground bg in Project.DataHandle.GetChunk<GMChunkBGND>().List)
                addPages(bg.TextureItem);
            foreach (GMFont fnt in Project.DataHandle.GetChunk<GMChunkFONT>().List)
                addPages(fnt.TextureItem);
            var embi = Project.DataHandle.GetChunk<GMChunkEMBI>();
            if (embi != null)
            {
                foreach (var img in embi.List)
                    addPages(img.TextureItem);
            }

            var tpagList = Project.DataHandle.GetChunk<GMChunkTPAG>().List;
            var txtrList = Project.DataHandle.GetChunk<GMChunkTXTR>().List;
            List<int> forRemoval = new List<int>();
            for (int i = 0; i < txtrList.Count; i++)
            {
                if (!referencedPages.Contains(i))
                {
                    // Purge this page; it's unreferenced
                    forRemoval.Add(i);
                }
            }

            for (int i = 0; i < forRemoval.Count; i++)
            {
                int currentIndex = forRemoval[i] - i;
                txtrList.RemoveAt(currentIndex);

                // Update texture page IDs in texture items
                foreach (GMTextureItem item in tpagList)
                {
                    if (item.TexturePageID == currentIndex)
                        item.TexturePageID = -1;
                    else if (item.TexturePageID > currentIndex)
                        item.TexturePageID--;
                }

                // Update texture page IDs in texture groups
                foreach (var g in TextureGroups)
                {
                    List<int> pageList = g.Pages.ToList();
                    for (int j = pageList.Count - 1; j >= 0; j--)
                    {
                        if (pageList[j] == currentIndex)
                            pageList.RemoveAt(j);
                        else if (pageList[j] > currentIndex)
                            pageList[j]--;
                    }
                    g.Pages = new HashSet<int>(pageList);
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
            var embi = Project.DataHandle.GetChunk<GMChunkEMBI>();
            if (embi != null)
            {
                foreach (var img in embi.List)
                {
                    if (img.TextureItem != null)
                        findGroupWithPage(img.TextureItem.TexturePageID);
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

            // Find any others that haven't been made yet (happens with unreferenced textures/modded games)
            var tpagList = Project.DataHandle.GetChunk<GMChunkTPAG>().List;
            if (PageToGroup.Count != tpagList.Count)
            {
                for (int i = 0; i < tpagList.Count; i++)
                {
                    if (!PageToGroup.ContainsKey(tpagList[i].TexturePageID))
                        PageToGroup[i] = findGroupWithPage(tpagList[i].TexturePageID);
                }
            }

            // Assign all texture entries to groups for further processing
            foreach (GMTextureItem entry in Project.DataHandle.GetChunk<GMChunkTPAG>().List)
            {
                if (entry.TexturePageID == -1)
                    continue;
                if (PageToGroup.TryGetValue(entry.TexturePageID, out int groupId))
                    TextureGroups[groupId].Items.Add(entry);
                else
                {
                    // This is an unreferenced entry
                    int group = findGroupWithPage(entry.TexturePageID);
                    PageToGroup[entry.TexturePageID] = group;
                    TextureGroups[group].Items.Add(entry);
                }    
            }
        }

        public DSImage GetTexture(int ind)
        {
            lock (CachedTextures)
            {
                if (CachedTextures[ind] != null)
                    return CachedTextures[ind];
                using Stream s = Project.DataHandle.GetChunk<GMChunkTXTR>().List[ind].TextureData.Data.Memory.AsStream();
                if (Project.DataHandle.VersionInfo.UseQoiFormat)
                    return CachedTextures[ind] = QoiConverter.GetImageFromStream(s);
                else
                    return CachedTextures[ind] = new DSImage(s);
            }       
        }

        public void ParseAllTextures()
        {
            CachedTextures = new DSImage[8192];
            Parallel.ForEach(Project.DataHandle.GetChunk<GMChunkTXTR>().List, (elem, _, i) =>
            {
                using Stream s = elem.TextureData.Data.Memory.AsStream();
                if (Project.DataHandle.VersionInfo.UseQoiFormat)
                    CachedTextures[i] = QoiConverter.GetImageFromStream(s);
                else
                    CachedTextures[i] = new DSImage(s);
            });
        }

        public void DisposeAllTextures()
        {
            if (CachedTextures == null)
                return;
            for (int i = 0; i < CachedTextures.Length; i++)
                CachedTextures[i] = null;
        }

        public DSImage GetTextureEntryImage(GMTextureItem entry, int? suggestWidth = null, int? suggestHeight = null)
        {
            if (entry.TargetX == 0 && entry.TargetY == 0 &&
                entry.SourceWidth == entry.BoundWidth &&
                entry.SourceHeight == entry.BoundHeight &&
                (suggestWidth == null || (suggestWidth == entry.SourceWidth && suggestHeight == entry.SourceHeight)))
            {
                // This can be done without copying
                return GetTextureEntryImageFast(entry);
            }

            DSImage texture = GetTexture(entry.TexturePageID);
            DSImage res = new DSImage(suggestWidth ?? entry.BoundWidth, suggestHeight ?? entry.BoundHeight);
#if DEBUG
            Debug.Assert(entry.SourceWidth == entry.TargetWidth);
            Debug.Assert(entry.SourceHeight == entry.TargetHeight);
#endif
            texture.CopyPartTo(res, entry.TargetX, entry.TargetY, entry.SourceWidth, entry.SourceHeight, entry.SourceX, entry.SourceY);

            return res;
        }

        // Quickly gets a view into a subset of the bitmap, rather than copying and making room for potential padding
        public DSImage GetTextureEntryImageFast(GMTextureItem entry)
        {
            return new DSImage(GetTexture(entry.TexturePageID), entry.SourceX, entry.SourceY, entry.SourceWidth, entry.SourceHeight);
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
                    GMTextureItem entry = entries.MinBy((entry) => entry.SourceX * entry.SourceX + entry.SourceY * entry.SourceY);
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
                    Parallel.ForEach(group.Items, entry =>
                    {
                        DSImage img = CachedTextures[entry.TexturePageID];

                        fixed (byte* bytePtr = &img.Data[0])
                        {
                            int* ptr = (int*)bytePtr + (entry.SourceX + (entry.SourceY * img.Width));
                            int* basePtr = ptr;
                            int stride = img.RealWidth;

                            // Horizontal check
                            bool tileHoriz = true;
                            if (entry.SourceX <= 0 || (entry.SourceX + entry.SourceWidth) >= img.Width)
                                tileHoriz = false; // out of bounds
                            else
                            {
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
                            }

                            // Vertical check
                            ptr = basePtr;
                            bool tileVert = true;
                            if (entry.SourceY <= 0 || (entry.SourceY + entry.SourceHeight) >= img.Height)
                                tileVert = false; // out of bounds
                            else
                            {
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
                            }

                            entry._TileHorizontally = tileHoriz;
                            entry._TileVertically = tileVert;
                        }
                    });
                }
            }
        }

        public unsafe long GetHashKeyForEntry(GMTextureItem entry)
        {
            if (entry.TexturePageID == -1)
            {
                // This is hand-crafted, and has a custom bitmap at the moment
                DSImage texture = entry._Image;
                
                long key = (long)((entry.SourceWidth * 65535) + entry.SourceHeight) << 32;
                fixed (byte* bytePtr = &texture.Data[0])
                {
                    int* ptr = (int*)bytePtr;
                    int w = entry.SourceWidth - 1, h = ((entry.SourceHeight - 1) * texture.Width);

                    key |= ((long)(*((byte*)ptr + 3)) << 24) |
                           ((long)(*((byte*)(ptr + w) + 3)) << 16) |
                           ((long)(*((byte*)(ptr + h) + 3)) << 8) |
                           (*((byte*)(ptr + w + h) + 3));
                }
                return key;
            }
            else
            {
                // This is normal
                DSImage texture = GetTexture(entry.TexturePageID);

                long key = (long)((entry.SourceWidth * 65535) + entry.SourceHeight) << 32;

                fixed (byte* bytePtr = &texture.Data[0])
                {
                    int* ptr = (int*)bytePtr + entry.SourceX + (entry.SourceY * texture.Width);
                    int w = entry.SourceWidth - 1, h = ((entry.SourceHeight - 1) * texture.Width);

                    key |= ((long)(*((byte*)ptr + 3)) << 24) |
                           ((long)(*((byte*)(ptr + w) + 3)) << 16) |
                           ((long)(*((byte*)(ptr + h) + 3)) << 8) |
                           (*((byte*)(ptr + w + h) + 3));
                }
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
        public unsafe int CompareHashedTextures(GMTextureItem self, List<GMTextureItem> list, int startAt = 0)
        {
            if (startAt >= list.Count)
                return -1;

            DSImage texture;
            if (self.TexturePageID == -1)
                texture = self._Image; // Custom bitmap
            else
                texture = GetTexture(self.TexturePageID);
            for (int i = startAt; i < list.Count; i++)
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
                    }
                    else if (entry.TexturePageID != -1)
                    {
                        // These aren't on the same page...
                        continue;
                    }
                }

                // Otherwise, get the raw data
                DSImage entryTexture;
                if (entry.TexturePageID == -1)
                    entryTexture = entry._Image; // Custom bitmap
                else
                    entryTexture = GetTexture(entry.TexturePageID);

                fixed (byte* bytePtr = &texture.Data[0],
                             entryBytePtr = &entryTexture.Data[0])
                {
                    int* ptr = (int*)bytePtr + (self.SourceX + (self.SourceY * texture.Width));
                    int* entryPtr = (int*)entryBytePtr + (entry.SourceX + (entry.SourceY * entryTexture.Width));

                    bool checking = true;
                    for (int y = 0; y < self.SourceHeight && checking; y++)
                    {
                        for (int x = 0; x < self.SourceWidth; x++)
                        {
                            if (*ptr != *entryPtr)
                            {
                                checking = false;
                                break;
                            }
                            ptr++;
                            entryPtr++;
                        }
                        ptr += texture.Width - self.SourceWidth;
                        entryPtr += entryTexture.Width - self.SourceWidth;
                    }

                    if (checking)
                        return i;
                }
            }

            return -1;
        }

        // After every element has been properly added to HashedItems, including any new ones
        public void ResolveDuplicates(Group group)
        {
            foreach (var list in group.HashedItems.Values)
            {
                for (int i = 0; i < list.Count - 1; i++)
                {
                    var entry = list[i];

                    int same = CompareHashedTextures(entry, list, i + 1);
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

        public DSImage DrawPage(Group group, TexturePacker.Page page)
        {
            // This is pretty much a complete guess here
            bool alwaysCropBorder = !Project.DataHandle.VersionInfo.IsNumberAtLeast(2);

            DSImage res = new(page.Width, page.Height);

            foreach (var item in page.Items)
            {
                GMTextureItem entry = item.TextureItem;

                if (entry._DuplicateOf != null)
                {
                    entry.SourceX = (ushort)item.X;
                    entry.SourceY = (ushort)item.Y;
                    continue;
                }

                DSImage entryImage;
                if (entry.TexturePageID == -1)
                    entryImage = entry._Image; // Custom bitmap
                else
                    entryImage = GetTextureEntryImageFast(entry);

                entry.SourceX = (ushort)item.X;
                entry.SourceY = (ushort)item.Y;

                entryImage.CopyTo(res, item.X, item.Y);

                if (!entry._EmptyBorder)
                { 
                    int border = group.Border;
                    if (border != 0)
                    {
                        if (entry._TileHorizontally)
                        {
                            // left
                            entryImage.CopyTiledBorderVertTo(res, item.X - border, item.Y, border, entryImage.Width - border);

                            // right
                            entryImage.CopyTiledBorderVertTo(res, item.X + entryImage.Width, item.Y, border, 0);

                            // top left
                            entryImage.CopyPartTo(res, item.X - border, item.Y - border, border, border, entryImage.Width - border, entryImage.Height - border);

                            // top right
                            entryImage.CopyPartTo(res, item.X + entryImage.Width, item.Y - border, border, border, 0, entryImage.Height - border);

                            // bottom left
                            entryImage.CopyPartTo(res, item.X - border, item.Y + entryImage.Height, border, border, entryImage.Width - border, 0);

                            // bottom right
                            entryImage.CopyPartTo(res, item.X + entryImage.Width, item.Y + entryImage.Height, border, border, 0, 0);
                        }
                        else if (!entry._TileVertically)
                        {
                            if (alwaysCropBorder || entry.TargetX == 0)
                            {
                                // left
                                entryImage.CopyBorderVertTo(res, item.X - border, item.Y, border, 0);

                                // top left
                                entryImage.CopyBorderPixelTo(res, item.X - border, item.Y - border, border, 0, 0);

                                // bottom left
                                entryImage.CopyBorderPixelTo(res, item.X - border, item.Y + entryImage.Height, border, 0, entryImage.Height - 1);
                            }
                            if (alwaysCropBorder || entry.TargetX + entry.TargetWidth == entry.BoundWidth)
                            {
                                // right
                                entryImage.CopyBorderVertTo(res, item.X + entryImage.Width, item.Y, border, entryImage.Width - 1);

                                // top right
                                entryImage.CopyBorderPixelTo(res, item.X + entryImage.Width, item.Y - border, border, entryImage.Width - 1, 0);

                                // bottom right
                                entryImage.CopyBorderPixelTo(res, item.X + entryImage.Width, item.Y + entryImage.Height, border, entryImage.Width - 1, entryImage.Height - 1);
                            }
                        }

                        if (entry._TileVertically)
                        {
                            // top
                            entryImage.CopyTiledBorderHorzTo(res, item.X, item.Y - border, border, entryImage.Height - border);

                            // bottom
                            entryImage.CopyTiledBorderHorzTo(res, item.X, item.Y + entryImage.Height, border, 0);
                        }
                        else
                        {
                            if (alwaysCropBorder || entry.TargetY == 0)
                            {
                                // top
                                entryImage.CopyBorderHorzTo(res, item.X, item.Y - border, border, 0);
                            }
                            if (alwaysCropBorder || entry.TargetY + entry.TargetHeight == entry.BoundHeight)
                            {
                                // bottom
                                entryImage.CopyBorderHorzTo(res, item.X, item.Y + entryImage.Height, border, entryImage.Height - 1);
                            }
                        }
                    }
                }
            }

            return res;
        }
        
        // Clears and re-initializes all the entries in the TGIN chunk
        public void RefreshTGIN()
        {
            var tginList = Project.DataHandle.GetChunk<GMChunkTGIN>()?.List;
            if (tginList != null)
            {
                Project.DataHandle.Logger?.Invoke("Refreshing texture group info...");

                tginList.Clear();

                // Update PageToGroup
                for (int i = 0; i < TextureGroups.Count; i++)
                {
                    foreach (int page in TextureGroups[i].Pages)
                        PageToGroup[page] = i;
                }

                var spriteConverter = Project.GetConverter<SpriteConverter>();
                var bgConverter = Project.GetConverter<BackgroundConverter>();
                var fntConverter = Project.GetConverter<Converters.FontConverter>();

                foreach (var g in TextureGroups)
                {
                    var info = new GMTextureGroupInfo()
                    {
                        Name = Project.DataHandle.DefineString(g.Name),
                        FontIDs = new(),
                        SpineSpriteIDs = new(),
                        SpriteIDs = new(),
                        TexturePageIDs = new(),
                        TilesetIDs = new()
                    };
                    tginList.Add(info);

                    foreach (int id in g.Pages)
                        info.TexturePageIDs.Add(new GMTextureGroupInfo.ResourceID() { ID = id });
                }

                for (int i = 0; i < Project.Sprites.Count; i++)
                {
                    var data = spriteConverter.GetFirstPageAndSpine(Project, i);
                    if (PageToGroup.TryGetValue(data.Item1, out int ind))
                    {
                        tginList[ind].SpriteIDs.Add(new() { ID = i });
                        if (data.Item2)
                            tginList[ind].SpineSpriteIDs.Add(new() { ID = i });
                    }
                }

                for (int i = 0; i < Project.Backgrounds.Count; i++)
                {
                    if (PageToGroup.TryGetValue(bgConverter.GetFirstPage(Project, i), out int ind))
                    {
                        tginList[ind].TilesetIDs.Add(new() { ID = i });
                    }
                }

                for (int i = 0; i < Project.Fonts.Count; i++)
                {
                    if (PageToGroup.TryGetValue(fntConverter.GetFirstPage(Project, i), out int ind))
                    {
                        tginList[ind].FontIDs.Add(new() { ID = i });
                    }
                }
            }
        }

        public void Dispose()
        {
            DisposeAllTextures();
            GC.SuppressFinalize(this);
        }
    }
}
