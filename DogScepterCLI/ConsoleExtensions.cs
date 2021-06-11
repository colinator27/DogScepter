using CliFx.Infrastructure;
using DogScepterLib.Core;
using DogScepterLib.Project;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterCLI
{
    public static class ConsoleExtensions
    {
        public static string ReadString(this IConsole console, string msg)
        {
            console.Output.Write($"> {msg}: ");
            return console.Input.ReadLine();
        }

        public static bool PromptYesNo(this IConsole console, string msg)
        {
            console.Output.Write($"> {msg} (Y/n): ");
            return char.ToLowerInvariant(console.Input.ReadLine().FirstOrDefault()) == 'y';
        }

        public static string PromptDirectory(this IConsole console, string msg)
        {
            string dir = Util.RemoveQuotes(console.ReadString(msg));

            while (!Directory.Exists(dir))
            {
                console.Output.WriteLine("The specified directory does not exist.");
                if (console.PromptYesNo("Create the directory?"))
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch (Exception e)
                    {
                        console.Output.Write($"Failed to create directory: {e.Message}");
                    }
                }
                else
                {
                    dir = Util.RemoveQuotes(console.ReadString("Specify a new directory"));
                }
            }

            return dir;
        }

        public static string PromptFile(this IConsole console, string msg)
        {
            string file = Util.RemoveQuotes(console.ReadString(msg));

            while (!File.Exists(file))
            {
                console.Output.WriteLine("The specified file does not exist.");
                file = Util.RemoveQuotes(console.ReadString("Specify a new file path"));
            }

            return file;
        }

        public static GMData LoadDataFile(this IConsole console, string file, bool verbose = false)
        {
            console.Output.WriteLine("Loading data file...");
            try
            {
                using FileStream fs = new FileStream(file, FileMode.Open);
                GMDataReader reader = new GMDataReader(fs, fs.Name);
                if (verbose)
                    reader.Data.Logger = console.Output.WriteLine;
                else
                    reader.Data.Logger = null;
                reader.Unserialize();
                foreach (GMWarning w in reader.Warnings)
                    console.Output.WriteLine($"[WARN: {w.Level}] {w.Message}"); // todo formatting
                return reader.Data;
            }
            catch (Exception e)
            {
                console.Error.WriteLine(e.ToString());
                return null;
            }
        }

        public static ProjectFile OpenProject(this IConsole console, GMData data, string directory)
        {
            console.Output.WriteLine("Opening project...");
            try
            {
                ProjectFile pf = new ProjectFile(data, directory,
                (ProjectFile.WarningType type, string info) =>
                {
                    console.Output.WriteLine($"[WARN: {type}] {info ?? ""}");
                });
                return pf;
            }
            catch (Exception e)
            {
                console.Error.WriteLine(e.ToString());
                return null;
            }
        }

        public static bool SaveProject(this IConsole console, ProjectFile pf)
        {
            console.Output.WriteLine("Saving project...");
            try
            {
                pf.SaveAll();
            }
            catch (Exception e)
            {
                console.Output.WriteLine();
                console.Error.WriteLine($"Failed to save project: {e}");
                return false;
            }
            console.Output.WriteLine("Project save completed.");
            return true;
        }
    }
}
