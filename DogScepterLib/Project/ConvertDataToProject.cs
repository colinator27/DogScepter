using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project;
using DogScepterLib.Project.Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static DogScepterLib.Core.Models.GMSound;

namespace DogScepterLib.Project
{
    /// <summary>
    /// Converts data file structures into a DogScepter project file structure
    /// </summary>
    public static class ConvertDataToProject
    {
        private delegate CachedRefData _MakeCachedData(GMNamedSerializable asset);

        private static void EmptyRefsForNamed<T>(IEnumerable<GMNamedSerializable> dataAssets, List<AssetRef<T>> projectAssets,
                                                    _MakeCachedData makeCachedData = null) where T : Asset
        {
            int index = 0;
            foreach (GMNamedSerializable asset in dataAssets)
            {
                var assetRef = new AssetRef<T>(asset.Name.Content, index++, asset);
                assetRef.CachedData = makeCachedData?.Invoke(asset) ?? null;
                projectAssets.Add(assetRef);
            }
        }

        public static void FastConvert(ProjectFile pf)
        {
            pf.JsonFile.BaseFileLength = pf.DataHandle.Length;
            pf.JsonFile.BaseFileHash = pf.DataHandle.Hash;
            pf.JsonFile.Info = "info.json";
            pf.Info = ConvertInfo(pf);
            pf.JsonFile.AudioGroups = "audiogroups.json";
            pf.AudioGroups = ConvertAudioGroups(pf);

            EmptyRefsForNamed(pf.DataHandle.GetChunk<GMChunkPATH>().List, pf.Paths);
            EmptyRefsForNamed(pf.DataHandle.GetChunk<GMChunkSOND>().List, pf.Sounds, (asset) =>
            {
                GMSound sound = (GMSound)asset;

                byte[] buff;
                if ((sound.Flags & AudioEntryFlags.IsEmbedded) != AudioEntryFlags.IsEmbedded &&
                    (sound.Flags & AudioEntryFlags.IsCompressed) != AudioEntryFlags.IsCompressed)
                {
                    buff = null;
                }
                else
                    buff = pf._CachedAudioChunks[sound.GroupID].List[sound.AudioID].Data;

                if (pf.AudioGroups == null)
                    return new CachedSoundRefData(buff, "");

                return new CachedSoundRefData(buff,
                                                (sound.GroupID >= 0 && sound.GroupID < pf.AudioGroups.Count) 
                                                    ? pf.AudioGroups[sound.GroupID] : "");
            });
            EmptyRefsForNamed(pf.DataHandle.GetChunk<GMChunkBGND>().List, pf.Backgrounds);
            EmptyRefsForNamed(pf.DataHandle.GetChunk<GMChunkOBJT>().List, pf.Objects);
        }

        public static Dictionary<string, object> ConvertInfo(ProjectFile pf)
        {
            var generalInfo = pf.DataHandle.GetChunk<GMChunkGEN8>();

            Dictionary<string, object> info = new Dictionary<string, object>();

            info["DisableDebug"] = generalInfo.DisableDebug;
            info["FormatID"] = generalInfo.FormatID;
            info["Unknown"] = generalInfo.Unknown;
            info["Filename"] = generalInfo.Filename.Content;
            info["Config"] = generalInfo.Config.Content;
            info["LastObjectID"] = generalInfo.LastObjectID;
            info["LastTileID"] = generalInfo.LastTileID;
            info["GameID"] = generalInfo.GameID;
            if (pf.DataHandle.VersionInfo.IsNumberAtLeast(2))
            {
                info["FPS"] = generalInfo.GMS2_FPS;
                info["AllowStatistics"] = generalInfo.GMS2_AllowStatistics;
                info["GUID"] = generalInfo.GMS2_GameGUID;
            }
            else
                info["GUID"] = generalInfo.LegacyGUID;
            info["Name"] = generalInfo.GameName.Content;
            info["Major"] = generalInfo.Major;
            info["Minor"] = generalInfo.Minor;
            info["Release"] = generalInfo.Release;
            info["Build"] = generalInfo.Build;
            info["DefaultWindowWidth"] = generalInfo.DefaultWindowWidth;
            info["DefaultWindowHeight"] = generalInfo.DefaultWindowHeight;
            info["Info"] = generalInfo.Info.ToString();
            info["LicenseCRC32"] = generalInfo.LicenseCRC32;
            info["LicenseMD5"] = generalInfo.LicenseMD5;
            info["Timestamp"] = generalInfo.Timestamp;
            info["DisplayName"] = generalInfo.DisplayName.Content;
            info["ActiveTargets"] = generalInfo.ActiveTargets;
            info["FunctionClassifications"] = generalInfo.FunctionClassifications.ToString();
            info["SteamAppID"] = generalInfo.SteamAppID;
            info["DebuggerPort"] = generalInfo.DebuggerPort;

            return info;
        }

        public static List<string> ConvertAudioGroups(ProjectFile pf)
        {
            var agrp = pf.DataHandle.GetChunk<GMChunkAGRP>();
            if (agrp == null)
                return null; // format ID <= 13
            var groups = agrp.List;

            // Make a cached map of group IDs to chunks
            pf._CachedAudioChunks = new Dictionary<int, GMChunkAUDO>()
                { { pf.DataHandle.VersionInfo.BuiltinAudioGroupID, pf.DataHandle.GetChunk<GMChunkAUDO>() } };
            if (agrp.AudioData != null)
            {
                for (int i = 1; i < groups.Count; i++)
                {
                    if (agrp.AudioData.ContainsKey(i))
                        pf._CachedAudioChunks.Add(i, agrp.AudioData[i].GetChunk<GMChunkAUDO>());
                }
            }

            // Actually make the list
            List<string> res = new List<string>();
            foreach (GMAudioGroup g in groups)
                res.Add(g.Name.Content);
            return res;
        }

        public static void ConvertPath(ProjectFile pf, int index)
        {
            GMPath asset = (GMPath)pf.Paths[index].DataAsset;

            AssetPath projectAsset = new AssetPath()
            {
                Name = asset.Name.Content,
                Smooth = asset.Smooth,
                Closed = asset.Closed,
                Precision = asset.Precision,
                Points = new List<AssetPath.Point>()
            };
            foreach (GMPath.Point point in asset.Points)
                projectAsset.Points.Add(new AssetPath.Point() { X = point.X, Y = point.Y, Speed = point.Speed });

            pf.Paths[index].Asset = projectAsset;
        }

        public static void ConvertSound(ProjectFile pf, int index)
        {
            GMSound asset = (GMSound)pf.Sounds[index].DataAsset;

            AssetSound projectAsset = new AssetSound()
            {
                Name = asset.Name.Content,
                AudioGroup = ((CachedSoundRefData)pf.Sounds[index].CachedData).AudioGroupName,
                Volume = asset.Volume,
                Pitch = asset.Pitch,
                Type = asset.Type?.Content,
                OriginalSoundFile = asset.File.Content,
                SoundFile = asset.File.Content
            };

            if ((asset.Flags & AudioEntryFlags.IsEmbedded) != AudioEntryFlags.IsEmbedded &&
                (asset.Flags & AudioEntryFlags.IsCompressed) != AudioEntryFlags.IsCompressed)
            {
                // External file
                projectAsset.Attributes = AssetSound.Attribute.CompressedStreamed;

                string soundFilePath = Path.Combine(pf.DataHandle.Directory, asset.File.Content);
                if (!soundFilePath.EndsWith(".ogg") && !soundFilePath.EndsWith(".mp3"))
                    soundFilePath += ".ogg";

                if (File.Exists(soundFilePath))
                {
                    projectAsset.SoundFileBuffer = File.ReadAllBytes(soundFilePath);
                    if (!projectAsset.SoundFile.Contains("."))
                        projectAsset.SoundFile += Path.GetExtension(soundFilePath);
                }
            }
            else
            {
                // Internal file
                projectAsset.SoundFileBuffer = pf._CachedAudioChunks[asset.GroupID].List[asset.AudioID].Data;

                if ((asset.Flags & AudioEntryFlags.IsCompressed) == AudioEntryFlags.IsCompressed)
                {
                    // But compressed!
                    if ((asset.Flags & AudioEntryFlags.IsEmbedded) == AudioEntryFlags.IsEmbedded)
                        projectAsset.Attributes = AssetSound.Attribute.UncompressOnLoad;
                    else
                        projectAsset.Attributes = AssetSound.Attribute.CompressedNotStreamed;
                    if (projectAsset.SoundFileBuffer.Length > 4 && !projectAsset.SoundFile.Contains("."))
                    {
                        if (projectAsset.SoundFileBuffer[0] == 'O' &&
                            projectAsset.SoundFileBuffer[1] == 'g' &&
                            projectAsset.SoundFileBuffer[2] == 'g' &&
                            projectAsset.SoundFileBuffer[3] == 'S')
                        {
                            projectAsset.SoundFile += ".ogg";
                        }
                        else
                            projectAsset.SoundFile += ".mp3";
                    }
                }
                else
                {
                    projectAsset.Attributes = AssetSound.Attribute.Uncompressed;
                    if (!projectAsset.SoundFile.Contains("."))
                        projectAsset.SoundFile += ".wav";
                }
            }

            pf.Sounds[index].Asset = projectAsset;
        }

        public static void ConvertObject(ProjectFile pf, int index)
        {
            // TODO replace both of these to use the AssetRef list instead once the respective assets are loaded literally at all
            var dataSprites = ((GMChunkSPRT)pf.DataHandle.Chunks["SPRT"]).List; 
            var dataCode = ((GMChunkCODE)pf.DataHandle.Chunks["CODE"])?.List;

            GMObject asset = (GMObject)pf.Objects[index].DataAsset;

            AssetObject projectAsset = new AssetObject()
            {
                Name = asset.Name.Content,
                Sprite = asset.SpriteID >= 0 ? dataSprites[asset.SpriteID].Name.Content : null,
                Visible = asset.Visible,
                Solid = asset.Solid,
                Depth = asset.Depth,
                Persistent = asset.Persistent,
                ParentObject = asset.ParentObjectID >= 0 ? pf.Objects[asset.ParentObjectID].Name 
                                    : (asset.ParentObjectID == -100 ? "<undefined>" : null),
                MaskSprite = asset.MaskSpriteID >= 0 ? dataSprites[asset.MaskSpriteID].Name.Content : null,
                Physics = asset.Physics,
                PhysicsSensor = asset.PhysicsSensor,
                PhysicsShape = asset.PhysicsShape,
                PhysicsDensity = asset.PhysicsDensity,
                PhysicsRestitution = asset.PhysicsRestitution,
                PhysicsGroup = asset.PhysicsGroup,
                PhysicsLinearDamping = asset.PhysicsLinearDamping,
                PhysicsAngularDamping = asset.PhysicsAngularDamping,
                PhysicsVertices = new List<AssetObject.PhysicsVertex>(),
                PhysicsFriction = asset.PhysicsFriction,
                PhysicsAwake = asset.PhysicsAwake,
                PhysicsKinematic = asset.PhysicsKinematic,
                Events = new SortedDictionary<AssetObject.EventType, List<AssetObject.Event>>()
            };

            pf.Objects[index].Asset = projectAsset;

            foreach (GMObject.PhysicsVertex v in asset.PhysicsVertices)
                projectAsset.PhysicsVertices.Add(new AssetObject.PhysicsVertex() { X = v.X, Y = v.Y });
            for (int j = 0; j < asset.Events.Count; j++)
            {
                List<AssetObject.Event> projectEvents = new List<AssetObject.Event>();
                AssetObject.EventType type = (AssetObject.EventType)j;
                projectAsset.Events[type] = projectEvents;

                void addActions(GMObject.Event ev, AssetObject.Event newEv)
                {
                    foreach (var ac in ev.Actions)
                    {
                        newEv.Actions.Add(new AssetObject.Action()
                        {
                            Code = (dataCode == null) ? ac.CodeID.ToString() : dataCode[ac.CodeID].Name.Content,
                            ID = ac.ID,
                            UseApplyTo = ac.UseApplyTo,
                            ActionName = ac.ActionName?.Content,
                            ArgumentCount = ac.ArgumentCount
                        });
                    }
                }

                switch (type)
                {
                    case AssetObject.EventType.Alarm:
                        foreach (var ev in asset.Events[j])
                        {
                            var newEv = new AssetObject.EventAlarm()
                            {
                                Actions = new List<AssetObject.Action>(),
                                AlarmNumber = ev.Subtype
                            };
                            addActions(ev, newEv);
                            projectEvents.Add(newEv);
                        }
                        break;
                    case AssetObject.EventType.Step:
                        foreach (var ev in asset.Events[j])
                        {
                            var newEv = new AssetObject.EventStep()
                            {
                                Actions = new List<AssetObject.Action>(),
                                SubtypeStep = (AssetObject.EventStep.Subtype)ev.Subtype
                            };
                            addActions(ev, newEv);
                            projectEvents.Add(newEv);
                        }
                        break;
                    case AssetObject.EventType.Collision:
                        foreach (var ev in asset.Events[j])
                        {
                            var newEv = new AssetObject.EventCollision()
                            {
                                Actions = new List<AssetObject.Action>(),
                                ObjectName = pf.Objects[ev.Subtype].Name
                            };
                            addActions(ev, newEv);
                            projectEvents.Add(newEv);
                        }
                        break;
                    case AssetObject.EventType.Keyboard:
                    case AssetObject.EventType.KeyPress:
                    case AssetObject.EventType.KeyRelease:
                        foreach (var ev in asset.Events[j])
                        {
                            var newEv = new AssetObject.EventKeyboard()
                            {
                                Actions = new List<AssetObject.Action>(),
                                SubtypeKey = (AssetObject.EventKeyboard.Subtype)ev.Subtype
                            };
                            addActions(ev, newEv);
                            projectEvents.Add(newEv);
                        }
                        break;
                    case AssetObject.EventType.Mouse:
                        foreach (var ev in asset.Events[j])
                        {
                            var newEv = new AssetObject.EventMouse()
                            {
                                Actions = new List<AssetObject.Action>(),
                                SubtypeMouse = (AssetObject.EventMouse.Subtype)ev.Subtype
                            };
                            addActions(ev, newEv);
                            projectEvents.Add(newEv);
                        }
                        break;
                    case AssetObject.EventType.Other:
                        foreach (var ev in asset.Events[j])
                        {
                            var newEv = new AssetObject.EventOther()
                            {
                                Actions = new List<AssetObject.Action>(),
                                SubtypeOther = (AssetObject.EventOther.Subtype)ev.Subtype
                            };
                            addActions(ev, newEv);
                            projectEvents.Add(newEv);
                        }
                        break;
                    case AssetObject.EventType.Draw:
                        foreach (var ev in asset.Events[j])
                        {
                            var newEv = new AssetObject.EventDraw()
                            {
                                Actions = new List<AssetObject.Action>(),
                                SubtypeDraw = (AssetObject.EventDraw.Subtype)ev.Subtype
                            };
                            addActions(ev, newEv);
                            projectEvents.Add(newEv);
                        }
                        break;
                    case AssetObject.EventType.Gesture:
                        foreach (var ev in asset.Events[j])
                        {
                            var newEv = new AssetObject.EventGesture()
                            {
                                Actions = new List<AssetObject.Action>(),
                                SubtypeGesture = (AssetObject.EventGesture.Subtype)ev.Subtype
                            };
                            addActions(ev, newEv);
                            projectEvents.Add(newEv);
                        }
                        break;
                    default:
                        foreach (var ev in asset.Events[j])
                        {
                            var newEv = new AssetObject.EventNormal()
                            {
                                Actions = new List<AssetObject.Action>()
                            };
                            addActions(ev, newEv);
                            projectEvents.Add(newEv);
                        }
                        break;
                }
            }
        }
    }
}
