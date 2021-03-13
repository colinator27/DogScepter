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
                        dataAsset.Flags |= GMSound.AudioEntryFlags.IsEmbedded;
                        goto case AssetSound.Attribute.CompressedNotStreamed;
                    case AssetSound.Attribute.CompressedNotStreamed:
                        dataAsset.Flags |= GMSound.AudioEntryFlags.IsCompressed;
                        goto case AssetSound.Attribute.Uncompressed;
                    case AssetSound.Attribute.Uncompressed:
                        dataAsset.Flags |= GMSound.AudioEntryFlags.IsEmbedded;

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
    }
}
