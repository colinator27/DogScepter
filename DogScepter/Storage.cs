using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepter
{
    public static class Storage
    {
        public static string DataDirectory =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
                                          Environment.SpecialFolderOption.DoNotVerify), "dogscepter");

        public static string TempDirectory = Path.Combine(Path.GetTempPath(), "dogscepter");

        private static void CreateDataDirectory()
        {
            Directory.CreateDirectory(DataDirectory);
        }

        public static bool FileExists(string filename)
        {
            return File.Exists(Path.Combine(DataDirectory, filename));
        }

        public static long FileSize(string filename)
        {
            string path = Path.Combine(DataDirectory, filename);
            if (File.Exists(path))
                return new FileInfo(path).Length;
            return 0;
        }

        public static void Rename(string filename, string newName)
        {
            CreateDataDirectory();
            string path = Path.Combine(DataDirectory, filename);
            if (File.Exists(path))
                File.Move(filename, Path.Combine(DataDirectory, newName), true);
        }

        public static void WriteAllBytes(string filename, byte[] bytes)
        {
            CreateDataDirectory();
            File.WriteAllBytes(Path.Combine(DataDirectory, filename), bytes);
        }

        public static byte[] ReadAllBytes(string filename)
        {
            string path = Path.Combine(DataDirectory, filename);
            if (File.Exists(path))
                return File.ReadAllBytes(path);
            return null;
        }

        public static StreamWriter AppendText(string filename)
        {
            CreateDataDirectory();
            return File.AppendText(Path.Combine(DataDirectory, filename));
        }

        public static string GetTempDirectory()
        {
            Directory.CreateDirectory(TempDirectory);
            return TempDirectory;
        }

        public static void ClearTempDirectory()
        {
            DirectoryInfo di = new DirectoryInfo(GetTempDirectory());

            foreach (FileInfo file in di.EnumerateFiles())
                file.Delete();
            foreach (DirectoryInfo dir in di.EnumerateDirectories())
                dir.Delete(true);
        }
    }
}
