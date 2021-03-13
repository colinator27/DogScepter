using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core
{
    public class GMWarning
    {
        public string Message;
        public string File; // Set when not in main WAD/data file
        public WarningLevel Level;
        public WarningKind Kind;

        public enum WarningLevel
        {
            Info,
            Bad,
            Severe
        }

        public enum WarningKind
        {
            Unknown,
            UnknownChunk
        }

        public GMWarning(string message, WarningLevel level = WarningLevel.Bad, WarningKind kind = WarningKind.Unknown)
        {
            Message = message;
            Level = level;
            Kind = kind;
        }
    }
}
