using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.User
{
    public static class Storage
    {
        public class StorageDirectory
        {
            public string Location { get; private set; }

            public StorageDirectory(string dir)
            {
                Location = dir;
            }

            // Returns error message or null if successful.
            public string CreateDirectory()
            {
                try
                {
                    Directory.CreateDirectory(Location);
                }
                catch (Exception e)
                {
                    return e.Message;
                }
                return null;
            }

            public bool FileExists(string filename)
            {
                try
                {
                    return File.Exists(Path.Combine(Location, filename));
                }
                catch
                {
                    return false;
                }
            }

            // Returns file size or -1 if unsuccessful
            public long FileSize(string filename)
            {
                try
                {
                    string path = Path.Combine(Location, filename);
                    if (File.Exists(path))
                        return new FileInfo(path).Length;
                }
                catch
                {
                    return -1;
                }
                return 0;
            }
            
            // Returns error message, or null if successful.
            public string Rename(string filename, string newName)
            {
                try
                {
                    string path = Path.Combine(Location, filename);
                    if (File.Exists(path))
                        File.Move(filename, Path.Combine(Location, newName), true);
                }
                catch (Exception e)
                {
                    return e.Message;
                }
                return null;
            }

            // Returns error message, or null if successful.
            public string WriteAllBytes(string filename, byte[] bytes)
            {
                try
                {
                    CreateDirectory();
                    File.WriteAllBytes(Path.Combine(Location, filename), bytes);
                }
                catch (Exception e)
                {
                    return e.Message;
                }
                return null;
            }

            public byte[] ReadAllBytes(string filename)
            {
                try
                {
                    string path = Path.Combine(Location, filename);
                    if (File.Exists(path))
                        return File.ReadAllBytes(path);
                }
                catch
                {
                    return null;
                }
                return null;
            }

            public StreamWriter AppendText(string filename)
            {
                try
                {
                    string path = Path.Combine(Location, filename);
                    if (File.Exists(path))
                        return File.AppendText(path);
                }
                catch
                {
                    return null;
                }
                return null;
            }

            // Returns error message or null if successful.
            public string Clear()
            {
                if (!Directory.Exists(Location))
                    return null;

                try
                {
                    DirectoryInfo di = new DirectoryInfo(Location);
                    foreach (FileInfo file in di.EnumerateFiles())
                        file.Delete();
                    foreach (DirectoryInfo dir in di.EnumerateDirectories())
                        dir.Delete(true);
                }
                catch (Exception e)
                {
                    return e.Message;
                }

                return null;
            }
        }

        public readonly static StorageDirectory Config = 
            new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData,
                                                       Environment.SpecialFolderOption.DoNotVerify), "dogscepter"));
        public readonly static StorageDirectory Data =
            new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                                                       Environment.SpecialFolderOption.DoNotVerify), "dogscepter"));

        public readonly static StorageDirectory Temp = new(Path.Combine(Path.GetTempPath(), "dogscepter"));
    }
}
