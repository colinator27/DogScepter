using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using System.Collections.Generic;
using System.Linq;
using static DogScepterLib.Core.Models.GMCode.Bytecode;
using static DogScepterLib.Core.Models.GMCode.Bytecode.Instruction;

namespace DogScepterLib.Project.GML.Compiler;

public class CompileContext
{
    public ProjectFile Project { get; init; }
    public Builtins Builtins { get; init; }
    public List<CodeContext> Code { get; init; } = new();
    public bool IsOldCodeFormat { get; init; }
    public bool IsGMS2 { get; init; }
    public bool IsGMS23 { get; init; }
    public List<ErrorMessage> Errors { get; init; } = new();
    public Dictionary<string, CodeContext> Macros { get; init; } = new();
    public Dictionary<string, Enum> Enums { get; init; } = new();
    public bool ResolveEnums { get; set; } = false;
    public HashSet<string> ReferencedEnums { get; init; } = new();
    public Dictionary<string, int> AssetIds = new();
    public Dictionary<string, int> VariableIds = new();
    public Dictionary<string, FunctionReference> Functions = new();
    public Dictionary<string, GMCode> NameToCode = new();
    public List<FunctionReference> FunctionsToResolveLater { get; set; } = new();
    public List<string> Scripts = new();

    public CompileContext(ProjectFile pf)
    {
        Project = pf;
        IsOldCodeFormat = (pf.DataHandle.VersionInfo.FormatID <= 14);
        IsGMS2 = pf.DataHandle.VersionInfo.IsVersionAtLeast(2);
        IsGMS23 = pf.DataHandle.VersionInfo.IsVersionAtLeast(2, 3);
            
        // Populate asset ID dictionary
        AddAssets(pf.Sounds);
        AddAssets(pf.Sprites);
        AddAssets(pf.Backgrounds);
        AddAssets(pf.Fonts);
        AddAssets(pf.Paths);
        AddAssets(pf.Objects);
        AddAssets(pf.Rooms);

        // Make a map of code entry names to their references, to speed up further operations
        GMChunkCODE codeChunk = Project.DataHandle.GetChunk<GMChunkCODE>();
        foreach (var code in codeChunk.List)
            NameToCode[code.Name.Content] = code;

        // Initialize builtin variables/functions
        Builtins = new(this);
    }

    private void AddAssets<T>(AssetRefList<T> list) where T : Asset
    {
        foreach (var asset in list)
            AssetIds[asset.Name] = asset.DataIndex;
    }

    /// <summary>
    /// Adds a new code entry to this compilation context.
    /// </summary>
    public void AddCode(string name, string code, CodeContext.CodeMode mode = CodeContext.CodeMode.Replace, bool isScript = false)
    {
        Code.Add(new CodeContext(this, name, code, mode, isScript));
    }

    /// <summary>
    /// Compiles all of the code entries associated with this context, linking them with the project's data handle.
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    public bool Compile()
    {
        BuildFunctionInformation();

        // Tokenize all of the code
        foreach (var code in Code)
            Lexer.LexCode(code);

        // Expand interdependent macros
        HashSet<string> referencedMacros = new();
        foreach (var kvp in Macros)
        {
            referencedMacros.Clear();
            referencedMacros.Add(kvp.Key);

            ExpandMacro(kvp.Value, referencedMacros);
        }

        if (Errors.Count != 0)
            return false;

        // Perform basic processing on every token (expanding macros, identifying functions, etc)
        foreach (var code in Code)
            TokenProcessor.ProcessIdentifiers(code);

        if (Errors.Count != 0)
            return false;

        // Parse tokens
        foreach (var code in Code)
        {
            code.Position = 0;
            code.RootNode = new Node(NodeKind.Block);
            Parser.SkipSemicolons(code);
            while (!code.Errored && code.Tokens[code.Position].Kind != TokenKind.EOF)
            {
                code.RootNode.Children.Add(Parser.ParseStatement(code));
                Parser.SkipSemicolons(code);
            }
        }

        if (Errors.Count != 0)
            return false;

        // Expand interdependent enums
        ResolveEnums = true;
        CodeContext tempCtx = new(this, "@@temp@@", "", CodeContext.CodeMode.Replace, false);
        foreach (var _enum in Enums.Values)
        {
            foreach (var val in _enum.Values)
            {
                ReferencedEnums.Clear();
                ReferencedEnums.Add(_enum.Name);

                if (!val.HasValue && val.Node != null)
                    val.Node = NodeProcessor.ProcessNode(tempCtx, val.Node);
            }
        }
        ReferencedEnums.Clear();
        foreach (var _enum in Enums.Values)
        {
            long counter = 0;
            foreach (var val in _enum.Values)
            {
                if (val.Node == null)
                {
                    val.HasValue = true;
                    val.Value = counter++;
                }
                else if (val.HasValue)
                {
                    counter = val.Value + 1;
                }
                else
                    val.Node.Token.Context?.Error("Enum did not resolve (note: must be integer constants)", val.Node.Token);
            }
        }

        if (Errors.Count != 0)
            return false;

        // Perform optimizations and basic processing on the parse tree
        foreach (var code in Code)
            code.RootNode = NodeProcessor.ProcessNode(code, code.RootNode);

        if (Errors.Count != 0)
            return false;

        // Produce VM bytecode from the processed parse tree
        foreach (var code in Code)
            Bytecode.CompileStatement(code, code.RootNode);

        if (Errors.Count != 0)
            return false;

        // Process references
        foreach (var func in FunctionsToResolveLater)
            func.Resolve(this);
        foreach (var code in Code)
            Bytecode.ProcessReferences(code);

        if (Errors.Count != 0)
            return false;

        // Finally add code to data file
        LinkToData();

        return Errors.Count == 0;
    }

    private void BuildFunctionInformation()
    {
        if (IsGMS23)
        {
            // Add all 2.3 function declarations NOT included in scripts we're compiling now, if we're replacing its contents entirely
            List<string> codeEntryNames = new(Code.Count);
            foreach (var code in Code)
            {
                if (code.Mode != CodeContext.CodeMode.Replace)
                    continue;
                codeEntryNames.Add(code.Name);
            }
            foreach (var func in Decompiler.DecompileCache.Find23FunctionsNotIncluded(Project, codeEntryNames))
                Functions.Add(func.Key, new FunctionReference(func.Value));

            // Now add scripts--extension functions will count as functions, while scripts will not
            foreach (var ext in Project.DataHandle.GetChunk<GMChunkEXTN>().List)
            {
                foreach (GMExtension.ExtensionFile file in ext.Files)
                {
                    foreach (GMExtension.ExtensionFunction func in file.Functions)
                    {
                        Scripts.Add(func.Name.Content);
                        Functions.Add(func.Name.Content, new FunctionReference(func.Name.Content));
                    }
                }
            }
            var globalList = Project.DataHandle.GetChunk<GMChunkGLOB>().List;
            foreach (var scr in Project.DataHandle.GetChunk<GMChunkSCPT>().List)
            {
                if (globalList.Contains(scr.CodeID))
                    Scripts.Add(scr.Name.Content);
            }
        }
        else
        {
            // Scripts and extension functions are all functions prior to 2.3
            foreach (var scr in Project.DataHandle.GetChunk<GMChunkSCPT>().List)
                Scripts.Add(scr.Name.Content);
            foreach (var ext in Project.DataHandle.GetChunk<GMChunkEXTN>().List)
            {
                foreach (GMExtension.ExtensionFile file in ext.Files)
                {
                    foreach (GMExtension.ExtensionFunction func in file.Functions)
                        Scripts.Add(func.Name.Content);
                }
            }
            foreach (var scr in Scripts)
                Functions.Add(scr, new FunctionReference(scr));
        }
    }

    private void ExpandMacro(CodeContext macro, HashSet<string> referenced)
    {
        for (int i = 0; i < macro.Tokens.Count; i++)
        {
            Token curr = macro.Tokens[i];
            if (curr.Kind == TokenKind.Identifier && Macros.TryGetValue(curr.Text, out var nextMacro))
            {
                if (referenced.Add(curr.Text))
                {
                    // This macro hasn't been referenced yet in this expansion, so it's safe to use
                    ExpandMacro(nextMacro, referenced);
                    macro.Tokens.RemoveAt(i);
                    macro.Tokens.InsertRange(i, nextMacro.Tokens);
                }
                else
                {
                    macro.Error($"Recursive macro definition found for \"{curr.Text}\"", curr.Index);
                }
            }
        }
    }

    private string NextUnreferencedName(ref int index)
    {
        string currUnreferencedName;
        do
        {
            currUnreferencedName = $"@@DS_EMPTY_CODE_{index++}@@";
        }
        while (NameToCode.ContainsKey(currUnreferencedName));

        return currUnreferencedName;
    }

    private void LinkToData()
    {
        int unreferencedIndex = 0;

        GMChunkCODE codeChunk = Project.DataHandle.GetChunk<GMChunkCODE>();
        GMChunkGLOB glob = Project.DataHandle.GetChunk<GMChunkGLOB>();
        GMChunkSCPT scpt = Project.DataHandle.GetChunk<GMChunkSCPT>();
        GMChunkFUNC func = Project.DataHandle.GetChunk<GMChunkFUNC>();

        // Link all of our code contexts
        foreach (var code in Code)
        {
            if (NameToCode.TryGetValue(code.Name, out GMCode existing))
            {
                // This is an existing code entry
                int codeOffset = 0, codeLength = 0;
                switch (code.Mode)
                {
                    case CodeContext.CodeMode.Replace:
                        existing.BytecodeEntry.Instructions = code.Instructions;
                        codeLength = code.BytecodeLength;
                        break;
                    case CodeContext.CodeMode.InsertBegin:
                        existing.BytecodeEntry.Instructions.InsertRange(0, code.Instructions);
                        codeLength = existing.BytecodeEntry.GetLength() * 4;
                        break;
                    case CodeContext.CodeMode.InsertEnd:
                        codeOffset = existing.BytecodeEntry.GetLength() * 4;
                        existing.BytecodeEntry.Instructions.AddRange(code.Instructions);
                        codeLength = codeOffset + code.BytecodeLength;
                        break;
                }

                if (Project.DataHandle.VersionInfo.IsVersionAtLeast(2, 3))
                {
                    // Handle updating function declarations
                    List<GMCode> oldChildren = new(existing.ChildEntries);

                    foreach (var decl in code.FunctionDeclsToRegister)
                    {
                        GMCode declCodeEntry = oldChildren.Find(c => c.Name.Content == decl.Reference.Name);
                        if (declCodeEntry == null)
                        {
                            if (code.Mode == CodeContext.CodeMode.ReplaceFunctions)
                            {
                                if (decl.Parent == null)
                                {
                                    // Ignore unreferenced functions
                                    continue;
                                }
                                
                                // Theoretically by this point, the parent function should have had its bytecode emitted,
                                // and this includes this sub-function. We just need to add the data referencing it properly now.
                                declCodeEntry = new()
                                {
                                    Name = Project.DataHandle.DefineString(decl.Reference.Name),
                                    LocalsCount = (short)decl.LocalCount,
                                    ArgumentsCount = (short)decl.ArgCount,
                                    Length = 0,
                                    BytecodeOffset = decl.Parent.InsertedOffset + (decl.Offset - decl.Parent.Offset),
                                    ParentEntry = existing,
                                    BytecodeEntry = existing.BytecodeEntry
                                };
                                existing.ChildEntries.Add(declCodeEntry);
                                
                                if (NameToCode.ContainsKey(decl.Reference.Name))
                                {
                                    // There's a conflicting code entry already, outside of this code
                                    throw new System.Exception($"Code entry with name \"{decl.Reference.Name}\" already exists in game");
                                }
                                else
                                {
                                    // Add to CODE chunk
                                    codeChunk.List.Add(declCodeEntry);

                                    // Add script entry
                                    scpt.List.Add(new()
                                    {
                                        Name = Project.DataHandle.DefineString(decl.Reference.Name),
                                        CodeID = codeChunk.List.Count - 1,
                                        Constructor = decl.Constructor
                                    });
                                }
                            }
                            else
                            {
                                // Need to make a new entry
                                declCodeEntry = new()
                                {
                                    Name = Project.DataHandle.DefineString(decl.Reference.Name),
                                    LocalsCount = (short)decl.LocalCount,
                                    ArgumentsCount = (short)decl.ArgCount,
                                    Length = codeLength,
                                    BytecodeOffset = codeOffset + decl.Offset,
                                    ParentEntry = existing,
                                    BytecodeEntry = existing.BytecodeEntry
                                };
                                existing.ChildEntries.Add(declCodeEntry);

                                if (NameToCode.ContainsKey(decl.Reference.Name))
                                {
                                    // There's a conflicting code entry already, outside of this code
                                    throw new System.Exception($"Code entry with name \"{decl.Reference.Name}\" already exists in game");
                                }
                                else
                                {
                                    // Add to CODE chunk
                                    codeChunk.List.Add(declCodeEntry);

                                    // Add script entry
                                    scpt.List.Add(new()
                                    {
                                        Name = Project.DataHandle.DefineString(decl.Reference.Name),
                                        CodeID = codeChunk.List.Count - 1,
                                        Constructor = decl.Constructor
                                    });
                                }
                            }
                        }
                        else
                        {
                            if (code.Mode == CodeContext.CodeMode.Replace)
                            {
                                // Change existing entry to point to new one
                                declCodeEntry.LocalsCount = (short)decl.LocalCount;
                                declCodeEntry.ArgumentsCount = (short)decl.ArgCount;
                                declCodeEntry.Length = codeLength;
                                declCodeEntry.BytecodeOffset = codeOffset + decl.Offset;
                                declCodeEntry.Flags = 0;

                                // Modify script entry
                                GMScript declScript = scpt.List.Find(s => s.Name.Content == decl.Reference.Name);
                                declScript.Constructor = decl.Constructor;

                                // Remove from old children list
                                oldChildren.Remove(declCodeEntry);
                            }
                            else if (code.Mode == CodeContext.CodeMode.ReplaceFunctions)
                            {
                                // Find index of first (inclusive) instruction and last (exclusive) instruction
                                int index = 0;
                                while (existing.BytecodeEntry.Instructions[index].Address < declCodeEntry.BytecodeOffset - 4)
                                    index++;
                                var branchInstruction = existing.BytecodeEntry.Instructions[index];
                                int oldEndOffset = declCodeEntry.BytecodeOffset + (branchInstruction.JumpOffset * 4) - 4;
                                int oldStartIndex = ++index;
                                while (existing.BytecodeEntry.Instructions[index].Address < oldEndOffset)
                                    index++;
                                int oldEndIndex = index;
                                
                                // Replace instructions in place
                                decl.InsertedOffset = declCodeEntry.BytecodeOffset;
                                existing.BytecodeEntry.Instructions.RemoveRange(oldStartIndex, oldEndIndex - oldStartIndex);
                                existing.BytecodeEntry.Instructions.InsertRange(oldStartIndex, 
                                    code.Instructions.Skip(decl.StartIndex).Take(decl.EndIndex - decl.StartIndex));
                                
                                // Update info
                                declCodeEntry.LocalsCount = (short)decl.LocalCount;
                                declCodeEntry.ArgumentsCount = (short)decl.ArgCount;
                                declCodeEntry.Flags = 0;
                                
                                // Update addresses
                                int newSize = (decl.EndOffset - decl.Offset);
                                int sizeDifference = newSize - (oldEndOffset - declCodeEntry.BytecodeOffset);
                                branchInstruction.JumpOffset = (newSize / 4) + 1;
                                foreach (var child in existing.ChildEntries)
                                {
                                    if (child.BytecodeOffset > oldEndOffset)
                                        child.BytecodeOffset += sizeDifference;
                                }
                                for (; index < existing.BytecodeEntry.Instructions.Count; index++)
                                    existing.BytecodeEntry.Instructions[index].Address += sizeDifference;
                            }
                            else
                            {
                                // Shouldn't be redeclaring an existing sub-code entry, so error out
                                throw new System.Exception($"Duplicate function declaration named \"{decl.Reference.Name}\"");
                            }
                        }
                    }

                    if (code.Mode == CodeContext.CodeMode.Replace)
                    {
                        // Get rid of unreferenced children
                        foreach (var child in oldChildren)
                        {
                            // Generate new (unique) name for this
                            GMString name = Project.DataHandle.DefineString(
                                                NextUnreferencedName(ref unreferencedIndex));

                            // Rename function
                            GMFunctionEntry funcEntry = func.FindOrDefine(child.Name.Content, Project.DataHandle);
                            funcEntry.Name = name;

                            // Clear out script entry
                            GMScript declScript = scpt.List.Find(s => s.Name.Content == child.Name.Content);
                            declScript.Name = name;
                            declScript.Constructor = false;

                            // Clear out code entry
                            child.Name = name;
                            child.Length = 0;
                            child.BytecodeOffset = 0;
                            child.BytecodeEntry = new GMCode.Bytecode(child);
                            child.Flags = 0;
                            child.LocalsCount = 0;
                            child.ArgumentsCount = 0;
                            child.ParentEntry = null;

                            // Add locals entry since this is now standalone
                            GMLocalsEntry fakeLocals = new(child.Name);
                            func.Locals.Add(fakeLocals);
                            fakeLocals.AddLocal(Project.DataHandle, "arguments", child);
                        }
                    }
                    else if (code.Mode == CodeContext.CodeMode.InsertBegin)
                    {
                        // Push old children back by inserted instruction length, and update their length
                        foreach (var child in oldChildren)
                        {
                            child.BytecodeOffset += code.BytecodeLength;
                            child.Length = codeLength;
                        }
                    }
                    else if (code.Mode == CodeContext.CodeMode.ReplaceFunctions)
                    {
                        // Update code lengths
                        codeLength = existing.BytecodeEntry.GetLength() * 4;
                        foreach (var child in existing.ChildEntries)
                            child.Length = codeLength;
                    }
                }
            }
            else
            {
                if (code.Mode == CodeContext.CodeMode.ReplaceFunctions)
                    throw new System.Exception($"Code entry must exist in order for ReplaceFunctions mode to work (\"{code.Name}\").");
                
                // This is a new code entry - create it
                GMCode newEntry = new() 
                { 
                    Name = Project.DataHandle.DefineString(code.Name), 
                    LocalsCount = 1 
                };
                newEntry.BytecodeEntry = new(newEntry) { Instructions = code.Instructions };
                codeChunk.List.Add(newEntry);

                // Properly finish local variables
                func.Locals.Add(code.CodeLocals);
                newEntry.LocalsCount = (short)code.CodeLocals.Entries.Count;

                if (Project.DataHandle.VersionInfo.IsVersionAtLeast(2, 3))
                {
                    if (code.IsScript)
                    {
                        // Add to global init scripts
                        glob.List.Add(codeChunk.List.Count - 1);

                        // Add to actual scripts
                        scpt.List.Add(new()
                        {
                            Name = Project.DataHandle.DefineString(code.ScriptName),
                            CodeID = codeChunk.List.Count - 1,
                            Constructor = false
                        });
                    }
                        
                    // Handle function declarations
                    foreach (var decl in code.FunctionDeclsToRegister)
                    {
                        // Add code entry, as a child
                        GMCode declCodeEntry = new() 
                        { 
                            Name = Project.DataHandle.DefineString(decl.Reference.Name), 
                            LocalsCount = (short)decl.LocalCount,
                            ArgumentsCount = (short)decl.ArgCount,
                            Length = code.BytecodeLength,
                            BytecodeOffset = decl.Offset,
                            ParentEntry = newEntry,
                            BytecodeEntry = newEntry.BytecodeEntry
                        };
                        newEntry.ChildEntries.Add(declCodeEntry);
                        codeChunk.List.Add(declCodeEntry);

                        // Add script entry
                        scpt.List.Add(new()
                        {
                            Name = Project.DataHandle.DefineString(decl.Reference.Name),
                            CodeID = codeChunk.List.Count - 1,
                            Constructor = decl.Constructor
                        });
                    }
                }
                else if (code.IsScript)
                {
                    // Add script entry
                    scpt.List.Add(new()
                    {
                        Name = Project.DataHandle.DefineString(code.Name),
                        CodeID = codeChunk.List.Count - 1,
                        Constructor = false
                    });
                }
            }
        }
    }
}

public class CodeContext
{
    public enum CodeKind
    {
        Script,
        Macro
    }

    public enum CodeMode
    {
        Replace,
        InsertBegin,
        InsertEnd,
        ReplaceFunctions
    }

    public CompileContext BaseContext { get; set; }
    public CodeKind Kind { get; set; } = CodeKind.Script;
    public CodeMode Mode { get; set; } = CodeMode.Replace;
    public string Name { get; init; }
    public string ScriptName { get; init; }
    public string Code { get; init; }
    public bool IsScript { get; init; }

    public int Position { get; set; } = 0;
    public List<Token> Tokens { get; set; } = null;
    public Node RootNode { get; set; } = null;
    public bool Errored { get; set; } = false;
    public List<string> ReferencedLocalVars { get; set; } = new();
    public List<string> LocalVars { get; set; } = new();
    public List<string> StaticVars { get; set; } = new();
    public List<string> ArgumentVars { get; set; } = new();
    public string CurrentName { get; set; }
    public Node FunctionBeginBlock { get; set; } = null;
    public Node FunctionStatic { get; set; } = null;

    // For bytecode
    public List<Instruction> Instructions { get; set; } = new(64);
    public int BytecodeLength { get; set; } = 0;
    public Stack<DataType> TypeStack { get; set; } = new();
    public bool InStaticBlock { get; set; } = false;
    public List<Bytecode.FunctionPatch> FunctionPatches { get; set; } = new();
    public List<Bytecode.VariablePatch> VariablePatches { get; set; } = new();
    public List<Bytecode.StringPatch> StringPatches { get; set; } = new();
    public Stack<Bytecode.Context> BytecodeContexts { get; set; } = new();
    public List<FunctionDeclInfo> FunctionDeclsToRegister { get; set; } = new();
    public FunctionDeclInfo ParentFunctionDecl { get; set; } = null;
    public GMLocalsEntry CodeLocals { get; set; } = null;

    public CodeContext(CompileContext baseContext, string name, string code, CodeMode mode, bool isScript)
    {
        BaseContext = baseContext;
        if (isScript && BaseContext.IsGMS23)
        {
            ScriptName = name;
            name = "gml_GlobalScript_" + name;
        }
        Name = name;
        CurrentName = name;
        Code = code;
        Mode = mode;
        IsScript = isScript;
    }

    public void Error(string message, int index)
    {
        Errored = true;

        if (index == -1)
        {
            BaseContext.Errors.Add(new(this, message));
            return;
        }

        // Count lines/columns
        int line = 1;
        int column = 1;
        for (int i = 0; i < index; i++)
        {
            if (Code[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
                column++;
        }
        BaseContext.Errors.Add(new(this, message, line, column));
    }

    public void Error(string message, Token token)
    {
        Error(message, token?.Index ?? -1);
    }
}

public class ErrorMessage
{
    public CodeContext Context { get; init; }
    public string Message { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }

    public ErrorMessage(CodeContext context, string message, int line = -1, int column = -1)
    {
        Context = context;
        Message = message;
        Line = line;
        Column = column;
    }
}

public class Enum
{
    public string Name { get; init; }
    public List<EnumValue> Values { get; init; } = new();

    public Enum(string name)
    {
        Name = name;
    }

    public bool Contains(string name)
    {
        return Values.Any(x => x.Name == name);
    }

    public bool TryGetValue(string name, out EnumValue val)
    {
        val = Values.Find(x => x.Name == name);
        return val != null;
    }
}

public class EnumValue
{
    public string Name { get; init; }
    public bool HasValue { get; set; } = false;
    public long Value { get; set; }

    private Node _node;
    public Node Node { get => _node; set { _node = value; CheckForValue(); } }

    public EnumValue(string name, Node node)
    {
        Name = name;
        Node = node;
    }

    private void CheckForValue()
    {
        if (Node != null && Node.Kind == NodeKind.Constant)
        {
            var constant = (Node.Token.Value as TokenConstant);
            if (constant.Kind == ConstantKind.Number)
            {
                HasValue = true;
                Value = (long)constant.ValueNumber;
            }
            else if (constant.Kind == ConstantKind.Int64)
            {
                HasValue = true;
                Value = constant.ValueInt64;
            }
        }
    }
}

public class FunctionReference
{
    public string Name { get; private set; }
    public bool Resolved { get; set; } = false;
    public bool Anonymous { get; init; }
    public GMFunctionEntry DataEntry { get; set; } = null;

    // Used for pre-2.3 scripts and extension functions
    public FunctionReference(string name)
    {
        Name = name;
        Anonymous = false;
    }

    // Used for pre-existing function declarations (found by light decompilation)
    public FunctionReference(GMFunctionEntry entry)
    {
        Name = entry.Name.Content;
        Resolved = true;
        DataEntry = entry;
        Anonymous = false;
    }

    // Used for function declarations
    public FunctionReference(CompileContext ctx, string internalName, bool anonymous)
    {
        Name = internalName;
        Anonymous = anonymous;

        // All function declarations are referenced (by at least their own declaration),
        // so we should resolve them.
        ctx.FunctionsToResolveLater.Add(this);
    }

    // Called when a function is referenced (i.e. should be added to function list)
    public void Resolve(CompileContext ctx)
    {
        if (Resolved)
            return;

        if (Anonymous)
        {
            if (ctx.NameToCode.ContainsKey(Name))
            {
                // Uh oh, we're conflicting with an existing code entry name!
                // Need to rename this now.
                int counter = 0;
                string curr;
                do
                {
                    curr = Name + $"_{counter++}";
                }
                while (ctx.NameToCode.ContainsKey(curr));
                Name = curr;
            }
        }

        var func = ctx.Project.DataHandle.GetChunk<GMChunkFUNC>();
        DataEntry = func.FindOrDefine(Name, ctx.Project.DataHandle);
        Resolved = true;
    }
}

public record FunctionDeclInfo
{
    public FunctionReference Reference { get; set; }
    public int LocalCount { get; set; }
    public int ArgCount { get; set; }
    public int Offset { get; set; }
    public int EndOffset { get; set; }
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
    public bool Constructor { get; set; }
    public FunctionDeclInfo Parent { get; set; }
    public int InsertedOffset { get; set; }
}