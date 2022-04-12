using DogScepterLib.Core;
using DogScepterLib.Project;
using DogScepterLib.Project.GML.Decompiler;
using System;
using System.IO;

namespace DogScepterTest
{
    public class Util
    {
        public static string BaseDirectory => AppContext.BaseDirectory;

        public static ProjectFile BasicLoadProject(string dataFilePath, string? projectPath, Log logger)
        {
            // Unserialize
            using FileStream fs = new(dataFilePath, FileMode.Open);

            GMDataReader reader = new(fs, fs.Name);
            reader.Deserialize();

            foreach (var warning in reader.Warnings)
                logger($"[WARN: {warning.Level}] {warning.Message}");

            // Load
            ProjectFile pf = new ProjectFile(reader.Data, Path.Combine(projectPath ?? Environment.CurrentDirectory, "project"),
            (ProjectFile.WarningType type, string info) =>
            {
                logger($"Project warn: {type} {info ?? ""}");
            });
            pf.DecompileCache = new DecompileCache(pf);

            return pf;
        }

        public static void BasicSaveProject(ProjectFile pf, string exportPath, Log logger)
        {
            // Convert to data
            pf.ConvertToData();

            // Serialize
            using FileStream fs2 = new(exportPath, FileMode.Create);
            using GMDataWriter writer = new(pf.DataHandle, fs2, fs2.Name, pf.DataHandle.Length);
            writer.Write();
            writer.Flush();
            foreach (var warning in writer.Warnings)
                logger($"[WARN: {warning.Level}] {warning.Message}");
        }
    }
}