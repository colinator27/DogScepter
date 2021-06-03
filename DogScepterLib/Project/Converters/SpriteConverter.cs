using DogScepterLib.Core;
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
        public (int, bool) GetFirstPageAndSpine(ProjectFile pf, int index)
        {
            var assetRef = pf.Sprites[index];
            IEnumerable<GMTextureItem> list;
            bool spine;
            if (assetRef.Asset != null)
            {
                list = assetRef.Asset.TextureItems;
                spine = assetRef.Asset.SpecialInfo?.SpriteType == GMSprite.SpriteType.Spine;
            }
            else
            {
                var asset = (assetRef.DataAsset as GMSprite);
                list = asset.TextureItems;
                spine = asset.S_SpriteType == GMSprite.SpriteType.Spine;
            }

            if (list.Any())
            {
                foreach (var item in list)
                    if (item != null)
                        return (item.TexturePageID, spine);
            }
            return (-1, spine);
        }

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
            List<byte[]> regenerated = CollisionMasks.GetMasksForSprite(pf, projectAsset, out _, bitmaps);
            if (!CollisionMasks.CompareMasks(asset.CollisionMasks, regenerated))
            {
                bool manual = true;
                if (projectAsset.CollisionMask.Type == AssetSprite.CollisionMaskInfo.MaskType.Diamond ||
                    projectAsset.CollisionMask.Type == AssetSprite.CollisionMaskInfo.MaskType.Ellipse)
                {
                    // This may be a false positive diamond/ellipse, try suggesting Precise
                    projectAsset.CollisionMask = CollisionMasks.GetInfoForSprite(pf, asset, out bitmaps, true);
                    regenerated = CollisionMasks.GetMasksForSprite(pf, projectAsset, out _, bitmaps);
                    manual = !CollisionMasks.CompareMasks(asset.CollisionMasks, regenerated);
                }

                if (manual)
                {
                    // Need to generate manually
                    projectAsset.CollisionMask.Mode = (AssetSprite.CollisionMaskInfo.MaskMode)(-1 - (int)projectAsset.CollisionMask.Mode);
                    projectAsset.CollisionMask.AlphaTolerance = null;
                    projectAsset.CollisionMask.Left = asset.MarginLeft;
                    projectAsset.CollisionMask.Top = asset.MarginTop;
                    projectAsset.CollisionMask.Right = asset.MarginRight;
                    projectAsset.CollisionMask.Bottom = asset.MarginBottom;
                    projectAsset.CollisionMask.RawMasks = asset.CollisionMasks;
                }
            }

            if (asset.SpecialOrGMS2)
            {
                projectAsset.SpecialInfo = new AssetSprite.SpriteSpecialInfo()
                {
                    SpriteType = asset.S_SpriteType
                };

                if (asset.S_SpriteType != GMSprite.SpriteType.Normal)
                {
                    projectAsset.SpecialInfo.Buffer = "buffer.bin";
                    projectAsset.SpecialInfo.InternalBuffer = asset.S_Buffer;
                }

                if (pf.DataHandle.VersionInfo.IsNumberAtLeast(2))
                {
                    projectAsset.SpecialInfo.GMS2PlaybackSpeed = asset.GMS2_PlaybackSpeed;
                    projectAsset.SpecialInfo.GMS2PlaybackSpeedType = asset.GMS2_PlaybackSpeedType;

                    if (asset.GMS2_3_Sequence != null)
                    {
                        var seq = asset.GMS2_3_Sequence.Sequence;

                        List<AssetSprite.SpriteSpecialInfo.SequenceInfo.Frame> frames = new();
                        foreach (var keyframe in 
                                 (seq.Tracks[0].Keyframes as GMSequence.Track.ValueKeyframes).List)
                        {
                            frames.Add(new()
                            {
                                Position = keyframe.Key,
                                Length = keyframe.Length,
                                Index = keyframe.Channels.Values.First().Value
                            });
                        }

                        List<AssetSprite.SpriteSpecialInfo.SequenceInfo.BroadcastMessage> broadcastMessages = new();
                        foreach (var message in seq.BroadcastMessages)
                        {
                            broadcastMessages.Add(new()
                            {
                                Position = message.Key,
                                Message = message.Channels.Values.First().List.First().Content
                            });
                        }
                        
                        projectAsset.SpecialInfo.Sequence = new AssetSprite.SpriteSpecialInfo.SequenceInfo()
                        {
                            Name = seq.Name.Content,
                            Frames = frames,
                            BroadcastMessages = broadcastMessages
                        };
                    }

                    if (asset.GMS2_3_2_NineSlice != null)
                    {
                        projectAsset.SpecialInfo.NineSlice = new AssetSprite.SpriteSpecialInfo.NineSliceInfo()
                        {
                            Left = asset.GMS2_3_2_NineSlice.Left,
                            Top = asset.GMS2_3_2_NineSlice.Top,
                            Right = asset.GMS2_3_2_NineSlice.Right,
                            Bottom = asset.GMS2_3_2_NineSlice.Bottom,
                            Enabled = asset.GMS2_3_2_NineSlice.Enabled,
                            TileModes = asset.GMS2_3_2_NineSlice.TileModes.ToList()
                        };
                    }
                }
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
                    // This asset was never converted
                    newList.Add((GMSprite)pf.Sprites[i].DataAsset);
                    continue;
                }

                // Add texture items to group
                if (!projectAsset.SeparateTextureGroup &&
                    projectAsset.TextureGroup != null && 
                    groupNames.TryGetValue(projectAsset.TextureGroup, out var group))
                {
                    foreach (var item in projectAsset.TextureItems)
                        if (item != null)
                            group.AddNewEntry(pf.Textures, item);
                }
                else
                {
                    if (projectAsset.SeparateTextureGroup)
                    {
                        // Export each frame to a separate texture page
                        foreach (var item in projectAsset.TextureItems)
                        {
                            if (item == null)
                                continue;

                            var g = new Textures.Group()
                            {
                                Dirty = true,
                                Border = 0,
                                AllowCrop = false,
                                Name = $"__DS_AUTO_GEN_{projectAsset.Name}__{pf.Textures.TextureGroups.Count}",
                                FillTGIN = false // Apparently
                            };
                            g.AddNewEntry(pf.Textures, item);
                            pf.Textures.TextureGroups.Add(g);
                        }
                    }
                    else
                    {
                        // Make a new texture group for this
                        var g = new Textures.Group()
                        {
                            Dirty = true,
                            Border = 0,
                            AllowCrop = false,
                            Name = $"__DS_AUTO_GEN_{projectAsset.Name}__{pf.Textures.TextureGroups.Count}",
                            FillTGIN = false // Apparently
                        };
                        foreach (var item in projectAsset.TextureItems)
                            if (item != null)
                                g.AddNewEntry(pf.Textures, item);
                        pf.Textures.TextureGroups.Add(g);
                    }
                }

                CollisionMasks.Rect outbbox;

                GMSprite dataAsset = new GMSprite()
                {
                    Name = pf.DataHandle.DefineString(projectAsset.Name),
                    Transparent = projectAsset.Transparent,
                    Smooth = projectAsset.Smooth,
                    Preload = projectAsset.Preload,
                    Width = projectAsset.Width,
                    Height = projectAsset.Height,
                    OriginX = projectAsset.OriginX,
                    OriginY = projectAsset.OriginY,
                    TextureItems = new GMRemotePointerList<GMTextureItem>(),
                    CollisionMasks = CollisionMasks.GetMasksForSprite(pf, projectAsset, out outbbox)
                };

                // Get collision mask info
                var colInfo = projectAsset.CollisionMask;
                if (colInfo.Left == null || colInfo.Top == null || colInfo.Right == null || colInfo.Bottom == null)
                {
                    dataAsset.MarginLeft = outbbox.Left;
                    dataAsset.MarginTop = outbbox.Top;
                    dataAsset.MarginRight = outbbox.Right;
                    dataAsset.MarginBottom = outbbox.Bottom;
                }
                else
                {
                    dataAsset.MarginLeft = (int)colInfo.Left;
                    dataAsset.MarginTop = (int)colInfo.Top;
                    dataAsset.MarginRight = (int)colInfo.Right;
                    dataAsset.MarginBottom = (int)colInfo.Bottom;
                }

                if ((int)colInfo.Mode < 0)
                {
                    dataAsset.BBoxMode = (uint)(-(1 + (int)colInfo.Mode));
                    dataAsset.SepMasks = GMSprite.SepMaskType.Precise;
                }
                else
                {
                    dataAsset.BBoxMode = (uint)colInfo.Mode;
                    switch (colInfo.Type)
                    {
                        case AssetSprite.CollisionMaskInfo.MaskType.Rectangle:
                            dataAsset.SepMasks = GMSprite.SepMaskType.AxisAlignedRect;
                            break;
                        case AssetSprite.CollisionMaskInfo.MaskType.RectangleWithRotation:
                            dataAsset.SepMasks = GMSprite.SepMaskType.RotatedRect;
                            break;
                        case AssetSprite.CollisionMaskInfo.MaskType.Precise:
                        case AssetSprite.CollisionMaskInfo.MaskType.Diamond:
                        case AssetSprite.CollisionMaskInfo.MaskType.Ellipse:
                        case AssetSprite.CollisionMaskInfo.MaskType.PrecisePerFrame:
                            dataAsset.SepMasks = GMSprite.SepMaskType.Precise;
                            break;
                    }
                }

                // Actually add the texture items
                foreach (var item in projectAsset.TextureItems)
                    dataAsset.TextureItems.Add(item);

                if (projectAsset.SpecialInfo != null)
                {
                    var info = projectAsset.SpecialInfo;

                    dataAsset.SpecialOrGMS2 = true;
                    dataAsset.S_SpriteType = info.SpriteType;
                    if (info.SpriteType != GMSprite.SpriteType.Normal)
                        dataAsset.S_Buffer = info.InternalBuffer;

                    if (info.GMS2PlaybackSpeed != null)
                    {
                        dataAsset.GMS2_PlaybackSpeed = (float)info.GMS2PlaybackSpeed;
                        dataAsset.GMS2_PlaybackSpeedType = (GMSprite.AnimSpeedType)info.GMS2PlaybackSpeedType;
                    }

                    if (projectAsset.SpecialInfo.Sequence != null)
                    {
                        var seq = projectAsset.SpecialInfo.Sequence;
                        var newSeq = new GMSequence()
                        {
                            Name = pf.DataHandle.DefineString(seq.Name),
                            PlaybackType = GMSequence.PlaybackTypeEnum.Loop,
                            PlaybackSpeed = dataAsset.GMS2_PlaybackSpeed,
                            PlaybackSpeedType = dataAsset.GMS2_PlaybackSpeedType,
                            Length = seq.Frames.Max(f => f.Position + f.Length),
                            OriginX = dataAsset.OriginX,
                            OriginY = dataAsset.OriginY,
                            Volume = 1,
                            BroadcastMessages = new(),
                            Tracks = new()
                            {
                                new()
                                {
                                    BuiltinName = 0,
                                    IsCreationTrack = false,
                                    Keyframes = new GMSequence.Track.ValueKeyframes()
                                    {
                                        List = new()
                                    },
                                    ModelName = pf.DataHandle.DefineString("GMSpriteFramesTrack"),
                                    Name = pf.DataHandle.DefineString("frames"),
                                    OwnedResources = new(),
                                    OwnedResourceTypes = new(),
                                    Tags = new(),
                                    Tracks = new(),
                                    Traits = GMSequence.Track.TraitsEnum.Unknown1
                                }
                            },
                            FunctionIDs = new(),
                            Moments = new()
                        };

                        var keyframes = newSeq.Tracks[0].Keyframes as GMSequence.Track.ValueKeyframes;
                        foreach (var frame in seq.Frames)
                        {
                            keyframes.List.Add(new()
                            {
                                Disabled = false,
                                Key = frame.Position,
                                Length = frame.Length,
                                Stretch = false,
                                Channels = new()
                                {
                                    {
                                        0,
                                        new()
                                        {
                                            Value = frame.Index
                                        }
                                    }
                                },
                            });
                        }

                        foreach (var msg in seq.BroadcastMessages)
                        {
                            newSeq.BroadcastMessages.Add(new()
                            {
                                Disabled = false,
                                Key = msg.Position,
                                Length = 1,
                                Stretch = false,
                                Channels = new()
                                {
                                    { 
                                        0, 
                                        new() 
                                        { 
                                            List = new() 
                                            { 
                                                pf.DataHandle.DefineString(msg.Message) 
                                            } 
                                        } 
                                    }
                                }
                            });
                        }
                    }

                    if (projectAsset.SpecialInfo.NineSlice != null)
                    {
                        var ns = projectAsset.SpecialInfo.NineSlice;

                        dataAsset.GMS2_3_2_NineSlice = new GMSprite.NineSlice()
                        {
                            Enabled = ns.Enabled,
                            Left = ns.Left,
                            Top = ns.Top,
                            Right = ns.Right,
                            Bottom = ns.Bottom,
                            TileModes = ns.TileModes.ToArray()
                        };
                    }
                }

                newList.Add(dataAsset);
            }

            dataAssets.Clear();
            foreach (var obj in newList)
                dataAssets.Add(obj);
        }
    }
}
