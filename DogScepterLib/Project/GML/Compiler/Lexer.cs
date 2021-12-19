using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Project.GML.Compiler
{
    public enum TokenKind
    {
        EOF,
        Error,

        Identifier,
        Number,
        String,

        Begin,
        End,
        Open,
        Close,
        Comma,
        Dot,
        Colon,
        Semicolon,
        ArrayOpen,
        ArrayClose,

        ArrayListOpen,
        ArrayMapOpen,
        ArrayGridOpen,
        ArrayDirectOpen,
        ArrayStructOpen,

        Equal,
        NotEqual,
        Greater,
        GreaterEqual,
        Lesser,
        LesserEqual,

        Assign,
        Plus,
        Increment,
        AssignPlus,
        Minus,
        Decrement,
        AssignMinus,
        Times,
        AssignTimes,
        Divide,
        AssignDivide,
        Mod,
        AssignMod,

        And,
        Or,
        Xor,
        BitAnd,
        BitOr,
        BitXor,
        AssignAnd,
        AssignOr,
        AssignXor,
        BitNegate,

        Conditional,
        NullCoalesce,
        AssignNullCoalesce,

        While,
        With,
        If,
        Do,
        Not,
        Enum,
        Var,
        Globalvar,
        Return,
        Default,
        For,
        Case,
        Switch,
        Until,
        Continue,
        Break,
        Else,
        Repeat,
        Exit,
        Then,
        Div,
        Function,
        New,
        Delete,
        Throw,
        Try,
        Catch,
        Finally,
        Static,
    }

    public class Token
    {
        public TokenKind Kind { get; set; }
        public int Index { get; set; }
        public string Text { get; set; }

        public Token(TokenKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public Token(TokenKind kind, int index, string text)
        {
            Kind = kind;
            Index = index;
            Text = text;
        }
    }

    public class Lexer
    {
        public static void LexCode(CompileContext ctx)
        {
            ctx.Tokens = new();
            ctx.Position = 0;
            Token next;
            do
            {
                next = GetNextToken(ctx);
                ctx.Tokens.Add(next);
            }
            while (next.Kind != TokenKind.EOF && next.Kind != TokenKind.Error);
        }

        private static Token GetNextToken(CompileContext ctx)
        {
            SkipWhitespace(ctx);
            if (ctx.Position >= ctx.Code.Length)
                return new Token(TokenKind.EOF, ctx.Position);

            char c = ctx.Code[ctx.Position];
            char lookahead = (ctx.Position + 1 < ctx.Code.Length) ? ctx.Code[ctx.Position + 1] : '\0';

            // TODO: Handle macros?

            // Identifiers
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_')
            {
                return ReadIdentifier(ctx);
            }
            
            // Numbers/hex
            if (char.IsDigit(c))
            {
                if (c == '0' && lookahead == 'x')
                    return ReadHex(ctx);
                return ReadNumber(ctx);
            }
            if (c == '$')
                return ReadHex(ctx);
            if (c == '.' && char.IsDigit(lookahead))
                return ReadNumber(ctx);

            // Strings
            if (ctx.IsGMS2)
            {
                if (c == '@' && (lookahead == '"' || lookahead == '\''))
                {
                    ctx.Position++;
                    return ReadVerbatimString(ctx);
                }
                if (c == '"')
                    return ReadString(ctx);
            }
            else
            {
                if (c == '"' || c == '\'')
                    return ReadVerbatimString(ctx);
            }

            switch (c)
            {
                case '{':
                    return new Token(TokenKind.Begin, ctx.Position++);
                case '}':
                    return new Token(TokenKind.End, ctx.Position++);
                case '(':
                    return new Token(TokenKind.Open, ctx.Position++);
                case ')':
                    return new Token(TokenKind.Close, ctx.Position++);
                case '=':
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.Equal, startPos);
                    }
                    return new Token(TokenKind.Assign, ctx.Position++);
                case '+':
                    if (lookahead == '+')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.Increment, startPos);
                    }
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.AssignPlus, startPos);
                    }
                    return new Token(TokenKind.Plus, ctx.Position++);
                case '-':
                    if (lookahead == '-')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.Decrement, startPos);
                    }
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.AssignMinus, startPos);
                    }
                    return new Token(TokenKind.Minus, ctx.Position++);
                case '*':
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.AssignTimes, startPos);
                    }
                    return new Token(TokenKind.Times, ctx.Position++);
                case '/':
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.AssignDivide, startPos);
                    }
                    return new Token(TokenKind.Divide, ctx.Position++);
                case '!':
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.NotEqual, startPos);
                    }
                    return new Token(TokenKind.Not, ctx.Position++);
                case ',':
                    return new Token(TokenKind.Comma, ctx.Position++);
                case '.':
                    return new Token(TokenKind.Dot, ctx.Position++);
                case ':':
                    return new Token(TokenKind.Colon, ctx.Position++);
                case ';':
                    return new Token(TokenKind.Semicolon, ctx.Position++);
                case '[':
                    switch (lookahead)
                    {
                        case '|':
                            {
                                int startPos = ctx.Position;
                                ctx.Position += 2;
                                return new Token(TokenKind.ArrayListOpen, startPos);
                            }
                        case '?':
                            {
                                int startPos = ctx.Position;
                                ctx.Position += 2;
                                return new Token(TokenKind.ArrayMapOpen, startPos);
                            }
                        case '#':
                            {
                                int startPos = ctx.Position;
                                ctx.Position += 2;
                                return new Token(TokenKind.ArrayGridOpen, startPos);
                            }
                        case '@':
                            {
                                int startPos = ctx.Position;
                                ctx.Position += 2;
                                return new Token(TokenKind.ArrayDirectOpen, startPos);
                            }
                        case '$':
                            {
                                int startPos = ctx.Position;
                                ctx.Position += 2;
                                return new Token(TokenKind.ArrayStructOpen, startPos);
                            }
                    }
                    return new Token(TokenKind.ArrayOpen, ctx.Position++);
                case ']':
                    return new Token(TokenKind.ArrayClose, ctx.Position++);
                case '?':
                    if (lookahead == '?')
                    {
                        int startPos = ctx.Position;
                        if (ctx.Position < ctx.Code.Length &&
                            ctx.Code[ctx.Position] == '=')
                        {
                            ctx.Position += 3;
                            return new Token(TokenKind.AssignNullCoalesce, startPos);
                        }
                        ctx.Position += 2;
                        return new Token(TokenKind.NullCoalesce, startPos);
                    }
                    return new Token(TokenKind.Conditional, ctx.Position++);
                case '%':
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.AssignMod, startPos);
                    }
                    return new Token(TokenKind.Mod, ctx.Position++);
                case '&':
                    if (lookahead == '&')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.And, startPos);
                    }
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.AssignAnd, startPos);
                    }
                    return new Token(TokenKind.BitAnd, ctx.Position++);
                case '|':
                    if (lookahead == '|')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.Or, startPos);
                    }
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.AssignOr, startPos);
                    }
                    return new Token(TokenKind.BitOr, ctx.Position++);
                case '^':
                    if (lookahead == '^')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.Xor, startPos);
                    }
                    if (lookahead == '=')
                    {
                        int startPos = ctx.Position;
                        ctx.Position += 2;
                        return new Token(TokenKind.AssignXor, startPos);
                    }
                    return new Token(TokenKind.BitXor, ctx.Position++);
                case '~':
                    return new Token(TokenKind.BitNegate, ctx.Position++);
            }

            Token err = new(TokenKind.Error, ctx.Position++);
            ctx.Error("Invalid token", err);
            return err;
        }

        private static void SkipWhitespace(CompileContext ctx)
        {
            bool stillWhitespace = true;
            while (stillWhitespace)
            {
                // Basic whitespace skipping
                while (ctx.Position < ctx.Code.Length && char.IsWhiteSpace(ctx.Code[ctx.Position]))
                    ctx.Position++;

                // Comment skipping
                if (ctx.Position < ctx.Code.Length && ctx.Code[ctx.Position] == '/')
                {
                    if (ctx.Position + 1 < ctx.Code.Length)
                    {
                        char ahead = ctx.Code[ctx.Position + 1];
                        switch (ahead)
                        {
                            case '/':
                                ctx.Position += 2;
                                while (ctx.Position < ctx.Code.Length)
                                {
                                    if (ctx.Code[ctx.Position] == '\n')
                                        break;
                                    ctx.Position++;
                                }
                                break;
                            case '*':
                                ctx.Position += 2;
                                while (ctx.Position + 1 < ctx.Code.Length)
                                {
                                    if (ctx.Code[ctx.Position] == '*' && ctx.Code[ctx.Position + 1] == '/')
                                        break;
                                    ctx.Position++;
                                }
                                if (ctx.Position + 1 < ctx.Code.Length)
                                    ctx.Position += 2;
                                else
                                    ctx.Position = ctx.Code.Length; // EOF
                                break;
                            default:
                                // This isn't a comment, whitespace is over
                                stillWhitespace = false;
                                break;
                        }
                    }
                    else
                    {
                        // This can't be a comment (EOF)
                        stillWhitespace = false;
                    }
                }
                else
                {
                    // Other characters (or EOF), whitespace is over
                    stillWhitespace = false;
                }
            }
        }

        private static Token ReadIdentifier(CompileContext ctx)
        {
            int startPosition = ctx.Position;

            StringBuilder sb = new();
            sb.Append(ctx.Code[ctx.Position++]);
            while (ctx.Position < ctx.Code.Length)
            {
                char c = ctx.Code[ctx.Position];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')
                {
                    sb.Append(c);
                    ctx.Position++;
                }
                else
                    break;
            }

            string identifier = sb.ToString();
            return identifier switch
            {
                "and" => new Token(TokenKind.And, startPosition),
                "or" => new Token(TokenKind.Or, startPosition),
                "xor" => new Token(TokenKind.Xor, startPosition),
                "while" => new Token(TokenKind.While, startPosition),
                "with" => new Token(TokenKind.With, startPosition),
                "if" => new Token(TokenKind.If, startPosition),
                "do" => new Token(TokenKind.Do, startPosition),
                "not" => new Token(TokenKind.Not, startPosition),
                "enum" => new Token(TokenKind.Enum, startPosition),
                "begin" => new Token(TokenKind.Begin, startPosition),
                "end" => new Token(TokenKind.End, startPosition),
                "var" => new Token(TokenKind.Var, startPosition),
                "globalvar" => new Token(TokenKind.Globalvar, startPosition),
                "return" => new Token(TokenKind.Return, startPosition),
                "default" => new Token(TokenKind.Default, startPosition),
                "for" => new Token(TokenKind.For, startPosition),
                "case" => new Token(TokenKind.Case, startPosition),
                "switch" => new Token(TokenKind.Switch, startPosition),
                "until" => new Token(TokenKind.Until, startPosition),
                "continue" => new Token(TokenKind.Continue, startPosition),
                "break" => new Token(TokenKind.Break, startPosition),
                "else" => new Token(TokenKind.Else, startPosition),
                "repeat" => new Token(TokenKind.Repeat, startPosition),
                "exit" => new Token(TokenKind.Exit, startPosition),
                "then" => new Token(TokenKind.Then, startPosition),
                "mod" => new Token(TokenKind.Mod, startPosition),
                "div" => new Token(TokenKind.Div, startPosition),
                "function" => new Token(TokenKind.Function, startPosition),
                "new" => new Token(TokenKind.New, startPosition),
                "delete" => new Token(TokenKind.Delete, startPosition),
                "throw" => new Token(TokenKind.Throw, startPosition),
                "try" => new Token(TokenKind.Try, startPosition),
                "catch" => new Token(TokenKind.Catch, startPosition),
                "finally" => new Token(TokenKind.Finally, startPosition),
                "static" => new Token(TokenKind.Static, startPosition),
                _ => new Token(TokenKind.Identifier, startPosition, identifier)
            };
        }

        private static Token ReadNumber(CompileContext ctx)
        {
            int startPosition = ctx.Position;

            StringBuilder sb = new();
            sb.Append(ctx.Code[ctx.Position++]);
            while (ctx.Position < ctx.Code.Length)
            {
                char c = ctx.Code[ctx.Position];
                if (char.IsDigit(c) || c == '.')
                {
                    sb.Append(c);
                    ctx.Position++;
                }
                else
                    break;
            }

            return new Token(TokenKind.Number, startPosition, sb.ToString());
        }

        private static Token ReadHex(CompileContext ctx)
        {
            int startPosition = ctx.Position;

            StringBuilder sb = new();
            sb.Append(ctx.Code[ctx.Position++]);
            if (ctx.Code[ctx.Position] == 'x')
            {
                sb.Append('x');
                ctx.Position++;
            }
            while (ctx.Position < ctx.Code.Length)
            {
                char c = ctx.Code[ctx.Position];
                if (char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
                {
                    sb.Append(c);
                    ctx.Position++;
                }
                else
                    break;
            }

            return new Token(TokenKind.Number, startPosition, sb.ToString());
        }

        private static Token ReadVerbatimString(CompileContext ctx)
        {
            int startPosition = ctx.Position;

            StringBuilder sb = new();
            char startChar = ctx.Code[ctx.Position++];

            while (ctx.Position < ctx.Code.Length)
            {
                char c = ctx.Code[ctx.Position];
                if (c == startChar)
                    break;
                sb.Append(c);
                ctx.Position++;
            }

            return new Token(TokenKind.String, startPosition, sb.ToString());
        }

        private static Token ReadString(CompileContext ctx)
        {
            int startPosition = ctx.Position;

            StringBuilder sb = new();
            while (ctx.Position < ctx.Code.Length)
            {
                char c = ctx.Code[ctx.Position];
                if (c == '"')
                    break;
                if (c == '\\')
                {
                    ctx.Position++;
                    if (ctx.Position < ctx.Code.Length)
                    {
                        // Escape codes
                        c = ctx.Code[ctx.Position++];
                        switch (c)
                        {
                            case '\n':
                                // Ignore newline
                                continue;
                            case 'a':
                                sb.Append('\a');
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 'f':
                                sb.Append('\f');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                            case 'v':
                                sb.Append('\v');
                                break;
                            case 'u':
                                {
                                    // Unicode character (as hex)
                                    ctx.Position++;
                                    int result = 0;
                                    int charsRead = 0;
                                    while (ctx.Position < ctx.Code.Length && charsRead < 6)
                                    {
                                        // Read current character as 4 bits (one hex character)
                                        int curr = ConvertHexToInt(ctx.Code[ctx.Position]);
                                        if (curr == -1)
                                            break;
                                        result = (result << 4) + curr;
                                        ctx.Position++;
                                        charsRead++;
                                    }
                                    if (charsRead != 0)
                                    {
                                        try
                                        {
                                            sb.Append(char.ConvertFromUtf32(result));
                                        }
                                        catch (ArgumentOutOfRangeException)
                                        {
                                            ctx.Error("\\u value in string not in valid range.", new Token(TokenKind.String, startPosition, sb.ToString()));
                                        }
                                    }
                                }
                                break;
                            case 'x':
                                {
                                    // Hex character
                                    ctx.Position++;
                                    int result = 0;
                                    int charsRead = 0;
                                    while (ctx.Position < ctx.Code.Length && charsRead < 2)
                                    {
                                        // Read current character as 4 bits (one hex character)
                                        int curr = ConvertHexToInt(ctx.Code[ctx.Position]);
                                        if (curr == -1)
                                            break;
                                        result = (result << 4) + curr;
                                        ctx.Position++;
                                        charsRead++;
                                    }
                                    if (charsRead == 2)
                                        sb.Append((char)result);
                                    else
                                        ctx.Error("\\x value in string is missing valid hex characters.", new Token(TokenKind.String, startPosition, sb.ToString()));
                                }
                                break;
                            default:
                                {
                                    if (c >= '0' && c <= '7')
                                    {
                                        // Octal character
                                        ctx.Position++;
                                        int result = 0;
                                        int charsRead = 0;
                                        while (ctx.Position < ctx.Code.Length && charsRead < 3)
                                        {
                                            // Read current character as octal
                                            c = ctx.Code[ctx.Position];
                                            if (c < '0' || c > '7')
                                                break;
                                            result = (result * 8) + (c - '0');
                                            ctx.Position++;
                                            charsRead++;
                                        }
                                        if (charsRead == 3)
                                            sb.Append((char)result);
                                        else
                                            ctx.Error("\\??? octal value in string is missing valid octal characters.", new Token(TokenKind.String, startPosition, sb.ToString()));
                                    }
                                    else
                                    {
                                        // Verbatim character
                                        sb.Append(c);
                                    }
                                }
                                break;
                        }
                    }
                }
                else if (c == '\n')
                    ctx.Error("Cannot have raw newlines in normal strings.", new Token(TokenKind.String, startPosition, sb.ToString()));
                else
                {
                    sb.Append(c);
                    ctx.Position++;
                }
            }

            return new Token(TokenKind.String, startPosition, sb.ToString());
        }

        private static int ConvertHexToInt(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return 10 + (c - 'a');
            if (c >= 'A' && c <= 'F')
                return 10 + (c - 'A');
            return -1;
        }
    }
}
