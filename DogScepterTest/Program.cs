using System;
using System.Diagnostics;
using System.IO;

using DogScepterLib.Core;

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
                    Console.WriteLine("WARN: " + w.Message);
                using (FileStream fs2 = new FileStream("out.win", FileMode.Create))
                {
                    using (GMDataWriter writer = new GMDataWriter(reader.Data, fs2, reader.Length))
                    {
                        writer.Flush();
                        foreach (GMWarning w in writer.Warnings)
                            Console.WriteLine("WARN: " + w.Message);
                    }
                }
            }
            s.Stop();
            Console.WriteLine(string.Format("Took {0} ms, {1} seconds.", s.Elapsed.TotalMilliseconds, Math.Round(s.Elapsed.TotalMilliseconds/1000, 2)));

            Console.ReadLine();
        }
    }
}
