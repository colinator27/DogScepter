using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project;
using DogScepterLib.Project.Assets;
using System;
using System.Collections.Generic;
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
            ConvertPaths(pf);
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
    }
}
