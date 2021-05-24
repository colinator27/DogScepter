using DogScepterLib.Core;
using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project
{
    // Packs texture entries, based on algorithms used by the actual compiler to produce accurate results
    public class TexturePacker
    {
        public class Page
        {
            public int Width;
            public int Height;
            public Rect MainRect;
            public List<Item> Items = new List<Item>();

            public Page(int width, int height)
            {
                Resize(width, height);
            }

            public void Resize(int width, int height)
            {
                Width = width;
                Height = height;
                MainRect = new Rect(0, 0, width, height);
                MainRect.Children = new List<Rect>() { new Rect(0, 0, width, height) };
            }

            public bool AttemptResize(int width, int height, Textures.Group group, int extraBorder, bool pregms2_2_2)
            {
                Resize(width, height);

                foreach (var item in Items)
                {
                    if (!Place(group, item.TextureItem, extraBorder, pregms2_2_2, out _, out _))
                        return false;
                }

                return true;
            }

            public class Item
            {
                public int X, Y;
                public Page TargetPage;
                public GMTextureItem TextureItem;

                public Item(int x, int y, Page targetPage, GMTextureItem textureItem)
                {
                    X = x;
                    Y = y;
                    TargetPage = targetPage;
                    targetPage.Items.Add(this);
                    TextureItem = textureItem;
                }
            }

            public class Rect
            {
                public int X, Y, Width, Height;
                public int Right, Bottom;
                public int Area;
                public long Hash;
                public List<Rect> Children = null;
                public List<Rect> StoredClips = null;
                public bool StoredFull;

                public Rect(int x, int y, int width, int height)
                {
                    X = x;
                    Y = y;
                    Width = width;
                    Height = height;
                    Right = x + width;
                    Bottom = y + height;
                    Area = width * height;
                    
                    // Used for identifying this rectangle in a list quickly
                    Hash = (long)x | ((long)y << 16) | ((long)width << 32) | ((long)height << 48);
                }

                public List<Rect> Clip(int x, int y, int width, int height, out bool full)
                {
                    if (x >= Right || x + width <= X || y >= Bottom || y + height <= Y)
                    {
                        full = false;
                        return null;
                    }    
                    if (Right > x && X < x + width && Y > y && Bottom < y + Height)
                    {
                        full = true;
                        return null;
                    }
                    if (x == X && y == Y && width == Width && height == Height)
                    {
                        full = true;
                        return null;
                    }
                    full = false;

                    List<Rect> result = new List<Rect>();

                    int right = x + width;
                    int bottom = y + height;
                    if (x > X)
                        result.Add(new Rect(X, Y, x - X, Height));
                    if (right < Right)
                        result.Add(new Rect(right, Y, Right - right, Height));
                    if (y > Y)
                        result.Add(new Rect(X, Y, Width, y - Y));
                    if (bottom < Bottom)
                        result.Add(new Rect(X, bottom, Width, Bottom - bottom));

                    return result;
                }

                public List<Rect> ClipHere(int width, int height, out bool full)
                {
                    if (width == Width && height == Height)
                    {
                        full = true;
                        return null;
                    }
                    full = false;

                    List<Rect> result = new List<Rect>();

                    int right = X + width;
                    int bottom = Y + height;
                    if (right < Right)
                        result.Add(new Rect(right, Y, Right - right, Height));
                    if (bottom < Bottom)
                        result.Add(new Rect(X, bottom, Width, Bottom - bottom));

                    return result;
                }

                public Rect FindSpace(int width, int height)
                {
                    Area = 0;
                    if (Children.Count == 0)
                        return null;

                    for (int i = 0; i < Children.Count; i++)
                    {
                        Rect child = Children[i];
                        if (child.Width >= width && child.Height >= height)
                        {
                            bool full;
                            List<Rect> clipping = child.ClipHere(width, height, out full);
                            child.StoredClips = clipping;
                            child.StoredFull = full;
                            if (full && clipping == null)
                            {
                                Area = child.Area;
                                return child;
                            }
                            else
                            {
                                if (clipping != null && clipping.Count != 0)
                                {
                                    for (int j = 0; j < clipping.Count; j++)
                                    {
                                        Rect clip = clipping[j];
                                        if (clip != null && Area < clip.Area)
                                        {
                                            Area = clip.Area;
                                            return child;
                                        }
                                    }
                                } 
                                else
                                {
                                    if (Area < child.Area)
                                    {
                                        Area = child.Area;
                                        return child;
                                    }
                                }
                            }
                        }
                    }

                    return null;
                }

                public void Place(Rect rect, int width, int height, out int x, out int y)
                {
                    bool full;

                    // Search for clips in the base rect
                    List<Rect> clips = rect.StoredClips;
                    if (clips == null)
                        rect.ClipHere(width, height, out full);
                    else
                        full = rect.StoredFull;
                    x = rect.X; 
                    y = rect.Y;

                    if (full)
                    {
                        if (rect != this)
                            Children.Remove(rect); 
                        if (clips != null && clips.Count != 0)
                        {
                            if (Children == null)
                                Children = new List<Rect>(64);
                            Children.AddRange(clips);
                        }
                    } 
                    else if (clips != null && clips.Count != 0)
                    {
                        if (rect != this)
                            Children.Remove(rect);
                        if (Children == null)
                            Children = new List<Rect>(64);
                        Children.AddRange(clips);
                    }

                    // Search for more clips in the (potentially newly-made) children
                    List<Rect> newChildren = new List<Rect>(64);
                    for (int i = 0; i < Children.Count; i++)
                    {
                        clips = Children[i].Clip(x, y, width, height, out full);
                        if (full)
                        {
                            Children.RemoveAt(i--);
                            if (clips != null && clips.Count != 0)
                                newChildren.AddRange(clips);
                        } 
                        else if (clips != null && clips.Count != 0)
                        {
                            Children.RemoveAt(i--);
                            newChildren.AddRange(clips);
                        }
                    }
                    Children.AddRange(newChildren);

                    // Get rid of rects completely contained in another rect
                    int count = Children.Count;
                    Rect[] childArray = Children.ToArray();
                    for (int i = 0; i < count; i++)
                    {
                        Rect first = childArray[i];
                        for (int j = i + 1; j < count; j++)
                        {
                            Rect second = childArray[j];
                            if (second.X >= first.X && second.Y >= first.Y && second.Right <= first.Right && second.Bottom <= first.Bottom)
                                Children[j] = null;
                            if (first.X >= second.X && first.Y >= second.Y && first.Right <= second.Right && first.Bottom <= second.Bottom)
                                Children[i] = null;
                        }
                    }
                    Children.RemoveAll(r => r == null);
                }
            }

            public bool Place(Textures.Group group, GMTextureItem entry, int extraBorder, bool pregms2_2_2, out int x, out int y)
            {
                int gap = group.Border;
                if (entry._HasExtraBorder)
                    gap += extraBorder;

                int width = entry.SourceWidth, height = entry.SourceHeight;
                bool addX = false, addY = false;

                if (pregms2_2_2)
                {
                    // Before 2.2.2
                    if (width + (gap * 2) < Width)
                    {
                        width = (width + (gap * 2) + 3) & -4;
                        addX = true;
                    }
                    if (height + (gap * 2) < Height)
                    {
                        height = (height + (gap * 2) + 3) & -4;
                        addY = true;
                    }
                } 
                else
                {
                    // After 2.2.2
                    if (width != Width)
                    {
                        width += gap * 2;
                        addX = true;
                    }
                    if (height != Height)
                    {
                        height += gap * 2;
                        addY = true;
                    }
                }

                Rect rect = MainRect.FindSpace(width, height);
                if (rect != null)
                {
                    MainRect.Place(rect, width, height, out x, out y);
                    if (addX)
                        x += gap;
                    if (addY)
                        y += gap;
                    return true;
                }

                x = -1;
                y = -1;
                return false;
            }
        }

        // Assumes all texture items on this group are already set up as they're desired to be
        // (i.e., everything is already cropped and de-duplicated, which is out of the scope of this function)
        public static List<Page> Pack(Textures.Group group, GMData data, int maxTextureWidth = 2048, int maxTextureHeight = 2048)
        {
            List<Page> result = new List<Page>();

            bool pregms2_2_2 = !data.VersionInfo.IsNumberAtLeast(2, 2, 2);
            int extraBorder = data.VersionInfo.IsNumberAtLeast(2) ? 1 : 2;

            // Sort entries by area, largest to smallest
            List<GMTextureItem> entries = group.Items.OrderByDescending((entry) => entry.SourceWidth * entry.SourceHeight).ToList();

            // Place every texture item on pages in that order
            List<GMTextureItem> duplicates = new List<GMTextureItem>(64);
            foreach (var entry in entries)
            {
                if (entry._DuplicateOf != null)
                {
                    duplicates.Add(entry);
                    continue; // this is a duplicate of another entry; this will be figured out later
                }

                int x = -1, y = -1;
                Page targetPage = null;

                // Try placing on each page in order until we find one that the entry fits on
                foreach (Page page in result)
                {
                    if (page.Place(group, entry, extraBorder, pregms2_2_2, out x, out y))
                    {
                        targetPage = page;
                        break;
                    }
                }

                if (targetPage == null)
                {
                    // Make a new page and place this entry on that one
                    targetPage = new Page(maxTextureWidth, maxTextureHeight);
                    result.Add(targetPage);
                    targetPage.Place(group, entry, extraBorder, pregms2_2_2, out x, out y);
                }

                if (x == -1)
                {
                    // Failed to pack!
                    return null;
                }

                entry._PackItem = new Page.Item(x, y, targetPage, entry);
            }

            // Now try to shrink each page
            foreach (var page in result)
            {
                int w = page.Width, h = page.Height;
                int originalWidth = w, originalHeight = h;
                while (w != 1 || h != 1)
                {
                    if (page.AttemptResize(w / 2, h / 2, group, extraBorder, pregms2_2_2))
                    {
                        // Successful, so halve the size
                        w /= 2;
                        h /= 2;
                    } 
                    else
                    {
                        // Unsuccessful, try each direction, then stop
                        if (page.AttemptResize(w / 2, h, group, extraBorder, pregms2_2_2))
                        {
                            w /= 2;
                        } 
                        else if (page.AttemptResize(w, h / 2, group, extraBorder, pregms2_2_2))
                        {
                            h /= 2;
                        }
                        break;
                    }
                }

                // We now have the final width/height
                page.Resize(w, h);
                if (w != originalWidth || h != originalHeight)
                {
                    // ...and it changed, so now we have to pack again for real
                    foreach (var item in page.Items)
                    {
                        page.Place(group, item.TextureItem, extraBorder, pregms2_2_2, out item.X, out item.Y);
                    }
                }
            }

            // Now process the duplicates
            foreach (var entry in duplicates)
            {
                var target = entry._DuplicateOf;
                while (target._DuplicateOf != null)
                    target = target._DuplicateOf;
                entry._PackItem = new Page.Item(target._PackItem.X, target._PackItem.Y, target._PackItem.TargetPage, entry);
            }

            return result;
        }
    }
}
