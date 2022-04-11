using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using Microsoft.Toolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks.Dataflow;

namespace DogScepterLib.Core;

/// <summary>
/// A delegate that is used for logging messages.
/// </summary>
public delegate void Log(string message);

/// <summary>
/// Class that represents a GameMaker data file.
/// </summary>
public class GMData
{
    /// <summary>
    /// Class that includes version information about a GameMaker data file.
    /// </summary>
    public class GMVersionInfo
    {
        /// <summary>
        /// The major version of the IDE a GameMaker data file was built with.
        /// </summary>
        /// <remarks>This is only an approximation, as some IDE versions do not increase this number.</remarks>
        public int Major = 1;

        /// <summary>
        /// The minor version of the IDE a GameMaker data file was built with.
        /// </summary>
        /// <remarks>This is only an approximation, as some IDE versions do not increase this number.</remarks>
        public int Minor = 0;

        /// <summary>
        /// The release version of the IDE a GameMaker data file was built with.
        /// </summary>
        /// <remarks>This is only an approximation, as some IDE versions do not increase this number.</remarks>
        public int Release = 0;

        /// <summary>
        /// The build version of the IDE a GameMaker data file was built with.
        /// </summary>
        /// <remarks>This is only an approximation, as some IDE versions do not increase this number.</remarks>
        public int Build = 0;

        /// <summary>
        /// Indicates the bytecode format of the GameMaker data file.
        /// </summary>
        /// <remarks>This is only an approximation, as some IDE versions do not increase this number.
        /// For example, this has a value of <c>17</c> from version 2.2.1 since at least 2022.3. </remarks>
        public byte FormatID = 0;

        /// <summary>
        /// Whether the GameMaker data file aligns chunks to 16 bytes.
        /// </summary>
        /// <remarks>GameMaker versions after Game Maker Studio 2.2.2 use this. On older versions,
        /// chunks are 4 byte aligned instead.</remarks>
        public bool AlignChunksTo16 = true;

        /// <summary>
        /// Whether the GameMaker data file aligns strings to 4 bytes.
        /// </summary>
        public bool AlignStringsTo4 = true;

        /// <summary>
        /// Whether the GameMaker data file aligns backgrounds to 8 bytes.
        /// </summary>
        public bool AlignBackgroundsTo8 = true;

        /// <summary>
        /// Whether the GameMaker data file uses Pre-Create events for rooms and objects.
        /// </summary>
        public bool RoomObjectPreCreate = false;

        /// <summary>
        /// Whether the GameMaker data file contains various variable counts whose purpose is currently unknown.
        /// </summary>
        public bool DifferentVarCounts = false;

        /// <summary>
        /// Whether the GameMaker data file uses option flags in the OPTN chunk.
        /// </summary>
        public bool OptionBitflag = true;

        /// <summary>
        /// Indicates whether this GameMaker data file was run from the IDE.
        /// </summary>
        public bool RunFromIDE = false;

        /// <summary>
        /// The ID of the main data file's audio group.
        /// </summary>
        public int BuiltinAudioGroupID => (Major >= 2 || (Major == 1 && (Build >= 1354 || (Build >= 161 && Build < 1000)))) ? 0 : 1;

        /// <summary>
        /// Sets the recorded version number (<see cref="Major"/>, <see cref="Minor"/>, <see cref="Release"/>, <see cref="Build"/>), but
        /// only if the parameters are higher..
        /// </summary>
        public void SetVersion(int major = 1, int minor = 0, int release = 0, int build = 0)
        {
            if (Major < major)
            {
                Major = major;
                Minor = minor;
                Release = release;
                Build = build;
                return;
            }
            if (Major > major) return;

            // Our Major is now equal to proposed major
            if (Minor < minor)
            {
                Minor = minor;
                Release = release;
                Build = build;
                return;
            }
            if (Minor > minor) return;

            // Our Minor is now equal to proposed minor.
            if (Release < release)
            {
                Release = release;
                Build = build;
                return;
            }
            if (Release > release) return;

            // Our Release is now equal to proposed release.
            if (Build < build)
                Build = build;
        }

        /// <summary>
        /// Returns whether the recorded version number is greater than or equal to the parameters.
        /// </summary>
        public bool IsVersionAtLeast(int major = 0, int minor = 0, int release = 0, int build = 0)
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

    /// <summary>
    /// Class that holds statistics about the data file, which are used for DogScepter. These aren't contained directly in the data file,
    /// but instead generated by DogScepter.
    /// </summary>
    public class GMStats
    {
        /// <summary>
        /// The ID of the last GML Struct referenced in the data file.
        /// </summary>
        public int LastStructID = -1;
    }

    /// <summary>
    /// Contains various version information of this data file.
    /// </summary>
    public readonly GMVersionInfo VersionInfo;

    /// <summary>
    /// Contains DogScepter related statistics about the data file.
    /// </summary>
    public readonly GMStats Stats;

    /// <summary>
    /// The FORM chunk of the data file.
    /// </summary>
    public GMChunkFORM FORM;

    /// <summary>
    /// All chunks of the data file.
    /// </summary>
    public Dictionary<string, GMChunk> Chunks => FORM.Chunks;

    #if DEBUG
    /// <summary>
    /// The debug logger, which writes messages to the console.
    /// </summary>
    public Log Logger = Console.WriteLine;
    #else
    /// <summary>
    /// The debug logger, which is disabled.
    /// </summary>
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

    /// <summary>
    /// Maps <see cref="GMString"/>.<see cref="GMString.Content"/> to the index it's located in
    /// <see cref="GMChunkSTRG"/>.<see cref="GMChunkSTRG.List"/>. Used as a cache for various <see cref="GMString"/> operations.
    /// </summary>
    private Dictionary<string, int> StringCache;

    /// <summary>
    /// The directory where the data file is located.
    /// </summary>
    public string Directory;

    /// <summary>
    /// The filename of the data file.
    /// </summary>
    public string Filename;

    /// <summary>
    /// The <see cref="System.Security.Cryptography.SHA1"/> of the data file.
    /// </summary>
    public byte[] Hash;

    //Only assigned, not used?
    public byte[] WorkingBuffer;

    /// <summary>
    /// The length of the data file in bytes.
    /// </summary>
    public int Length = 1024; // just give it a semi-reasonable minimum size

    /// <summary>
    /// Initializes a new instance of <see cref="GMData"/> with default values.
    /// </summary>
    public GMData()
    {
        VersionInfo = new GMVersionInfo();
        Stats = new GMStats();
    }

    /// <summary>
    /// Populates <see cref="StringCache"/> from <see cref="GMChunkSTRG"/>.<see cref="GMChunkSTRG.List"/>.
    /// </summary>
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

        GMPointerList<GMString> list = ((GMChunkSTRG)Chunks["STRG"]).List;

        lock (list)
        {
            if (StringCache != null)
            {
                if (StringCache.TryGetValue(content, out int stringIndex))
                    return list[stringIndex];

                for (int i = StringCache.Count; i < list.Count; i++)
                {
                    if (list[i].Content == content)
                        return list[i];
                }
            }
            else
            {
                foreach (GMString str in list.Where(str => str.Content == content))
                    return str;
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

        GMPointerList<GMString> list = ((GMChunkSTRG)Chunks["STRG"]).List;

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

        GMPointerList<GMString> list = ((GMChunkSTRG)Chunks["STRG"]).List;

        lock (list)
        {
            if (StringCache != null)
            {
                if (StringCache.TryGetValue(content, out int stringIndex))
                    return stringIndex;

                for (int i = StringCache.Count; i < list.Count; i++)
                {
                    if (list[i].Content == content)
                        return i;
                }
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

    /// <summary>
    /// Returns a specific <see cref="GMChunk"/> from the GameMaker data file.
    /// </summary>
    /// <typeparam name="T">The specific <see cref="GMChunk"/> that should get returned.</typeparam>
    /// <returns>The specific <see cref="GMChunk"/> from the data file if it exists, if not <see langword="null"/>.</returns>
    public T GetChunk<T>() where T : GMChunk
    {
        GMChunk chunk;
        if (Chunks.TryGetValue(GMChunkFORM.ChunkMapReverse[typeof(T)], out chunk))
            return (T)chunk;
        return null;
    }
}