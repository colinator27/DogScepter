using CliFx.Infrastructure;
using DogScepterLib.Core;
using DogScepterLib.Project;
using System;
using System.IO;
using System.Linq;

namespace DogScepterCLI
{
    public static class ConsoleExtensions
    {
        /// <summary>
        /// Reads a string a line from the console, while displaying a message.
        /// </summary>
        /// <param name="console">The console from where the string will be read from.</param>
        /// <param name="message">The message to display.</param>
        /// <returns></returns>
        public static string ReadString(this IConsole console, string message)
        {
            console.Output.Write($"> {message}: ");
            return console.Input.ReadLine()?.Trim();
        }

        /// <summary>
        /// Does a Yes/No prompt from the console, while displaying a message.
        /// </summary>
        /// <param name="console">The console from where the Yes/No prompt will be done from.</param>
        /// <param name="message">The message to display.</param>
        /// <returns><see langword="true"/> if <c>Y</c> or <c>y</c> was inputted, otherwise <see langword="false"/>.</returns>
        public static bool PromptYesNo(this IConsole console, string message)
        {
            console.Output.Write($"> {message} (y/N): ");
            // ReadLine can return null, so if it does, we'll use ' ' instead.
            char firstCharOfInput = console.Input.ReadLine()?.FirstOrDefault() ?? ' ';
            return Char.ToLowerInvariant(firstCharOfInput) == 'y';
        }

        /// <summary>
        /// Prompts for a directory path, while displaying a custom message.
        /// </summary>
        /// <param name="console">The console from where the directory prompt will be done from.</param>
        /// <param name="message">The message to display.</param>
        /// <returns>The directory path that was inputted.</returns>
        /// <remarks>This method will be in a <c>do...while</c> loop until a directory path that exists was inputted. <br/>
        /// Should the directory path not exist, another prompt will appear to create the directory. An affirmative input will create that directory,
        /// while ignoring all exceptions that could occur. A negative input will continue to prompt for another directory.</remarks>
        public static string PromptDirectory(this IConsole console, string message)
        {
            string dir = Util.RemoveQuotes(console.ReadString(message));

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
                        console.Error.Write($"Failed to create directory: {e.Message}");
                    }
                }
                else
                {
                    dir = Util.RemoveQuotes(console.ReadString("Specify a new directory"));
                }
            }

            return dir;
        }

        /// <summary>
        /// Prompts for a file path, while displaying a custom method.
        /// </summary>
        /// <param name="console">The console from where the file prompt will be done from.</param>
        /// <param name="message">The message to display</param>
        /// <returns>The file path that was inputted.</returns>
        /// <remarks>This method will do a <c>do..while</c> loop until a file path that exists will was inputted,
        /// continuing to prompt for another file path if it doesn't.</remarks>
        public static string PromptFile(this IConsole console, string message)
        {
            string file = Util.RemoveQuotes(console.ReadString(message));

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

        /// <summary>
        /// Opens a new DogScepter project and returns it as a <see cref="ProjectFile"/>.
        /// </summary>
        /// <param name="console">The console from where the DogScepter project should be opened..</param>
        /// <param name="data">The GameMaker data file that should be associated for the project.</param>
        /// <param name="directory">The directory path to where the DogScepter project is located.</param>
        /// <returns>The opened DogScepter project as a <see cref="ProjectFile"/>.</returns>
        public static ProjectFile OpenProject(this IConsole console, GMData data, string directory)
        {
            console.Output.WriteLine("Opening project...");
            try
            {
                ProjectFile projectFile = new ProjectFile(data, directory,
                (ProjectFile.WarningType type, string info) =>
                {
                    console.Output.WriteLine($"[WARN: {type}] {info ?? ""}");
                });
                console.Output.WriteLine("Project opened.");
                return projectFile;
            }
            catch (Exception e)
            {
                console.Error.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Saves a DogScepter project file.
        /// </summary>
        /// <param name="console">The console from where the DogScepter project file should be saved..</param>
        /// <param name="projectFile">The DogScepter project file that should get saved.</param>
        /// <returns><see langword="true"/> if saving was successful, otherwise <see langword="false"/>.</returns>
        public static bool SaveProject(this IConsole console, ProjectFile projectFile)
        {
            console.Output.WriteLine("Saving project...");
            try
            {
                projectFile.SaveAll();
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
