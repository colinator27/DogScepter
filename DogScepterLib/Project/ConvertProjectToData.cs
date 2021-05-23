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
            if (!Directory.Exists(pf.DataHandle.Directory))
                throw new Exception($"Missing output directory \"{pf.DataHandle.Directory}\"");

            pf.DataHandle.BuildStringCache();

            ConvertInfo(pf);
            ConvertAudioGroups(pf);
            ConvertTextureGroups(pf);
            ConvertPaths(pf);
            ConvertSounds(pf);
            // TODO sprites need to be converted before objects
            // TODO future note: sprite/font/tileset/etc. IDs need to be updated in TGIN
            pf.Textures.RegenerateTextures();
            ConvertObjects(pf);
            CopyDataFiles(pf);
        }

        public static void CopyDataFiles(ProjectFile pf)
        {
            if (pf.JsonFile.DataFiles != null && pf.JsonFile.DataFiles.Trim() != "")
            {
                string dataFileDir = Path.Combine(pf.DirectoryPath, pf.JsonFile.DataFiles);
                if (Directory.Exists(dataFileDir))
                {
                    void CopyFiles(DirectoryInfo source, DirectoryInfo target)
                    {
                        foreach (DirectoryInfo subDir in source.GetDirectories())
                            CopyFiles(subDir, target.CreateSubdirectory(subDir.Name));
                        foreach (FileInfo file in source.GetFiles())
                        {
                            pf.DataHandle.Logger?.Invoke($"Writing data file \"{file.Name}\"...");
                            file.CopyTo(Path.Combine(target.FullName, file.Name), true);
                        }
                    }

                    CopyFiles(new DirectoryInfo(dataFileDir), new DirectoryInfo(pf.DataHandle.Directory));
                }
            }
        }

        private static int GetInt(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetInt32();
            return (int)o;
        }

        private static long GetLong(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetInt64();
            return (long)o;
        }

        private static short GetShort(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetInt16();
            return (short)o;
        }

        private static byte GetByte(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetByte();
            return (byte)o;
        }

        private static bool GetBool(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetBoolean();
            return (bool)o;
        }

        private static float GetFloat(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetSingle();
            return (float)o;
        }

        private static Guid GetGUID(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetGuid();
            return (Guid)o;
        }

        private static string GetString(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetString();
            return (string)o;
        }

        private static byte[] GetBytes(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetBytesFromBase64();
            return (byte[])o;
        }

        private static GMString GetString(this Dictionary<string, object> dict, ProjectFile pf, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return pf.DataHandle.DefineString(((JsonElement)dict[name]).GetString());
            return pf.DataHandle.DefineString((string)o);
        }

        private static void ConvertInfo(ProjectFile pf)
        {
            GMChunkGEN8 info = (GMChunkGEN8)pf.DataHandle.Chunks["GEN8"];

            info.DisableDebug = pf.Info.GetBool("DisableDebug");
            info.FormatID = pf.Info.GetByte("FormatID");
            info.Unknown = pf.Info.GetShort("Unknown");
            info.Filename = pf.Info.GetString(pf, "Filename");
            info.Config = pf.Info.GetString(pf, "Config");
            info.LastObjectID = pf.Info.GetInt("LastObjectID");
            info.LastTileID = pf.Info.GetInt("LastTileID");
            info.GameID = pf.Info.GetInt("GameID");
            if (pf.DataHandle.VersionInfo.IsNumberAtLeast(2))
            {
                if (pf.Info.ContainsKey("FPS"))
                {
                    info.GMS2_FPS = pf.Info.GetFloat("FPS");
                    info.GMS2_AllowStatistics = pf.Info.GetBool("AllowStatistics");
                    info.GMS2_GameGUID = pf.Info.GetGUID("GUID");
                }
            }
            else
                info.LegacyGUID = pf.Info.GetGUID("GUID");
            info.GameName = pf.Info.GetString(pf, "Name");
            info.Major = pf.Info.GetInt("Major");
            info.Minor = pf.Info.GetInt("Minor");
            info.Release = pf.Info.GetInt("Release");
            info.Build = pf.Info.GetInt("Build");
            info.DefaultWindowWidth = pf.Info.GetInt("DefaultWindowWidth");
            info.DefaultWindowHeight = pf.Info.GetInt("DefaultWindowHeight");
            info.Info = Enum.Parse<GMChunkGEN8.InfoFlags>(pf.Info.GetString("Info"));
            info.LicenseCRC32 = pf.Info.GetInt("LicenseCRC32");
            info.LicenseMD5 = pf.Info.GetBytes("LicenseMD5");
            info.Timestamp = pf.Info.GetLong("Timestamp");
            info.DisplayName = pf.Info.GetString(pf, "DisplayName");
            info.ActiveTargets = pf.Info.GetLong("ActiveTargets");
            info.FunctionClassifications = Enum.Parse<GMChunkGEN8.FunctionClassification>(pf.Info.GetString("FunctionClassifications"));
            info.SteamAppID = pf.Info.GetInt("SteamAppID");
            info.DebuggerPort = pf.Info.GetInt("DebuggerPort");
        }

        private static void ConvertAudioGroups(ProjectFile pf)
        {
            GMChunkAGRP groups = pf.DataHandle.GetChunk<GMChunkAGRP>();
            if (groups == null || pf.AudioGroups == null)
                return;

            groups.List.Clear();
            int ind = 0;
            foreach (string g in pf.AudioGroups)
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

        private static void ConvertTextureGroups(ProjectFile pf)
        {
            // Sort the texture pages by their ID
            List<int> newGroupIDs = new List<int>();
            Dictionary<int, ProjectJson.TextureGroup> newGroups = new Dictionary<int, ProjectJson.TextureGroup>();

            int highest = -1;
            foreach (var group in pf.TextureGroups)
            {
                int thisId = group.ID;

                if (newGroupIDs.Contains(thisId))
                {
                    // Duplicate ID? Go one above the highest instead
                    pf.DataHandle.Logger?.Invoke($"Warning: overlapping texture group ID {thisId}; changing it automatically.");
                    thisId = highest + 1;
                }
                newGroupIDs.Add(thisId);

                if (thisId > highest)
                    highest = thisId;

                newGroups[thisId] = group;
            }
            newGroupIDs.Sort();

            var tginList = pf.DataHandle.GetChunk<GMChunkTGIN>()?.List;
            int tginEnd = tginList?.Count ?? 0;

            // Add new pages to the end
            int i;
            for (i = newGroups.Count - 1; i >= pf.Textures.TextureGroups.Count; i--)
            {
                var groupInfo = newGroups[newGroupIDs[i]];
                pf.Textures.TextureGroups.Add(new Textures.Group() 
                { 
                    Dirty = true,
                    Border = groupInfo.Border,
                    AllowCrop = groupInfo.AllowCrop
                });

                // Add new TGIN entries, will be filled out with more later
                tginList?.Insert(tginEnd, new GMTextureGroupInfo()
                {
                    Name = pf.DataHandle.DefineString(groupInfo.Name)
                });
            }

            // Handle changing properties on other pages
            for (; i >= 0; i--)
            {
                var groupInfo = newGroups[newGroupIDs[i]];
                var group = pf.Textures.TextureGroups[i];
                if (group.Border != groupInfo.Border || group.AllowCrop != groupInfo.AllowCrop)
                {
                    group.Dirty = true;
                    group.Border = groupInfo.Border;
                    group.AllowCrop = groupInfo.AllowCrop;
                }

                if (tginList != null)
                {
                    // Find TGIN entry
                    GMTextureGroupInfo resultInfo = null;
                    foreach (var info in tginList)
                    {
                        if (info.TexturePageIDs.Any(j => group.Pages.Contains(j.ID)))
                        {
                            resultInfo = info;
                            break;
                        }
                    }

                    // Update name
                    resultInfo.Name = pf.DataHandle.DefineString(groupInfo.Name);
                }
            }
        }

        private static void ConvertPaths(ProjectFile pf)
        {
            GMList<GMPath> dataAssets = pf.DataHandle.GetChunk<GMChunkPATH>().List;

            dataAssets.Clear();
            for (int i = 0; i < pf.Paths.Count; i++)
            {
                AssetPath assetPath = pf.Paths[i].Asset;
                if (assetPath == null)
                {
                    // This asset was never converted, so handle references and re-add it
                    GMPath p = (GMPath)pf.Paths[i].DataAsset;
                    p.Name = pf.DataHandle.DefineString(p.Name.Content);
                    dataAssets.Add(p);
                    continue;
                }

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
            var dataAssets = pf.DataHandle.GetChunk<GMChunkSOND>().List;
            var agrp = pf.DataHandle.GetChunk<GMChunkAGRP>();
            var groups = agrp.List;

            bool updatedVersion = pf.DataHandle.VersionInfo.IsNumberAtLeast(1, 0, 0, 9999);

            // First, sort sounds alphabetically
            List<AssetRef<AssetSound>> sortedSounds = updatedVersion ? pf.Sounds.OrderBy(x => x.Name).ToList() : pf.Sounds;

            // Get all the AUDO chunk handles in the game
            GMChunkAUDO defaultChunk = pf.DataHandle.GetChunk<GMChunkAUDO>();
            defaultChunk.List.Clear();
            Dictionary<string, GMChunkAUDO> audioChunks = new Dictionary<string, GMChunkAUDO>();
            Dictionary<string, int> audioChunkIndices = new Dictionary<string, int>();
            if (agrp.AudioData != null)
            {
                for (int i = 1; i < groups.Count; i++)
                {
                    if (agrp.AudioData.ContainsKey(i))
                    {
                        var currChunk = agrp.AudioData[i].GetChunk<GMChunkAUDO>();
                        currChunk.List.Clear();
                        audioChunks.Add(groups[i].Name.Content, currChunk);
                        audioChunkIndices.Add(groups[i].Name.Content, i);
                    }
                }
            }

            dataAssets.Clear();
            Dictionary<AssetRef<AssetSound>, GMSound> finalMap = new Dictionary<AssetRef<AssetSound>, GMSound>();
            for (int i = 0; i < sortedSounds.Count; i++)
            {
                AssetSound asset = sortedSounds[i].Asset;
                if (asset == null)
                {
                    // This asset was never converted, so handle references and re-add it
                    GMSound s = (GMSound)sortedSounds[i].DataAsset;
                    s.Name = pf.DataHandle.DefineString(s.Name.Content);
                    s.File = pf.DataHandle.DefineString(s.File.Content);
                    if (s.Type != null)
                        s.Type = pf.DataHandle.DefineString(s.Type.Content);

                    // Get the group name from the cache
                    var cachedData = (CachedSoundRefData)sortedSounds[i].CachedData;

                    // Potentially handle the internal sound buffer
                    if (cachedData.SoundBuffer != null)
                    {
                        string groupName = cachedData.AudioGroupName;

                        int ind;
                        GMChunkAUDO chunk;
                        if (!audioChunkIndices.TryGetValue(groupName, out ind))
                        {
                            ind = pf.DataHandle.VersionInfo.BuiltinAudioGroupID; // might be wrong
                            chunk = defaultChunk;
                        }
                        else
                            chunk = audioChunks[groupName];

                        s.GroupID = ind;
                        s.AudioID = chunk.List.Count;
                        chunk.List.Add(new GMAudio() { Data = cachedData.SoundBuffer });
                    }

                    finalMap[sortedSounds[i]] = s;
                    continue;
                }

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
                finalMap[sortedSounds[i]] = dataAsset;

                switch (asset.Attributes)
                {
                    case AssetSound.Attribute.CompressedStreamed:
                        if (updatedVersion)
                            dataAsset.AudioID = -1;
                        else
                            dataAsset.AudioID = defaultChunk.List.Count - 1;
                        dataAsset.GroupID = pf.DataHandle.VersionInfo.BuiltinAudioGroupID; // might be wrong

                        if (asset.SoundFileBuffer != null)
                        {
                            pf.DataHandle.Logger?.Invoke($"Writing sound file \"{asset.SoundFile}\"...");
                            pf.DataHandle.FileWrites.Post(new KeyValuePair<string, byte[]>(Path.Combine(pf.DataHandle.Directory, asset.SoundFile), asset.SoundFileBuffer));
                        }
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
            foreach (var assetRef in pf.Sounds)
            {
                dataAssets.Add(finalMap[assetRef]);
            }
        }

        private static void ConvertObjects(ProjectFile pf)
        {
            var dataAssets = pf.DataHandle.GetChunk<GMChunkOBJT>().List;

            // TODO use refs once added, probably
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
                    return pf.Objects.Select((elem, index) => new { elem, index }).First(p => p.elem.Name == name).index;
                }
                catch (InvalidOperationException)
                {
                    return -1;
                }
            }

            List<GMObject> newList = new List<GMObject>();
            for (int i = 0; i < pf.Objects.Count; i++)
            {
                AssetObject projectAsset = pf.Objects[i].Asset;
                if (projectAsset == null)
                {
                    // This asset was never converted, so handle references and re-add it
                    GMObject o = (GMObject)pf.Objects[i].DataAsset;
                    o.Name = pf.DataHandle.DefineString(o.Name.Content);
                    foreach (var evList in o.Events)
                    {
                        foreach (var ev in evList)
                        {
                            foreach (var ac in ev.Actions)
                            {
                                if (ac.ActionName != null)
                                    ac.ActionName = pf.DataHandle.DefineString(ac.ActionName.Content);
                            }
                        }
                    }
                    newList.Add(o);
                    continue;
                }

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
