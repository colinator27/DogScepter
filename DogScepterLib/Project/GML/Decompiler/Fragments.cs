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
        public string Name;
        public Block Start;
        public Block End;
        public BlockList Blocks;

        public Fragment(string name, Block start, Block end)
        {
            Name = name;
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
                prev.ControlFlow = Block.ControlFlowType.PreFragment;
                prev.Instructions.RemoveAt(prev.Instructions.Count - 1); // Remove `b` instruction

                Block end = blockList.List[(prev.Branches[0] as Block).Index - 1];
                end.Instructions.RemoveAt(end.Instructions.Count - 1); // Remove `exit.i` instruction
                end.Branches.Clear();

                Block after = prev.Branches[0] as Block;
                after.AfterFragment = true;

                res.Add(new Fragment(child.Name.Content, start, end));
            }

            // Sort from inner to outer
            res = res.OrderBy(s => s.End.EndAddress).ThenByDescending(s => s.Start.Address).ToList();

            // Now assign BlockLists
            List<int> excludes = new List<int>(blockList.List.Count);
            foreach (var fragment in res)
            {
                int start = fragment.Start.Index, end = fragment.End.Index;
                fragment.Blocks = new BlockList(blockList, start, end, excludes);
                fragment.Blocks.FindUnreachables(excludes);
                excludes.AddRange(Enumerable.Range(start, (end - start) + 1).ToList());
            }

            var last = new Fragment(null, blockList.List[0], blockList.List[^1]);
            last.Blocks = new BlockList(blockList, excludes);
            res.Add(last);

            return res;
        }
    }
}
