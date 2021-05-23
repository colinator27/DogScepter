using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project;
using DogScepterLib.Project.Bytecode;
using SkiaSharp;

namespace DogScepterTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch s = new Stopwatch();
            s.Start();
            using (FileStream fs = new FileStream(@"input/data.win", FileMode.Open))
            {
                GMDataReader reader = new GMDataReader(fs, fs.Name);
                foreach (GMWarning w in reader.Warnings)
                    Console.WriteLine(string.Format("[WARN: {0}] {1}", w.Level, w.Message));

                //var blockTest = Block.GetBlocks(reader.Data.GetChunk<GMChunkCODE>().List[1]);

                ProjectFile pf = new ProjectFile(reader.Data, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project"), 
                    (ProjectFile.WarningType type, string info) => 
                    {
                        Console.WriteLine($"Project warn: {type} {info ?? ""}");
                    });

                //SKBitmap testImage = SKBitmap.Decode(File.ReadAllBytes("testsprite.png"));
                //foreach (var group in pf.Textures.TextureGroups)
                //    group.AddNewEntry(pf.Textures, new GMTextureItem(testImage));
                //pf.Textures.RegenerateTextures();

                /*
                pf.Textures.TextureGroups.Clear();
                var megaGroup = new Textures.Group();
                var list = pf.DataHandle.GetChunk<GMChunkTXTR>().List;
                for (int i = 0; i < list.Count; i++)
                    megaGroup.Pages.Add(i);
                foreach (var entry in pf.DataHandle.GetChunk<GMChunkTPAG>().List)
                    if (entry.TexturePageID != -1)
                        megaGroup.AddNewEntry(pf.Textures, entry);
                pf.Textures.TextureGroups.Add(megaGroup);
                pf.Textures.RegenerateTextures();
                */

                bool first = !Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project"));
                if (first)
                {
                    ConvertDataToProject.ConvertSound(pf, 0);
                    pf.Sounds[0].Asset.Dirty = true;
                    pf.AddDirtyAssetsToJSON(pf.Sounds, "sounds");
                    pf.SaveAll();
                } else
                {
                    ConvertDataToProject.ConvertSound(pf, 0);
                    pf.LoadMain();
                    pf.PurgeIdenticalAssetsOnDisk(pf.Sounds);
                    pf.LoadAll();
                }

                Directory.CreateDirectory("output");
                using (FileStream fs2 = new FileStream("output/data.win", FileMode.Create))
                {
                    using (GMDataWriter writer = new GMDataWriter(reader.Data, fs2, fs2.Name, reader.Length))
                    {
                        if (!first)
                            pf.ConvertToData();
                        writer.Write();
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
