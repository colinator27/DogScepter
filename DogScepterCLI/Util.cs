using System.IO;
using CliFx.Infrastructure;

namespace DogScepterCLI
{
    /// <summary>
    /// Class that contains several utility methods.
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// Removes double quotes or single quotes from a path, should it be surrounded by them.
        /// </summary>
        /// <param name="path">The path to remove quotes from.</param>
        /// <returns><paramref name="path"/> without quotes at the beginning and end of it.</returns>
        public static string RemoveQuotes(string path)
        {
            if (path.Length >= 2)
            {
                // Some file explorers (primarily on Linux) surround file paths with ' instead of "
                if ((path.StartsWith('"') && path.EndsWith('"')) ||
                    (path.StartsWith('\'') && path.EndsWith('\'')))
                    return path[1..^1];
            }
            return path;
        }

        /// <summary>
        /// Check whether a DogScepter project already exists in a certain location.
        /// </summary>
        /// <param name="console">The console to use for outputting Errors to.</param>
        /// <param name="dir">The directory to check if it contains a DogScepter project.</param>
        /// <returns><see langword="false"/> if the project does not exist at that location or the project directory does not exist, otherwise <see langword="true"/>.</returns>
        public static bool CheckIfProjectExists(IConsole console, string dir)
        {
            if (!Directory.Exists(dir))
            {
                console.Error.WriteLine($"{dir} does not exist.");
                return false;
            }

            if (!File.Exists(Path.Combine(dir, "project.json")))
            {
                console.Error.WriteLine($"No project exists in {dir}.");
                return false;
            }

            console.Output.WriteLine($"Project exists at {dir}");
            return true;
        }
    }
}
