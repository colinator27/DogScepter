using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib
{
    public static class GameConfigs
    {
        public static string BaseDirectory = Path.Combine(AppContext.BaseDirectory, "GameConfigs");
        public static string MacroTypesDirectory = Path.Combine(BaseDirectory, "MacroTypes");

        public static string[] FindAllMacroTypes()
        {
            return Directory.GetFiles(MacroTypesDirectory, "*.json");
        }
    }
}
