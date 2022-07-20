using DogScepterLib.Core.Models;

namespace DogScepterLib.Project.GML.Compiler;

public enum TokenKind
{
    EOF,
    Error,

    Identifier,

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
    BitShiftLeft,
    BitShiftRight,

    Conditional,
    NullCoalesce,
    AssignNullCoalesce,

    _KeywordsBegin,
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
    _KeywordsEnd,

    Constant,
    Variable,
    FunctionCall
}

public class Token
{
    public TokenKind Kind { get; set; }
    public int Index { get; set; }
    public int ID { get; set; } = 0;
    public string Text { get; set; }
    public ITokenValue Value { get; set; }
    public CodeContext Context { get; set; }

    public Token(CodeContext context, int index)
    {
        Context = context;
        Kind = TokenKind.Error;
        Index = index;
    }

    public Token(CodeContext context, TokenKind kind, int index)
    {
        Context = context;
        Kind = kind;
        Index = index;
    }

    public Token(CodeContext context, TokenKind kind, int index, string text)
    {
        Context = context;
        Kind = kind;
        Index = index;
        Text = text;
    }

    public Token(CodeContext context, TokenConstant value, int index)
    {
        Context = context;
        Kind = TokenKind.Constant;
        Value = value;
        Index = index;
    }

    public Token(CodeContext context, TokenConstant value, int index, string text)
    {
        Context = context;
        Kind = TokenKind.Constant;
        Value = value;
        Index = index;
        Text = text;
    }

    public Token(CodeContext context, TokenVariable value, int index)
    {
        Context = context;
        Kind = TokenKind.Variable;
        Value = value;
        Index = index;
            
        // Initialize variable ID
        if (value.Builtin != null)
            ID = value.Builtin.ID;
        else
        {
            if (context.BaseContext.VariableIds.TryGetValue(value.Name, out int id))
                ID = id;
            else
            {
                ID = 100000 + context.BaseContext.VariableIds.Count;
                context.BaseContext.VariableIds.Add(value.Name, ID);
            }
        }
    }

    public Token(CodeContext context, TokenFunction value, int index)
    {
        Context = context;
        Kind = TokenKind.FunctionCall;
        Value = value;
        Index = index;

        // Initialize function ID
        if (value.Builtin != null)
            ID = value.Builtin.ID;
        // TODO? handle else case here? Not sure if it really matters
    }

    public override string ToString()
    {
        if (Text != null)
            return $"Token: {Kind}, Text: {Text} ({Index})";
        if (Value != null)
            return $"Token: {Kind}, Value: {Value} ({Index}) [{ID}]";
        return $"Token: {Kind} ({Index})";
    }
}

public interface ITokenValue
{
}

public class TokenVariable : ITokenValue
{
    public string Name { get; set; }
    public int InstanceType = -1; // assume self until told otherwise
    public GMCode.Bytecode.Instruction.VariableType VariableType = GMCode.Bytecode.Instruction.VariableType.Normal;
    public bool ExplicitInstType = false; // whether this has been assigned a direct instance type already (and shouldn't change)
    public BuiltinVariable Builtin { get; set; }

    public TokenVariable(string name, BuiltinVariable builtin)
    {
        Name = name;
        Builtin = builtin;
    }

    public override string ToString()
    {
        return $"\"{Name}\"@{InstanceType} {(Builtin != null ? "(builtin)" : "(user)")}";
    }
}

public class TokenFunction : ITokenValue
{
    public string Name { get; set; }
    public BuiltinFunction Builtin { get; set; }
    public GMCode.Bytecode.Instruction.InstanceType ExplicitInstType { get; set; } = GMCode.Bytecode.Instruction.InstanceType.Undefined;

    public TokenFunction(string name, BuiltinFunction builtin)
    {
        Name = name;
        Builtin = builtin;
    }

    public override string ToString()
    {
        return $"\"{Name}\" {(Builtin != null ? "(builtin)" : "(user)")}";
    }
}

public enum ConstantKind
{
    Number,
    String,
    Int64
}

public class TokenConstant : ITokenValue
{
    public ConstantKind Kind { get; set; }
    public string ValueString;
    public double ValueNumber;
    public long ValueInt64;
    public bool IsBool = false;

    public TokenConstant(double valueNumber)
    {
        Kind = ConstantKind.Number;
        ValueNumber = valueNumber;
    }

    public TokenConstant(string valueString)
    {
        Kind = ConstantKind.String;
        ValueString = valueString;
    }

    public TokenConstant(long valueInt64)
    {
        Kind = ConstantKind.Int64;
        ValueInt64 = valueInt64;
    }

    public override string ToString()
    {
        return Kind switch
        {
            ConstantKind.Number => $"{ValueNumber} (number)",
            ConstantKind.String => $"\"{ValueString}\" (string)",
            ConstantKind.Int64 => $"{ValueInt64} (int64)",
            _ => $"{Kind}",
        };
    }
}
