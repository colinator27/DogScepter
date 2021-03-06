using System;
using System.Diagnostics;
using System.IO;

using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project;
using DogScepterLib.Project.Bytecode;

namespace DogScepterTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch s = new Stopwatch();
            s.Start();
            using (FileStream fs = new FileStream(@"in.win", FileMode.Open))
            {
                GMDataReader reader = new GMDataReader(fs);
                foreach (GMWarning w in reader.Warnings)
                    Console.WriteLine(string.Format("[WARN: {0}] {1}", w.Level, w.Message));

                ProjectFile pf = new ProjectFile(reader.Data, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project"), 
                    (ProjectFile.WarningType type) => 
                    {
                        Console.WriteLine($"Project warn: {type}");
                    });
                if (!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project")))
                {
                    pf.AddAllPathsToJSON();
                    pf.Save();
                }
                else
                {
                    pf.Load();
                    pf.PurgeUnmodifiedPaths();
                    pf.ConvertToData();
                }

                using (FileStream fs2 = new FileStream("out.win", FileMode.Create))
                {
                    using (GMDataWriter writer = new GMDataWriter(reader.Data, fs2, reader.Length))
                    {
                        writer.Flush();
                        foreach (GMWarning w in writer.Warnings)
                            Console.WriteLine(string.Format("[WARN: {0}] {1}", w.Level, w.Message));
                    }
                }
            }
            s.Stop();
            Console.WriteLine(string.Format("Took {0} ms, {1} seconds.", s.Elapsed.TotalMilliseconds, Math.Round(s.Elapsed.TotalMilliseconds/1000, 2)));

            Console.ReadLine();
        }
    }
}
