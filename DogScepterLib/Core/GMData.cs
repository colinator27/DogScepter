using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using Microsoft.Toolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace DogScepterLib.Core
{
    public delegate void Log(string message);

    public class GMData
    {
        public class GMVersionInfo
        {
            public int Major = 1;
            public int Minor = 0;
            public int Release = 0;
            public int Build = 0;

            public byte FormatID = 0;

            public bool AlignChunksTo16 = true;
            public bool AlignStringsTo4 = true;
            public bool AlignBackgroundsTo8 = true;

            public bool RoomObjectPreCreate = false;

            public bool DifferentVarCounts = false;

            public bool OptionBitflag = true;

            public bool RunFromIDE = false;

            public int BuiltinAudioGroupID => (Major >= 2 || (Major == 1 && (Build >= 1354 || (Build >= 161 && Build < 1000)))) ? 0 : 1;

            /// <summary>
            /// Sets the major/minor/release/build version, only if higher
            /// </summary>
            public void SetNumber(int major = 1, int minor = 0, int release = 0, int build = 0)
            {
                if (Major < major)
                {
                    Major = major;
                    Minor = minor;
                    Release = release;
                    Build = build;
                }
                else if (Major == major)
                {
                    if (Minor < minor)
                    {
                        Minor = minor;
                        Release = release;
                        Build = build;
                    }
                    else if (Minor == minor)
                    {
                        if (Release < release)
                        {
                            Release = release;
                            Build = build;
                        }
                        else if (Release == release)
                        {
                            if (Build < build)
                                Build = build;
                        }
                    }
                }
            }

            /// <summary>
            /// Returns whether the recorded version number is greater than or equal to the parameters
            /// </summary>
            public bool IsNumberAtLeast(int major = 0, int minor = 0, int release = 0, int build = 0)
            {
                if (Major != major)
                    return (Major > major);
                if (Minor != minor)
                    return (Minor > minor);
                if (Release != release)
                    return (Release > release);
                if (Build != build)
                    return (Build > build);
                return true;
            }
        }

        public class GMStats
        {
            public int LastStructID = -1;
        }

        public GMVersionInfo VersionInfo;
        public GMStats Stats;

        public GMChunkFORM FORM;
        public Dictionary<string, GMChunk> Chunks => FORM.Chunks;
#if DEBUG
        public Log Logger = Console.WriteLine;
#else
        public Log Logger = null;
#endif

        // Handles writing miscellaneous files asynchronously
        public ActionBlock<KeyValuePair<string, BufferRegion>> FileWrites = new ActionBlock<KeyValuePair<string, BufferRegion>>(f =>
        {
            using FileStream fs = new FileStream(f.Key, FileMode.Create);
            using Stream s = f.Value.Memory.AsStream();
            s.CopyTo(fs);
        }, new ExecutionDataflowBlockOptions()
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        }
        );

        public Dictionary<string, int> StringCache;

        public string Directory;
        public string Filename;
        public byte[] Hash;
        public byte[] WorkingBuffer;
        public int Length = 1024; // just give it a semi-reasonable minimum size

        public GMData()
        {
            VersionInfo = new GMVersionInfo();
            Stats = new GMStats();
        }

        public void BuildStringCache()
        {
            StringCache = new Dictionary<string, int>();
            var list = ((GMChunkSTRG)Chunks["STRG"]).List;
            for (int i = 0; i < list.Count; i++)
                StringCache[list[i].Content] = i;
        }

        public GMString DefineString(string content)
        {
            if (content == null)
                return null;

            var list = ((GMChunkSTRG)Chunks["STRG"]).List;

            lock (list)
            {
                if (StringCache != null)
                {
                    if (StringCache.TryGetValue(content, out int stringIndex))
                        return list[stringIndex];

                    for (int i = StringCache.Count; i < list.Count; i++)
                        if (list[i].Content == content)
                            return list[i];
                }
                else
                {
                    foreach (GMString str in list)
                    {
                        if (str.Content == content)
                            return str;
                    }
                }

                GMString res = new();
                res.Content = content;
                list.Add(res);
                return res;
            }
        }

        public GMString DefineString(string content, out int index)
        {
            if (content == null)
            {
                index = -1;
                return null;
            }

            var list = ((GMChunkSTRG)Chunks["STRG"]).List;

            lock (list)
            {
                if (StringCache != null)
                {
                    if (StringCache.TryGetValue(content, out int stringIndex))
                    {
                        index = stringIndex;
                        return list[stringIndex];
                    }

                    for (int i = StringCache.Count; i < list.Count; i++)
                    {
                        if (list[i].Content == content)
                        {
                            index = i;
                            return list[i];
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].Content == content)
                        {
                            index = i;
                            return list[i];
                        }
                    }
                }

                GMString res = new();
                res.Content = content;
                list.Add(res);
                index = list.Count - 1;
                return res;
            }
        }

        public int DefineStringIndex(string content)
        {
            if (content == null)
                return -1;

            var list = ((GMChunkSTRG)Chunks["STRG"]).List;

            lock (list)
            {
                if (StringCache != null)
                {
                    if (StringCache.TryGetValue(content, out int stringIndex))
                        return stringIndex;

                    for (int i = StringCache.Count; i < list.Count; i++)
                        if (list[i].Content == content)
                            return i;
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].Content == content)
                            return i;
                    }
                }

                GMString res = new();
                res.Content = content;
                list.Add(res);
                return list.Count - 1;
            }
        }

        public T GetChunk<T>() where T : GMChunk
        {
            GMChunk chunk;
            if (Chunks.TryGetValue(GMChunkFORM.ChunkMapReverse[typeof(T)], out chunk))
                return (T)chunk;
            return null;
        }
    }
}
