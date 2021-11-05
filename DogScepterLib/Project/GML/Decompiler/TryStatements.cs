using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    public class TryStatements
    {
        /// Finds all the switch statements within a list of blocks
        public static List<TryStatement> Find(DecompileContext ctx)
        {
            List<TryStatement> res = new List<TryStatement>();

            foreach (Block b in ctx.Blocks.List)
            {
                if (b.ControlFlow == Block.ControlFlowType.TryHook)
                {
                    // TODO
                }
            }

            return res;
        }

        /// Inserts a try statement node into the graph
        public static void InsertNode(DecompileContext ctx, TryStatement s)
        {
            // TODO
        }
    }
}
