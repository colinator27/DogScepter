using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Project;
using DogScepterLib.Project.GML.Decompiler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DogScepterTest
{
    [Collection("Sequential")]
    public class TestFiles
    {
        private readonly ITestOutputHelper _output;
        private readonly string[] _testFiles;
        private readonly string _baseDir;
        private readonly string _exportDir;

        public TestFiles(ITestOutputHelper output)
        {
            _output = output;
            _baseDir = Path.Combine(Util.BaseDirectory, "TestFiles");
            _exportDir = Path.Combine(Util.BaseDirectory, "Export");
            _testFiles = Directory.GetFiles(_baseDir, "*.testwad", SearchOption.AllDirectories);

            if (!Directory.Exists(_exportDir))
                Directory.CreateDirectory(_exportDir);
        }

        [Fact]
        public void DeserializeTestFiles()
        {
            Assert.All(_testFiles, file =>
            {
                using FileStream fs = new(file, FileMode.Open);

                GMDataReader reader = new(fs, fs.Name);
                reader.Deserialize();

                foreach (var warning in reader.Warnings)
                    _output.WriteLine($"[WARN: {warning.Level}] {warning.Message}");
            });
        }

        [Fact]
        public void LoadTestFiles()
        {
            Assert.All(_testFiles, file =>
            {
                using FileStream fs = new(file, FileMode.Open);

                GMDataReader reader = new(fs, fs.Name);
                reader.Deserialize();

                foreach (var warning in reader.Warnings)
                    _output.WriteLine($"[WARN: {warning.Level}] {warning.Message}");

                ProjectFile pf = new ProjectFile(reader.Data, Path.Combine(_exportDir, "project"),
                (ProjectFile.WarningType type, string info) =>
                {
                    _output.WriteLine($"Project warn: {type} {info ?? ""}");
                });
                pf.DecompileCache = new DecompileCache(pf);
            });
        }

        [Fact]
        public void DeserializeAndSerializeTestFiles()
        {
            Assert.All(_testFiles, file =>
            {
                // Deserialize
                using FileStream fs = new(file, FileMode.Open);

                GMDataReader reader = new(fs, fs.Name);
                reader.Deserialize();

                foreach (var warning in reader.Warnings)
                    _output.WriteLine($"[WARN: {warning.Level}] {warning.Message}");

                // Serialize
                using FileStream fs2 = new(Path.Combine(_exportDir, "data.testwad"), FileMode.Create);
                using GMDataWriter writer = new(reader.Data, fs2, fs2.Name, reader.Length);
                writer.Write();
                writer.Flush();
                foreach (var warning in writer.Warnings)
                    _output.WriteLine($"[WARN: {warning.Level}] {warning.Message}");
            });
        }

        [Fact]
        public void LoadAndSaveTestFiles()
        {
            Assert.All(_testFiles, file =>
            {
                // Deserialize
                using FileStream fs = new(file, FileMode.Open);

                GMDataReader reader = new(fs, fs.Name);
                reader.Deserialize();

                foreach (var warning in reader.Warnings)
                    _output.WriteLine($"[WARN: {warning.Level}] {warning.Message}");

                // Load
                ProjectFile pf = new ProjectFile(reader.Data, Path.Combine(_exportDir, "project"),
                (ProjectFile.WarningType type, string info) =>
                {
                    _output.WriteLine($"Project warn: {type} {info ?? ""}");
                });
                pf.DecompileCache = new DecompileCache(pf);

                // Convert to data
                pf.ConvertToData();

                // Serialize
                using FileStream fs2 = new(Path.Combine(_exportDir, "data.testwad"), FileMode.Create);
                using GMDataWriter writer = new(reader.Data, fs2, fs2.Name, reader.Length);
                writer.Write();
                writer.Flush();
                foreach (var warning in writer.Warnings)
                    _output.WriteLine($"[WARN: {warning.Level}] {warning.Message}");
            });
        }

        [Fact]
        public void LoadAndDecompileAll()
        {
            Assert.All(_testFiles, file =>
            {
                // Deserialize
                using FileStream fs = new(file, FileMode.Open);

                GMDataReader reader = new(fs, fs.Name);
                reader.Deserialize();

                foreach (var warning in reader.Warnings)
                    _output.WriteLine($"[WARN: {warning.Level}] {warning.Message}");

                // Load
                ProjectFile pf = new ProjectFile(reader.Data, Path.Combine(_exportDir, "project"),
                (ProjectFile.WarningType type, string info) =>
                {
                    _output.WriteLine($"Project warn: {type} {info ?? ""}");
                });
                pf.DecompileCache = new DecompileCache(pf);

                var codeList = pf.DataHandle.GetChunk<GMChunkCODE>().List;
                Parallel.ForEach(codeList, elem =>
                {
                    if (elem.ParentEntry != null)
                        return;
                    try
                    {
                        new DecompileContext(pf).DecompileWholeEntryString(elem);
                    }
                    catch (Exception e)
                    {
                        _output.WriteLine($"Failed to decompile code for \"{elem.Name.Content}\": {e}");
                        throw;
                    }
                });
            });
        }
    }
}
