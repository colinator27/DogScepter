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
using DogScepterLib.Project.Util;
using Microsoft.Toolkit.HighPerformance;

Stopwatch s = new Stopwatch();
s.Start();

s.Stop();
Console.WriteLine(string.Format("Took {0} ms, {1} seconds.", s.Elapsed.TotalMilliseconds, Math.Round(s.Elapsed.TotalMilliseconds/1000, 2)));

Console.ReadLine();