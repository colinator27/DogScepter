using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Decompiler
{
    public class Fragment
    {
        public Block Start;
        public Block End;
        public BlockList Blocks;

        public Fragment(Block start, Block end)
        {
            Start = start;
            End = end;
        }
    }

    public static class Fragments
    {
        public static List<Fragment> FindAndProcess(GMCode entry)
        {
#if DEBUG
            if (entry.ParentEntry != null)
                throw new InvalidOperationException("Shouldn't be finding fragments in a child entry");
#endif

            List<Fragment> res = new List<Fragment>(entry.ChildEntries.Count + 1);
            BlockList blockList = Block.GetBlocks(entry);

            // Assemble list of fragments at a high level
            foreach (var child in entry.ChildEntries)
            {
                Block start = blockList.AddressToBlock[child.BytecodeOffset];

                Block prev = blockList.List[start.Index - 1];
                prev.Instructions.RemoveAt(prev.Instructions.Count - 1); // Remove `b` instruction

                Block end = blockList.List[(prev.Branches[0] as Block).Index - 1];
                end.Instructions.RemoveAt(end.Instructions.Count - 1); // Remove `exit.i` instruction
                end.Branches.Clear();

                res.Add(new Fragment(start, end));
            }
            res.Add(new Fragment(blockList.List[0], blockList.List[^1]));

            // Sort from inner to outer
            res = res.OrderBy(s => s.End.EndAddress).ThenByDescending(s => s.Start.Address).ToList();

            // Now assign BlockLists
            List<int> excludes = new List<int>(blockList.List.Count);
            foreach (var fragment in res)
            {
                fragment.Blocks = new BlockList(blockList, fragment.Start.Index, fragment.End.Index);
                fragment.Blocks.FindUnreachables(excludes);
                excludes.AddRange(Enumerable.Range(fragment.Start.Index, (fragment.End.Index - fragment.Start.Index) + 1).ToList());
            }

            return res;
        }
    }
}
