using System;
using System.IO;

using DogScepterLib.Core;

namespace DogScepterTest
{
    class Program
    {
        static void Main(string[] args)
        {
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
                    }
                }
            }
        }
    }
}
