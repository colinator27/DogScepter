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
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetInt32();
            return (int)o;
        }

        public static long GetLong(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetInt64();
            return (long)o;
        }

        public static short GetShort(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetInt16();
            return (short)o;
        }

        public static byte GetByte(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetByte();
            return (byte)o;
        }

        public static bool GetBool(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetBoolean();
            return (bool)o;
        }

        public static float GetFloat(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetSingle();
            return (float)o;
        }

        public static Guid GetGUID(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetGuid();
            return (Guid)o;
        }

        public static string GetString(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetString();
            return (string)o;
        }

        public static byte[] GetBytes(this Dictionary<string, object> dict, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return ((JsonElement)dict[name]).GetBytesFromBase64();
            return (byte[])o;
        }

        public static GMString GetString(this Dictionary<string, object> dict, ProjectFile pf, string name)
        {
            object o = dict[name];
            if (o is JsonElement)
                return pf.DataHandle.DefineString(((JsonElement)dict[name]).GetString());
            return pf.DataHandle.DefineString((string)o);
        }
    }
}
