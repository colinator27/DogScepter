using DogScepterLib.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterCLI
{
    public static class Util
    {
        public static string RemoveQuotes(string path)
        {
            if (path.Length >= 2 && path.StartsWith('"') && path.EndsWith('"'))
                return path[1..^1];
            return path;
        }
    }
}
