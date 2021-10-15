using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project;
using DogScepterLib.Project.GML.Decompiler;
using DogScepterLib.Project.Converters;

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
                reader.Unserialize();
                foreach (GMWarning w in reader.Warnings)
                    Console.WriteLine(string.Format("[WARN: {0}] {1}", w.Level, w.Message));

                ProjectFile pf = new ProjectFile(reader.Data, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project"), 
                    (ProjectFile.WarningType type, string info) => 
                    {
                        Console.WriteLine($"Project warn: {type} {info ?? ""}");
                    });

                DecompileContext ctx = new DecompileContext(pf);
                var list = reader.Data.GetChunk<GMChunkCODE>().List;
                /*int ind = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Name.Content.Contains("gml_Object_obj_kingcutscene_ch1_Step_0"))
                    {
                        ind = i;
                        break;
                    }
                }
                string result = ctx.DecompileSegment(list[ind]);*/
                //string result = ctx.DecompileSegment(list[0]);
                Directory.CreateDirectory("Decomp");
                Parallel.ForEach(list, elem =>
                {
                    try
                    {
                        File.WriteAllText($"Decomp/{elem.Name.Content[0..Math.Min(elem.Name.Content.Length, 100)]}.gml", new DecompileContext(pf).DecompileSegment(elem));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed on {elem.Name.Content}, {e.Message}");
                    }
                });
                
                /*var cvt = pf.GetConverter<SpriteConverter>();
                foreach (var i in Enumerable.Range(0, 100))
                {
                    cvt.ConvertData(pf, i);
                    pf.Sprites[i].Asset.Dirty = true;
                }
                pf.AddDirtyAssetsToJSON(pf.Sprites);
                pf.SaveAll();*/

                //foreach (var group in pf.Textures.TextureGroups)
                //    group.AddNewEntry(pf.Textures, new GMTextureItem(testImage));
                //pf.Textures.RegenerateTextures();

                /*bool first = !Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "project"));
                if (first)
                {
                    for (int i = 0; i < pf.Rooms.Count; i++)
                    {
                        pf.GetConverter<RoomConverter>().ConvertData(pf, i);
                        pf.Rooms[i].Asset.Dirty = true;
                    }
                    pf.AddDirtyAssetsToJSON(pf.Rooms);
                    pf.SaveAll();
                } else
                {
                    //foreach (var g in pf.Textures.TextureGroups)
                    //    g.Dirty = true;
                    //var cvt = pf.GetConverter<SpriteConverter>();
                    //Parallel.ForEach(Enumerable.Range(0, pf.Sprites.Count), (i) => cvt.ConvertData(pf, i));
                    pf.LoadMain();
                    //Parallel.ForEach(pf.Sprites, (s) => CollisionMasks.GetMasksForSprite(pf, s.Asset, out _));
                    pf.PurgeIdenticalAssetsOnDisk(pf.Rooms);
                    pf.LoadAll();
                }*/

                //foreach (var group in pf.Textures.TextureGroups)
                //    group.Dirty = true;

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
                pf.TextureGroups.Clear();
                pf.TextureGroups.Add(new ProjectJson.TextureGroup() { Name = "main", AllowCrop = true, Border = 2, ID = 0 });
                pf.Textures.RegenerateTextures();
                pf.Textures.PurgeUnreferencedPages();
                */

                /*Directory.CreateDirectory("output");
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
                }*/
            }
            s.Stop();
            Console.WriteLine(string.Format("Took {0} ms, {1} seconds.", s.Elapsed.TotalMilliseconds, Math.Round(s.Elapsed.TotalMilliseconds/1000, 2)));

            Console.ReadLine();
        }
    }
}
