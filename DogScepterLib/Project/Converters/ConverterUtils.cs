using DogScepterLib.Core;
using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Converters
{
    public static class ConverterUtils
    {
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

        public static int GetInt(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return elem.GetInt32();
            return (int)o;
        }

        public static long GetLong(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return elem.GetInt64();
            return (long)o;
        }

        public static short GetShort(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return elem.GetInt16();
            return (short)o;
        }

        public static byte GetByte(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return elem.GetByte();
            return (byte)o;
        }

        public static bool GetBool(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return elem.GetBoolean();
            return (bool)o;
        }

        public static float GetFloat(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return elem.GetSingle();
            return (float)o;
        }

        public static Guid GetGUID(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return elem.GetGuid();
            return (Guid)o;
        }

        public static string GetString(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return elem.GetString();
            return (string)o;
        }

        public static byte[] GetBytes(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return elem.GetBytesFromBase64();
            if (o is BufferRegion region)
                return region.Memory.ToArray();
            return (byte[])o;
        }

        public static GMString GetString(this Dictionary<string, object> dict, ProjectFile pf, string name)
        {
            object o = dict[name];
            if (o is JsonElement elem)
                return pf.DataHandle.DefineString(elem.GetString());
            return pf.DataHandle.DefineString((string)o);
        }
    }
}
