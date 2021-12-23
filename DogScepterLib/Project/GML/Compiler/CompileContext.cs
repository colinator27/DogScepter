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
        public Dictionary<string, int> AssetIds = new();

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

        public CodeContext(CompileContext baseContext, string name, string code)
        {
            BaseContext = baseContext;
            Name = name;
            Code = code;
        }

        public void Error(string message, int index)
        {
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
            // Count lines/columns
            int line = 1;
            int column = 1;
            for (int i = 0; i < token.Index; i++)
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
    }

    public class ErrorMessage
    {
        public CodeContext Context { get; init; }
        public string Message { get; init; }
        public int Line { get; init; }
        public int Column { get; init; }

        public ErrorMessage(CodeContext context, string message, int line, int column)
        {
            Context = context;
            Message = message;
            Line = line;
            Column = column;
        }
    }
}
