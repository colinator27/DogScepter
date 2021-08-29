using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML
{
    public class DecompileContext
    {
        public BlockList Blocks { get; set; }
        public List<Loop> Loops { get; set; }
        public List<ShortCircuit> ShortCircuits { get; set; }
        public Node BaseNode { get; set; }

        public DecompileContext()
        {
            // TODO
        }
    }

    public static class Decompiler
    {
        public static string Decompile(DecompileContext ctx)
        {
            // TODO
            return "// Stub";
        }
    }
}
