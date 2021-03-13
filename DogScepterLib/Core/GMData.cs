using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core
{
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
                } else if (Major == major)
                {
                    if (Minor < minor)
                    {
                        Minor = minor;
                        Release = release;
                        Build = build;
                    } else if (Minor == minor)
                    {
                        if (Release < release)
                        {
                            Release = release;
                            Build = build;
                        } else if (Release == release)
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

        public GMVersionInfo VersionInfo;

        public GMChunkFORM FORM;
        public Dictionary<string, GMChunk> Chunks => FORM.Chunks;

        public string Directory;
        public byte[] Hash;
        public int Length = 1024; // just give it a semi-reasonable minimum size

        public GMData()
        {
            VersionInfo = new GMVersionInfo();
        }

        public GMString DefineString(string content)
        {
            var list = ((GMChunkSTRG)Chunks["STRG"]).List;
            foreach (GMString str in list)
            {
                if (str.Content == content)
                    return str;
            }

            GMString res = new GMString();
            res.Content = content;
            list.Add(res);
            return res;
        }

    }
}
