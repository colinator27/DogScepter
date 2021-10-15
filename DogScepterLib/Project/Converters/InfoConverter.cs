using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Converters
{
    public class InfoConverter : IConverter
    {
        public void ConvertData(ProjectFile pf)
        {
            pf.JsonFile.BaseFileLength = pf.DataHandle.Length;
            pf.JsonFile.BaseFileHash = pf.DataHandle.Hash;
            pf.JsonFile.Info = "info.json";
            pf.Info = ConvertInfo(pf);
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
            if (pf.DataHandle.VersionInfo.FormatID >= 14)
                info["DebuggerPort"] = generalInfo.DebuggerPort;

            return info;
        }

        public void ConvertProject(ProjectFile pf)
        {
            GMChunkGEN8 info = pf.DataHandle.GetChunk<GMChunkGEN8>();

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
            info.LicenseMD5 = new BufferRegion(pf.Info.GetBytes("LicenseMD5"));
            info.Timestamp = pf.Info.GetLong("Timestamp");
            info.DisplayName = pf.Info.GetString(pf, "DisplayName");
            info.ActiveTargets = pf.Info.GetLong("ActiveTargets");
            info.FunctionClassifications = Enum.Parse<GMChunkGEN8.FunctionClassification>(pf.Info.GetString("FunctionClassifications"));
            info.SteamAppID = pf.Info.GetInt("SteamAppID");
            info.DebuggerPort = pf.Info.GetInt("DebuggerPort");
        }
    }
}
