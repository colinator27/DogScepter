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
using System.Threading.Tasks;
using static DogScepterLib.Core.Models.GMSound;

namespace DogScepterLib.Project
{
    /// <summary>
    /// Converts data file structures into a DogScepter project file structure
    /// </summary>
    public static class ConvertDataToProject
    {
        public static void Convert(ProjectFile pf)
        {
            pf.JsonFile.BaseFileLength = pf.DataHandle.Length;
            pf.JsonFile.BaseFileHash = pf.DataHandle.Hash;
            pf.JsonFile.Info = ConvertInfo(pf);
            pf.JsonFile.AudioGroups = ConvertAudioGroups(pf);
            pf.Paths = ConvertPaths(pf.DataHandle).OfType<AssetPath>().ToList();
            pf.Sounds = ConvertSounds(pf.DataHandle).OfType<AssetSound>().ToList();
        }

        private static Dictionary<string, object> ConvertInfo(ProjectFile pf)
        {
            GMChunkGEN8 generalInfo = (GMChunkGEN8)pf.DataHandle.Chunks["GEN8"];

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

        private static List<string> ConvertAudioGroups(ProjectFile pf)
        {
            GMChunkAGRP groups = (GMChunkAGRP)pf.DataHandle.Chunks["AGRP"];

            List<string> res = new List<string>();
            foreach (GMAudioGroup g in groups.List)
                res.Add(g.Name.Content);
            return res;
        }

        public static List<Asset> ConvertPaths(GMData data)
        {
            var dataAssets = ((GMChunkPATH)data.Chunks["PATH"]).List;
            List<Asset> list = new List<Asset>();
            for (int i = 0; i < dataAssets.Count; i++)
            {
                GMPath asset = dataAssets[i];
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
                list.Add(projectAsset);
            }
            return list;
        }

        public static List<Asset> ConvertSounds(GMData data)
        {
            var dataAssets = ((GMChunkSOND)data.Chunks["SOND"]).List;
            var agrp = (GMChunkAGRP)data.Chunks["AGRP"];
            var groups = agrp.List;

            // Get all the AUDO chunk handles in the game
            Dictionary<int, GMChunkAUDO> audioChunks = new Dictionary<int, GMChunkAUDO>() { { data.VersionInfo.BuiltinAudioGroupID, (GMChunkAUDO)data.Chunks["AUDO"] } };
            if (agrp.AudioData != null)
            {
                for (int i = 1; i < groups.Count; i++)
                {
                    if (agrp.AudioData.ContainsKey(i))
                        audioChunks.Add(i, (GMChunkAUDO)agrp.AudioData[i].Chunks["AUDO"]);
                }
            }

            List<Asset> list = new List<Asset>();
            for (int i = 0; i < dataAssets.Count; i++)
            {
                GMSound asset = dataAssets[i];
                AssetSound projectAsset = new AssetSound()
                {
                    Name = asset.Name.Content,
                    AudioGroup = (asset.GroupID >= 0 && asset.GroupID < groups.Count) ? groups[asset.GroupID].Name.Content : "",
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

                    string soundFilePath = Path.Combine(data.Directory, asset.File.Content);
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
                    projectAsset.SoundFileBuffer = audioChunks[asset.GroupID].List[asset.AudioID].Data;

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

                list.Add(projectAsset);
            }
            return list;
        }
    }
}
