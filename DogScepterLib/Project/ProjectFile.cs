using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DogScepterLib.Project
{
    public class ProjectFile
    {
        public string DirectoryPath;
        public GMData DataHandle;

        public ProjectJson JsonFile;
        public List<AssetPath> Paths;

        public static JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public ProjectFile(GMData dataHandle, string directoryPath)
        {
            DataHandle = dataHandle;
            DirectoryPath = directoryPath;
        }

        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            JsonFile = new ProjectJson();

            SaveInfo((GMChunkGEN8)DataHandle.Chunks["GEN8"]);
            SavePaths(DirectoryPath + "/paths", ((GMChunkPATH)DataHandle.Chunks["PATH"]).List);

            File.WriteAllText(Path.Combine(DirectoryPath, "project.json"), JsonSerializer.Serialize(JsonFile, JsonOptions));
        }

        public void Load()
        {
            JsonFile = JsonSerializer.Deserialize<ProjectJson>(
            File.ReadAllBytes(Path.Combine(DirectoryPath, "project.json")),
            JsonOptions);

            Paths = LoadPaths();
        }

        public void RebuildData()
        {
            RebuildInfo();
            RebuildPaths();
        }

        private void SaveInfo(GMChunkGEN8 generalInfo)
        {
            Dictionary<string, object> info = new Dictionary<string, object>();

            info["DisableDebug"] = generalInfo.DisableDebug;
            info["FormatID"] = generalInfo.FormatID;
            info["Unknown"] = generalInfo.Unknown;
            info["Filename"] = generalInfo.Filename.Content;
            info["Config"] = generalInfo.Config.Content;
            info["LastObjectID"] = generalInfo.LastObjectID;
            info["LastTileID"] = generalInfo.LastTileID;
            info["GameID"] = generalInfo.GameID;
            if (DataHandle.VersionInfo.IsNumberAtLeast(2))
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

            JsonFile.Info = info;
            
        }

        private void SavePaths(string assetsPath, GMList<GMPath> dataAssets)
        {
            if (dataAssets.Count > 0)
                Directory.CreateDirectory(assetsPath);

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
                projectAsset.Write(assetsPath, projectAsset.Name);
                    JsonFile.Assets["Paths"].Add(new ProjectJson.AssetEntry() { Name = projectAsset.Name,
                        Path = Path.Combine($"paths/{projectAsset.Name}.json") });
            }
        }

        private List<AssetPath> LoadPaths()
        {
            List<AssetPath> assetList = new List<AssetPath>();
            Directory.CreateDirectory(Path.Combine(DirectoryPath, "paths"));

            foreach (ProjectJson.AssetEntry entry in JsonFile.Assets["Paths"])
            {
                assetList.Add(AssetPath.Load(DirectoryPath, entry.Path));
            }

            return assetList;
        }

        private void RebuildInfo()
        {
            GMChunkGEN8 info = (GMChunkGEN8)DataHandle.Chunks["GEN8"];

            int GetInt(string propertyName) { return ((JsonElement)JsonFile.Info[propertyName]).GetInt32(); }
            GMString GetString(string propertyName) { return DataHandle.DefineString(((JsonElement)JsonFile.Info[propertyName]).GetString()); }

            info.DisableDebug = ((JsonElement)JsonFile.Info["DisableDebug"]).GetBoolean();
            info.FormatID = ((JsonElement)JsonFile.Info["FormatID"]).GetByte();
            info.Unknown = ((JsonElement)JsonFile.Info["Unknown"]).GetInt16();
            info.Filename = GetString("Filename");
            info.Config = GetString("Config");
            info.LastObjectID = GetInt("LastObjectID");
            info.LastTileID = GetInt("LastTileID");
            info.GameID = GetInt("GameID");
            if (DataHandle.VersionInfo.IsNumberAtLeast(2))
            {
                info.GMS2_FPS = ((JsonElement)JsonFile.Info["FPS"]).GetSingle();
                info.GMS2_AllowStatistics = ((JsonElement)JsonFile.Info["AllowStatistics"]).GetBoolean();
                info.GMS2_GameGUID = ((JsonElement)JsonFile.Info["GUID"]).GetGuid();
            }
            else
                info.LegacyGUID = ((JsonElement)JsonFile.Info["GUID"]).GetGuid();
            info.GameName = GetString("Name");
            info.Major = GetInt("Major");
            info.Minor = GetInt("Minor");
            info.Release = GetInt("Release");
            info.Build = GetInt("Build");
            info.DefaultWindowWidth = GetInt("DefaultWindowWidth");
            info.DefaultWindowHeight = GetInt("DefaultWindowHeight");
            info.Info = Enum.Parse<GMChunkGEN8.InfoFlags>(((JsonElement)JsonFile.Info["Info"]).GetString());
            info.LicenseCRC32 = GetInt("LicenseCRC32");
            info.LicenseMD5 = ((JsonElement)JsonFile.Info["LicenseMD5"]).GetBytesFromBase64();
            info.Timestamp = ((JsonElement)JsonFile.Info["Timestamp"]).GetInt64();
            info.DisplayName = GetString("DisplayName");
            info.ActiveTargets = ((JsonElement)JsonFile.Info["ActiveTargets"]).GetInt64();
            info.FunctionClassifications = Enum.Parse<GMChunkGEN8.FunctionClassification>(((JsonElement)JsonFile.Info["FunctionClassifications"]).GetString());
            info.SteamAppID = GetInt("SteamAppID");
            info.DebuggerPort = GetInt("DebuggerPort");
        }

        private void RebuildPaths()
        {
            GMList<GMPath> dataAssets = ((GMChunkPATH)DataHandle.Chunks["PATH"]).List;

            dataAssets.Clear();
            for (int i = 0; i < Paths.Count; i++)
            {
                AssetPath assetPath = Paths[i];
                dataAssets.Add(new GMPath()
                    {
                    Name = DataHandle.DefineString(assetPath.Name),
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
    }

    public class ProjectJson
    {
        public struct AssetEntry
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        public int Version { get; private set; } = 1;
        public Dictionary<string, List<AssetEntry>> Assets { get; set; }
        public Dictionary<string, object> Info { get; set; }

        public ProjectJson()
        {
            Assets = new Dictionary<string, List<AssetEntry>>();
            Assets.Add("Paths", new List<AssetEntry>());
        }
    }
}
