using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Decompiler
{
    public class DecompileSettings
    {
        public string Indent { get; set; } = "    ";
    }

    public class DecompileCache
    {
        public AssetResolverTypes Types;
        public Dictionary<GMFunctionEntry, string> GlobalFunctionNames;

        public DecompileCache(ProjectFile pf)
        {
            Types = new AssetResolverTypes(pf);
            GlobalFunctionNames = new Dictionary<GMFunctionEntry, string>();
            if (pf.DataHandle.VersionInfo.IsNumberAtLeast(2, 3))
            {
                // Find all function names in global scripts
                GMChunkCODE code = pf.DataHandle.GetChunk<GMChunkCODE>();
                Parallel.ForEach(pf.DataHandle.GetChunk<GMChunkGLOB>().List, scr =>
                {
                    GMCode entry = code.List[scr];

                    // Find fragments
                    List<Fragment> fragments = Fragments.FindAndProcess(entry);

                    // Find blocks in the main fragment that come after another fagment
                    foreach (Block b in fragments[^1].Blocks.List)
                    {
                        if (b.AfterFragment && b.Instructions.Count > 2 &&
                            b.Instructions[0].Kind == GMCode.Bytecode.Instruction.Opcode.Push &&
                            b.Instructions[0].Type1 == GMCode.Bytecode.Instruction.DataType.Int32 &&
                            b.Instructions[0].Value == null)
                        {
                            string name = ASTBuilder.GetNameAfterFragment(b);
                            if (name != null)
                            {
                                GMFunctionEntry func = b.Instructions[0].Function.Target;
                                lock (GlobalFunctionNames)
                                    GlobalFunctionNames[func] = name;
                            }
                        }
                    }
                });
            }
        }
    }

    public class DecompileContext
    {
        public ProjectFile Project { get; set; }
        public GMData Data { get; set; }
        private string _codeName;
        public string CodeName
        {
            get => _codeName; 
            set
            {
                _codeName = value;
                if (Cache.Types.CodeEntries != null &&
                    Cache.Types.CodeEntries.TryGetValue(_codeName, out var data))
                    CodeAssetTypes = data;
                else
                    CodeAssetTypes = null;
            }
        }
        public AssetResolverTypes.AssetResolverTypeJson? CodeAssetTypes;
        public DecompileSettings Settings { get; set; }
        public DecompileCache Cache => Project.DecompileCache;
        public IList<GMString> Strings { get; set; }
        public BlockList Blocks { get; set; }
        public List<Loop> LoopNodes { get; set; }
        public List<ShortCircuit> ShortCircuitNodes { get; set; }
        public List<IfStatement> IfStatementNodes { get; set; }
        public List<SwitchStatement> SwitchStatementNodes { get; set; }
        public List<Node> PredecessorsToClear { get; set; }
        public Node BaseNode { get; set; }
        public ASTBlock BaseASTBlock { get; set; }
        public HashSet<string> RemainingLocals { get; set; }
        public HashSet<string> AllLocals { get; set; }

        private int indentationLevel = 0;
        public int IndentationLevel
        {
            get => indentationLevel;
            set { indentationLevel = value; Indentation = new StringBuilder().Insert(0, Settings.Indent, indentationLevel).ToString(); }
        }
        public string Indentation = "";

        public DecompileContext ParentContext;
        public List<DecompileContext> SubContexts;
        public Fragment Fragment;
        public List<ASTNode> StructArguments;

        public DecompileContext(ProjectFile pf, DecompileSettings settings = null)
        {
#if DEBUG
            if (pf.DecompileCache == null)
                throw new Exception("Missing decompile cache! Needs to be initialized.");
#endif

            Project = pf;
            Data = pf.DataHandle;
            Strings = Data.GetChunk<GMChunkSTRG>().List;
            Settings = settings ?? new DecompileSettings();
        }

        public DecompileContext(DecompileContext existing, Fragment fragment)
        {
            ParentContext = existing;
            Fragment = fragment;

            Project = existing.Project;
            Data = existing.Data;
            Strings = existing.Strings;
            Settings = existing.Settings;
            SubContexts = existing.SubContexts; // maintain this reference

#if DEBUG
            if (Project.DecompileCache == null)
                throw new Exception("Missing decompile cache! Needs to be initialized.");
#endif


            CodeName = fragment.Name;
        }

        public string DecompileWholeEntry(GMCode codeEntry)
        {
            CodeName = codeEntry.Name?.Content;

            // Find all fragments
            List<Fragment> fragments = Fragments.FindAndProcess(codeEntry);

            // Decompile each individual fragment
            SubContexts = new List<DecompileContext>(fragments.Count - 1);
            for (int i = 0; i < fragments.Count - 1; i++)
            {
                var ctx = new DecompileContext(this, fragments[i]);
                ctx.DecompileSegment(codeEntry, fragments[i].Blocks);
                SubContexts.Add(ctx);
            }

            // Then decompile the main fragment
            return DecompileSegmentString(codeEntry, fragments[^1].Blocks);
        }

        public void DecompileSegment(GMCode codeEntry, BlockList existingList = null)
        {
            Blocks = existingList ?? Block.GetBlocks(codeEntry);
            Blocks.FindUnreachables();

            // Add node to beginning
            BaseNode = new Block(-1, -1);
            BaseNode.Branches.Add(Blocks.List[0]);
            Blocks.List[0].Predecessors.Add(BaseNode);

            // Find loops first to eliminate backwards branches (mostly), and differentiate `bt` instructions
            LoopNodes = Loops.Find(Blocks);
            ShortCircuitNodes = ShortCircuits.Find(Blocks);

            // Now actually insert the nodes for them
            Loops.InsertNodes(this);
            ShortCircuits.InsertNodes(this);

            // Find all of the switch/if statements, then insert them in nested order together
            SwitchStatementNodes = SwitchStatements.Find(this);
            IfStatementNodes = IfStatements.Find(this);
            BranchStatements.InsertNodes(this);

            // Now build the AST, clean it, and write it to a string
            RemainingLocals = new HashSet<string>();
            BaseASTBlock = ASTBuilder.FromContext(this);
            AllLocals = new HashSet<string>(RemainingLocals);
            BaseASTBlock.Clean(this);
        }

        public string DecompileSegmentString(GMCode codeEntry, BlockList existingList = null)
        {
            DecompileSegment(codeEntry, existingList);
            return ASTNode.WriteFromContext(this);
        }
    }
}
