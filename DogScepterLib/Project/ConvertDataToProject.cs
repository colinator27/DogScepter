using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project;
using DogScepterLib.Project.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project
{
    public static class ConvertDataToProject
    {
        public static void Convert(ProjectFile pf)
        {
            pf.JsonFile.BaseFileLength = pf.DataHandle.Length;
            pf.JsonFile.BaseFileHash = pf.DataHandle.Hash;
            pf.JsonFile.Info = ConvertInfo(pf);
            pf.Paths = ConvertPaths(pf.DataHandle);
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

        public static List<AssetPath> ConvertPaths(GMData data)
        {
            var dataAssets = ((GMChunkPATH)data.Chunks["PATH"]).List;
            List<AssetPath> list = new List<AssetPath>();
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
    }
}
