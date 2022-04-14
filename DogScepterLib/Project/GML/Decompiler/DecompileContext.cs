using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.GML.Analysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Decompiler;

public class DecompileSettings
{
    public string Indent { get; set; } = "\t";
}

public class DecompileCache
{
    public MacroResolverTypes Types;
    public Dictionary<GMFunctionEntry, string> GlobalFunctionNames;

    public DecompileCache(ProjectFile pf)
    {
        Types = new MacroResolverTypes(pf);
        GlobalFunctionNames = new Dictionary<GMFunctionEntry, string>();
        if (pf.DataHandle.VersionInfo.IsVersionAtLeast(2, 3))
        {
            // Find all function names in global scripts
            GMChunkCODE code = pf.DataHandle.GetChunk<GMChunkCODE>();
            Parallel.ForEach(pf.DataHandle.GetChunk<GMChunkGLOB>().List, scr =>
            {
                GMCode entry = code.List[scr];

                // Find fragments
                List<Fragment> fragments = Fragments.FindAndProcess(entry, false);

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

    public static Dictionary<string, GMFunctionEntry> Find23FunctionsNotIncluded(ProjectFile pf, List<string> excludeList)
    {
        Dictionary<string, GMFunctionEntry> res = new();

        // Find all function names in global scripts
        GMChunkCODE code = pf.DataHandle.GetChunk<GMChunkCODE>();
        Parallel.ForEach(pf.DataHandle.GetChunk<GMChunkGLOB>().List, scr =>
        {
            GMCode entry = code.List[scr];
            if (excludeList.Contains(entry.Name?.Content))
                return;

            // Find fragments
            List<Fragment> fragments = Fragments.FindAndProcess(entry, false);

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
                        lock (res)
                            res.Add(name, func);
                    }
                }
            }
        });

        return res;
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
                CodeMacroTypes = data;
            else
                CodeMacroTypes = null;
        }
    }
    public MacroResolverTypes.MacroResolverTypeJson? CodeMacroTypes;
    public DecompileSettings Settings { get; set; }
    public DecompileCache Cache => Project.DecompileCache;
    public IList<GMString> Strings { get; set; }
    public BlockList Blocks { get; set; }
    public List<Loop> LoopNodes { get; set; }
    public List<ShortCircuit> ShortCircuitNodes { get; set; }
    public List<IfStatement> IfStatementNodes { get; set; }
    public List<SwitchStatement> SwitchStatementNodes { get; set; }
    public List<TryStatement> TryStatementNodes { get; set; }
    public List<Node> PredecessorsToClear { get; set; }
    public Node BaseNode { get; set; } // Represents the starting node of the control flow graph
    public ASTBlock BaseASTBlock { get; set; } // Represents the root AST node of the decompilation
    public ASTNode ParentCall { get; set; } // If applicable, this is the function call to the parent object
    public HashSet<string> RemainingLocals { get; set; } // Locals that haven't been used yet in decompilation
    public HashSet<string> AllLocals { get; set; } // All detected locals that exist

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
    public ConditionContext ConditionContext;

    public string FunctionName = null;

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

        ConditionContext = new(this);
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

        ConditionContext = existing.ConditionContext;
    }

    public string DecompileWholeEntryString(GMCode codeEntry)
    {
        DecompileWholeEntry(codeEntry);
        return ASTNode.WriteFromContext(this);
    }

    public void DecompileWholeEntry(GMCode codeEntry)
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
        DecompileSegment(codeEntry, fragments[^1].Blocks);
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
        ShortCircuitNodes = ShortCircuits.Find(Blocks, Data.VersionInfo.FormatID <= 14);

        // Now actually insert the nodes for them
        Loops.InsertNodes(this);
        ShortCircuits.InsertNodes(this);

        if (Data.VersionInfo.IsVersionAtLeast(2, 3))
        {
            // Remove static jumps (not needed for decompilation)
            BranchStatements.ProcessStatic(this);

            // Find try statement nodes
            TryStatementNodes = TryStatements.FindAndClean(this);
        }
        else
            TryStatementNodes = new List<TryStatement>();

        // Find all of the switch/if statements, then insert them (as well as 2.3 branch nodes!) in nested order together
        SwitchStatementNodes = SwitchStatements.Find(this);
        IfStatementNodes = IfStatements.Find(this);
        BranchStatements.InsertNodes(this);

        // Now build the AST, clean it, and write it to a string
        RemainingLocals = new HashSet<string>();
        BaseASTBlock = ASTBuilder.FromContext(this);
        AllLocals = new HashSet<string>(RemainingLocals);
        BaseASTBlock.Clean(this);
        ParentCall?.Clean(this);
    }
}
