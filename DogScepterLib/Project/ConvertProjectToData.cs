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

namespace DogScepterLib.Project
{
    /// <summary>
    /// Converts DogScepter project data into proper GameMaker format
    /// </summary>
    public static class ConvertProjectToData
    {
        public static void Convert(ProjectFile pf)
        {
            ConvertInfo(pf);
            ConvertAudioGroups(pf);
            ConvertPaths(pf);
            ConvertSounds(pf);
            // TODO sprites need to be converted before objects
            ConvertObjects(pf);
        }

        private static void ConvertInfo(ProjectFile pf)
        {
            GMChunkGEN8 info = (GMChunkGEN8)pf.DataHandle.Chunks["GEN8"];

            int GetInt(string propertyName) { return ((JsonElement)pf.JsonFile.Info[propertyName]).GetInt32(); }
            GMString GetString(string propertyName) { return pf.DataHandle.DefineString(((JsonElement)pf.JsonFile.Info[propertyName]).GetString()); }

            info.DisableDebug = ((JsonElement)pf.JsonFile.Info["DisableDebug"]).GetBoolean();
            info.FormatID = ((JsonElement)pf.JsonFile.Info["FormatID"]).GetByte();
            info.Unknown = ((JsonElement)pf.JsonFile.Info["Unknown"]).GetInt16();
            info.Filename = GetString("Filename");
            info.Config = GetString("Config");
            info.LastObjectID = GetInt("LastObjectID");
            info.LastTileID = GetInt("LastTileID");
            info.GameID = GetInt("GameID");
            if (pf.DataHandle.VersionInfo.IsNumberAtLeast(2))
            {
                info.GMS2_FPS = ((JsonElement)pf.JsonFile.Info["FPS"]).GetSingle();
                info.GMS2_AllowStatistics = ((JsonElement)pf.JsonFile.Info["AllowStatistics"]).GetBoolean();
                info.GMS2_GameGUID = ((JsonElement)pf.JsonFile.Info["GUID"]).GetGuid();
            }
            else
                info.LegacyGUID = ((JsonElement)pf.JsonFile.Info["GUID"]).GetGuid();
            info.GameName = GetString("Name");
            info.Major = GetInt("Major");
            info.Minor = GetInt("Minor");
            info.Release = GetInt("Release");
            info.Build = GetInt("Build");
            info.DefaultWindowWidth = GetInt("DefaultWindowWidth");
            info.DefaultWindowHeight = GetInt("DefaultWindowHeight");
            info.Info = Enum.Parse<GMChunkGEN8.InfoFlags>(((JsonElement)pf.JsonFile.Info["Info"]).GetString());
            info.LicenseCRC32 = GetInt("LicenseCRC32");
            info.LicenseMD5 = ((JsonElement)pf.JsonFile.Info["LicenseMD5"]).GetBytesFromBase64();
            info.Timestamp = ((JsonElement)pf.JsonFile.Info["Timestamp"]).GetInt64();
            info.DisplayName = GetString("DisplayName");
            info.ActiveTargets = ((JsonElement)pf.JsonFile.Info["ActiveTargets"]).GetInt64();
            info.FunctionClassifications = Enum.Parse<GMChunkGEN8.FunctionClassification>(((JsonElement)pf.JsonFile.Info["FunctionClassifications"]).GetString());
            info.SteamAppID = GetInt("SteamAppID");
            info.DebuggerPort = GetInt("DebuggerPort");
        }

        private static void ConvertAudioGroups(ProjectFile pf)
        {
            GMChunkAGRP groups = (GMChunkAGRP)pf.DataHandle.Chunks["AGRP"];

            groups.List.Clear();
            int ind = 0;
            foreach (string g in pf.JsonFile.AudioGroups)
            {
                if (groups.AudioData != null && ind != 0 && !groups.AudioData.ContainsKey(ind))
                {
                    // Well now we have to make a new group file
                    GMData data = new GMData()
                    {
                        Length = 1024 * 1024 // just a random default
                    };
                    data.FORM = new GMChunkFORM()
                    {
                        ChunkNames = new List<string>() { "AUDO" },
                        Chunks = new Dictionary<string, GMChunk>()
                        {
                            { "AUDO", new GMChunkAUDO() { List = new GMPointerList<GMAudio>() } }
                        }
                    };
                    groups.AudioData[ind] = data;
                }

                groups.List.Add(new GMAudioGroup()
                {
                    Name = pf.DataHandle.DefineString(g)
                });

                ind++;
            }
        }

        private static void ConvertPaths(ProjectFile pf)
        {
            GMList<GMPath> dataAssets = ((GMChunkPATH)pf.DataHandle.Chunks["PATH"]).List;

            dataAssets.Clear();
            for (int i = 0; i < pf.Paths.Count; i++)
            {
                AssetPath assetPath = pf.Paths[i];
                dataAssets.Add(new GMPath()
                {
                    Name = pf.DataHandle.DefineString(assetPath.Name),
                    Smooth = assetPath.Smooth,
                    Closed = assetPath.Closed,
                    Precision = assetPath.Precision,
                    Points = new GMList<GMPath.Point>()
                });

                GMPath gmPath = dataAssets[dataAssets.Count - 1];
                foreach (AssetPath.Point point in assetPath.Points)
                    gmPath.Points.Add(new GMPath.Point() { X = point.X, Y = point.Y, Speed = point.Speed });
            }
        }

        private static void ConvertSounds(ProjectFile pf)
        {
            var dataAssets = ((GMChunkSOND)pf.DataHandle.Chunks["SOND"]).List;
            var agrp = (GMChunkAGRP)pf.DataHandle.Chunks["AGRP"];
            var groups = agrp.List;

            bool updatedVersion = pf.DataHandle.VersionInfo.IsNumberAtLeast(1, 0, 0, 9999);

            // First, sort sounds alphabetically
            List<AssetSound> sortedSounds = updatedVersion ? pf.Sounds.OrderBy(x => x.Name).ToList() : pf.Sounds;

            // Get all the AUDO chunk handles in the game
            GMChunkAUDO defaultChunk = (GMChunkAUDO)pf.DataHandle.Chunks["AUDO"];
            defaultChunk.List.Clear();
            Dictionary<string, GMChunkAUDO> audioChunks = new Dictionary<string, GMChunkAUDO>();
            Dictionary<string, int> audioChunkIndices = new Dictionary<string, int>();
            if (agrp.AudioData != null)
            {
                for (int i = 1; i < groups.Count; i++)
                {
                    if (agrp.AudioData.ContainsKey(i))
                    {
                        var currChunk = (GMChunkAUDO)agrp.AudioData[i].Chunks["AUDO"];
                        currChunk.List.Clear();
                        audioChunks.Add(groups[i].Name.Content, currChunk);
                        audioChunkIndices.Add(groups[i].Name.Content, i);
                    }
                }
            }

            dataAssets.Clear();
            Dictionary<AssetSound, GMSound> finalMap = new Dictionary<AssetSound, GMSound>();
            for (int i = 0; i < sortedSounds.Count; i++)
            {
                AssetSound asset = sortedSounds[i];
                GMSound dataAsset = new GMSound()
                {
                    Name = pf.DataHandle.DefineString(asset.Name),
                    Volume = asset.Volume,
                    Flags = GMSound.AudioEntryFlags.Regular,
                    Effects = 0,
                    Pitch = asset.Pitch,
                    File = pf.DataHandle.DefineString(asset.OriginalSoundFile),
                    Type = (asset.Type != null) ? pf.DataHandle.DefineString(asset.Type) : null
                };
                finalMap[asset] = dataAsset;

                switch (asset.Attributes)
                {
                    case AssetSound.Attribute.CompressedStreamed:
                        if (updatedVersion)
                            dataAsset.AudioID = -1;
                        else
                            dataAsset.AudioID = defaultChunk.List.Count - 1;
                        dataAsset.GroupID = pf.DataHandle.VersionInfo.BuiltinAudioGroupID; // might be wrong

                        File.WriteAllBytes(Path.Combine(pf.DataHandle.Directory, asset.SoundFile), asset.SoundFileBuffer);
                        break;
                    case AssetSound.Attribute.UncompressOnLoad:
                    case AssetSound.Attribute.Uncompressed:
                        dataAsset.Flags |= GMSound.AudioEntryFlags.IsEmbedded;
                        goto case AssetSound.Attribute.CompressedNotStreamed;
                    case AssetSound.Attribute.CompressedNotStreamed:
                        if (asset.Attributes != AssetSound.Attribute.Uncompressed)
                            dataAsset.Flags |= GMSound.AudioEntryFlags.IsCompressed;

                        int ind;
                        GMChunkAUDO chunk;
                        if (!audioChunkIndices.TryGetValue(asset.AudioGroup, out ind))
                        {
                            ind = pf.DataHandle.VersionInfo.BuiltinAudioGroupID; // might be wrong
                            chunk = defaultChunk;
                        } else
                            chunk = audioChunks[asset.AudioGroup];

                        dataAsset.GroupID = ind;
                        dataAsset.AudioID = chunk.List.Count;
                        chunk.List.Add(new GMAudio() { Data = asset.SoundFileBuffer });
                        break;
                }
            }

            // Actually add sounds to the data
            foreach (AssetSound snd in pf.Sounds)
            {
                dataAssets.Add(finalMap[snd]);
            }
        }

        private static void ConvertObjects(ProjectFile pf)
        {
            GMList<GMObject> dataAssets = ((GMChunkOBJT)pf.DataHandle.Chunks["OBJT"]).List;
            GMList<GMSprite> dataSprites = ((GMChunkSPRT)pf.DataHandle.Chunks["SPRT"]).List;
            GMList<GMCode> dataCode = ((GMChunkCODE)pf.DataHandle.Chunks["CODE"]).List;

            int getSprite(string name)
            {
                if (name == null)
                    return -1;
                try
                {
                    return dataSprites.Select((elem, index) => new { elem, index }).First(p => p.elem.Name.Content == name).index;
                }
                catch (InvalidOperationException)
                {
                    return -1;
                }
            }
            int getCode(string name)
            {
                try
                {
                    return dataCode.Select((elem, index) => new { elem, index }).First(p => p.elem.Name.Content == name).index;
                }
                catch (InvalidOperationException)
                {
                    return -1;
                }
            }
            int getObject(string name)
            {
                if (name == null)
                    return -1;
                if (name == "<undefined>")
                    return -100;
                try
                {
                    return dataAssets.Select((elem, index) => new { elem, index }).First(p => p.elem.Name.Content == name).index;
                }
                catch (InvalidOperationException)
                {
                    return -1;
                }
            }

            List<GMObject> newList = new List<GMObject>();
            for (int i = 0; i < pf.Objects.Count; i++)
            {
                AssetObject projectAsset = pf.Objects[i];
                GMObject dataAsset = new GMObject()
                {
                    Name = pf.DataHandle.DefineString(projectAsset.Name),
                    SpriteID = getSprite(projectAsset.Sprite),
                    Visible = projectAsset.Visible,
                    Solid = projectAsset.Solid,
                    Depth = projectAsset.Depth,
                    Persistent = projectAsset.Persistent,
                    ParentObjectID = getObject(projectAsset.ParentObject),
                    MaskSpriteID = getSprite(projectAsset.MaskSprite),
                    Physics = projectAsset.Physics,
                    PhysicsSensor = projectAsset.PhysicsSensor,
                    PhysicsShape = projectAsset.PhysicsShape,
                    PhysicsDensity = projectAsset.PhysicsDensity,
                    PhysicsRestitution = projectAsset.PhysicsRestitution,
                    PhysicsGroup = projectAsset.PhysicsGroup,
                    PhysicsLinearDamping = projectAsset.PhysicsLinearDamping,
                    PhysicsAngularDamping = projectAsset.PhysicsAngularDamping,
                    PhysicsVertices = new List<GMObject.PhysicsVertex>(),
                    PhysicsFriction = projectAsset.PhysicsFriction,
                    PhysicsAwake = projectAsset.PhysicsAwake,
                    PhysicsKinematic = projectAsset.PhysicsKinematic,
                    Events = new GMPointerList<GMPointerList<GMObject.Event>>()
                };

                foreach (AssetObject.PhysicsVertex v in projectAsset.PhysicsVertices)
                    dataAsset.PhysicsVertices.Add(new GMObject.PhysicsVertex() { X = v.X, Y = v.Y });

                foreach (var events in projectAsset.Events.Values)
                {
                    var newEvents = new GMPointerList<GMObject.Event>();
                    foreach (var ev in events)
                    {
                        GMObject.Event newEv = new GMObject.Event()
                        {
                            Subtype = 0,
                            Actions = new GMPointerList<GMObject.Event.Action>()
                            {
                                new GMObject.Event.Action()
                                {
                                    LibID = 1,
                                    ID = ev.Actions[0].ID,
                                    Kind = 7,
                                    UseRelative = false,
                                    IsQuestion = false,
                                    UseApplyTo = ev.Actions[0].UseApplyTo,
                                    ExeType = 2,
                                    ActionName = ev.Actions[0].ActionName != null ? pf.DataHandle.DefineString(ev.Actions[0].ActionName) : null,
                                    CodeID = getCode(ev.Actions[0].Code),
                                    ArgumentCount = ev.Actions[0].ArgumentCount,
                                    Who = -1,
                                    Relative = false,
                                    IsNot = false
                                }
                            }
                        };

                        // Handle subtype
                        switch (ev)
                        {
                            case AssetObject.EventAlarm e:
                                newEv.Subtype = e.AlarmNumber;
                                break;
                            case AssetObject.EventStep e:
                                newEv.Subtype = (int)e.SubtypeStep;
                                break;
                            case AssetObject.EventCollision e:
                                newEv.Subtype = getObject(e.ObjectName);
                                break;
                            case AssetObject.EventKeyboard e:
                                newEv.Subtype = (int)e.SubtypeKey;
                                break;
                            case AssetObject.EventMouse e:
                                newEv.Subtype = (int)e.SubtypeMouse;
                                break;
                            case AssetObject.EventOther e:
                                newEv.Subtype = (int)e.SubtypeOther;
                                break;
                            case AssetObject.EventDraw e:
                                newEv.Subtype = (int)e.SubtypeDraw;
                                break;
                            case AssetObject.EventGesture e:
                                newEv.Subtype = (int)e.SubtypeGesture;
                                break;
                        }
                        newEvents.Add(newEv);
                    }
                    dataAsset.Events.Add(newEvents);
                }

                newList.Add(dataAsset);
            }

            dataAssets.Clear();
            foreach (var obj in newList)
                dataAssets.Add(obj);
        }
    }
}
