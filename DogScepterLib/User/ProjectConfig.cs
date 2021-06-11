using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.User
{
    public class ProjectConfig
    {
        public string InputFile { get; set; }
        public string OutputDirectory { get; set; }

        public ProjectConfig(string inputFile, string outputDirectory)
        {
            InputFile = inputFile;
            OutputDirectory = outputDirectory;
        }
    }
}
