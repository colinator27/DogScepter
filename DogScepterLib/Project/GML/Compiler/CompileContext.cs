using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Compiler
{
    public class CompileContext
    {
        public ProjectFile Project { get; init; }
        public Builtins Builtins { get; init; }
        public List<CodeContext> Code { get; init; } = new();
        public bool IsGMS2 { get; init; }
        public bool IsGMS23 { get; init; }
        public List<ErrorMessage> Errors { get; init; } = new();
        public Dictionary<string, CodeContext> Macros { get; init; } = new();
        public Dictionary<string, Enum> Enums { get; init; } = new();
        public bool ResolveEnums { get; set; } = false;
        public HashSet<string> ReferencedEnums { get; init; } = new();
        public Dictionary<string, int> AssetIds = new();
        public Dictionary<string, int> VariableIds = new();

        public CompileContext(ProjectFile pf)
        {
            Project = pf;
            IsGMS2 = pf.DataHandle.VersionInfo.IsNumberAtLeast(2);
            IsGMS23 = pf.DataHandle.VersionInfo.IsNumberAtLeast(2, 3);
            
            // Populate asset ID dictionary
            AddAssets(pf.Sounds);
            AddAssets(pf.Sprites);
            AddAssets(pf.Backgrounds);
            AddAssets(pf.Fonts);
            AddAssets(pf.Paths);
            AddAssets(pf.Objects);
            AddAssets(pf.Rooms);

            // Initialize builtin variables/functions
            Builtins = new(this);
        }

        private void AddAssets<T>(AssetRefList<T> list) where T : Asset
        {
            foreach (var asset in list)
                AssetIds[asset.Name] = asset.DataIndex;
        }

        public void AddCode(string name, string code)
        {
            Code.Add(new CodeContext(this, name, code));
        }

        public bool Compile()
        {
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
            foreach (var _enum in Enums.Values)
            {
                foreach (var val in _enum.Values)
                {
                    ReferencedEnums.Clear();
                    ReferencedEnums.Add(_enum.Name);

                    if (!val.HasValue && val.Node != null)
                        val.Node = NodeProcessor.ProcessNode(this, val.Node);
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
                code.RootNode = NodeProcessor.ProcessNode(this, code.RootNode);

            if (Errors.Count != 0)
                return false;

            return true;
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
    }

    public class CodeContext
    {
        public enum CodeKind
        {
            Script,
            Macro
        }

        public CompileContext BaseContext { get; set; }
        public CodeKind Kind { get; set; } = CodeKind.Script;
        public string Name { get; init; }
        public string Code { get; init; }

        public int Position { get; set; } = 0;
        public List<Token> Tokens { get; set; } = null;
        public Node RootNode { get; set; } = null;
        public bool Errored { get; set; } = false;
        public List<string> LocalVars { get; set; } = new();
        public List<string> StaticVars { get; set; } = new();
        public List<string> ArgumentVars { get; set; } = new();
        public Node FunctionBeginBlock { get; set; } = null;
        public Node FunctionStatic { get; set; } = null;

        public CodeContext(CompileContext baseContext, string name, string code)
        {
            BaseContext = baseContext;
            Name = name;
            Code = code;
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

        public void CheckForValue()
        {
            if (Node != null && Node.Kind == NodeKind.Constant && (Node.Token.Value as TokenConstant).Kind != ConstantKind.String)
            {
                HasValue = true;

                var constant = (Node.Token.Value as TokenConstant);
                if (constant.Kind == ConstantKind.Number)
                    Value = (long)constant.ValueNumber;
                else if (constant.Kind == ConstantKind.Int64)
                    Value = constant.ValueInt64;
            }
        }
    }
}
