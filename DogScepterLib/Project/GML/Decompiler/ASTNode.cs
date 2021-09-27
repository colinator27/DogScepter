using DogScepterLib.Core.Models;
using DogScepterLib.Project.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    /// <summary>
    /// This file contains the definitions of all AST nodes for GML output.
    /// It also contains the necessary code to write all of them to a string, recursively.
    /// </summary>

    public interface ASTNode
    {
        public enum StatementKind
        {
            Block,

            Int16,
            Int32,
            Int64,
            Float,
            Double,
            String,
            Boolean,
            Variable,

            TypeInst,

            Binary,
            Unary,

            Function,
            Assign,

            Break,
            Continue,
            Exit,
            Return,

            IfStatement,
            ShortCircuit,
            WhileLoop,
            ForLoop,
            DoUntilLoop,
            RepeatLoop,
            WithLoop,
            SwitchStatement,
            SwitchCase,
            SwitchDefault,
        }

        public StatementKind Kind { get; set; }
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public List<ASTNode> Children { get; set; }
        public Instruction.DataType DataType { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb);
        public ASTNode Clean(DecompileContext ctx);

        public static void Newline(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append('\n');
            sb.Append(ctx.Indentation);
        }

        public static string WriteFromContext(DecompileContext ctx)
        {
            StringBuilder sb = new StringBuilder();

            ASTBlock block = ctx.BaseASTBlock;
            foreach (var child in block.Children)
            {
                child.Write(ctx, sb);
                Newline(ctx, sb);
            }

            return sb.ToString();
        }

        public static int GetStackLength(ASTNode node)
        {
            if (node.DataType != Instruction.DataType.Unset)
                return Instruction.GetDataTypeStackLength(node.DataType);
            switch (node.Kind)
            {
                case StatementKind.Int16:
                case StatementKind.Int32:
                case StatementKind.Boolean:
                case StatementKind.Float:
                    return 4;
                case StatementKind.Int64:
                case StatementKind.Double:
                    return 8;
                case StatementKind.Binary:
                    return Instruction.GetDataTypeStackLength((node as ASTBinary).Instruction.Type2);
            }
            return 16;
        }
    }

    public class ASTBlock : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Block;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append('{');
            ctx.IndentationLevel++;
            foreach (var child in Children)
            {
                ASTNode.Newline(ctx, sb);
                child.Write(ctx, sb);
            }
            ctx.IndentationLevel--;
            ASTNode.Newline(ctx, sb);
            sb.Append('}');
        }

        public static void WhileForConversion(DecompileContext ctx, List<ASTNode> nodes)
        {
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                nodes[i] = nodes[i].Clean(ctx);
                if (i > 0 && nodes[i - 1].Kind == ASTNode.StatementKind.Assign)
                {
                    // Check for while/for loop conversions
                    if (nodes[i].Kind == ASTNode.StatementKind.WhileLoop)
                    {
                        ASTWhileLoop loop = nodes[i] as ASTWhileLoop;
                        if (!loop.ContinueUsed && loop.Children[0].Kind == ASTNode.StatementKind.Block)
                        {
                            ASTBlock block = loop.Children[0] as ASTBlock;
                            if (block.Children.Count >= 1 && block.Children[^1].Kind == ASTNode.StatementKind.Assign)
                            {
                                // This while loop can be cleanly turned into a for loop, so do it!
                                ASTForLoop newLoop = new ASTForLoop();
                                newLoop.HasInitializer = true;
                                newLoop.Children.Add(block.Children[^1]);
                                newLoop.Children.Add(block);
                                block.Children.RemoveAt(block.Children.Count - 1);
                                newLoop.Children.Add(loop.Children[1]);
                                newLoop.Children.Add(nodes[i - 1].Clean(ctx));
                                nodes[i - 1] = newLoop.Clean(ctx);
                                nodes.RemoveAt(i--);
                            }
                        }
                    }
                    else if (nodes[i].Kind == ASTNode.StatementKind.ForLoop)
                    {
                        // This for loop should have the intialization added to it
                        ASTForLoop loop = nodes[i] as ASTForLoop;
                        loop.HasInitializer = true;
                        loop.Children.Add(nodes[i - 1].Clean(ctx));
                        nodes.RemoveAt(--i);
                        loop.Clean(ctx);
                    }
                }
            }
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            if (Children.Count == 1)
            {
                switch (Children[0].Kind)
                {
                    case ASTNode.StatementKind.IfStatement:
                    case ASTNode.StatementKind.SwitchStatement:
                    case ASTNode.StatementKind.WhileLoop:
                    case ASTNode.StatementKind.ForLoop:
                    case ASTNode.StatementKind.DoUntilLoop:
                    case ASTNode.StatementKind.RepeatLoop:
                    case ASTNode.StatementKind.WithLoop:
                        // Don't get rid of curly brackets for these
                        Children[0] = Children[0].Clean(ctx);
                        break;
                    default:
                        Children[0] = Children[0].Clean(ctx);
                        return Children[0];
                }
            }
            else
                WhileForConversion(ctx, Children);
            return this;
        }
    }

    public class ASTBreak : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Break;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("break");
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTContinue : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Continue;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("continue");
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTInt16 : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Int16;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public short Value;
        public Instruction.Opcode Opcode;
        public ASTInt16(short value, Instruction.Opcode opcode)
        {
            Value = value;
            Opcode = opcode;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value);
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTInt32 : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Int32;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public int Value;
        public ASTInt32(int value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value);
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTInt64 : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Int64;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public long Value;
        public ASTInt64(long value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value);
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTFloat : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Float;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public float Value;
        public ASTFloat(float value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value.ToString("R", CultureInfo.InvariantCulture));
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTDouble : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Double;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public double Value;
        public ASTDouble(double value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(RoundTripDouble.ToRoundTrip(Value));
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTString : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.String;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public int Value;
        public ASTString(int value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            string val = ctx.Strings[Value].Content;

            if (ctx.Data.VersionInfo.IsNumberAtLeast(2))
                val = "\"" + val.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"") + "\"";
            else
            {
                // Handle GM:S 1's lack of escaping
                bool front, back;
                if (val.StartsWith('"'))
                {
                    front = true;
                    val = val.Remove(0, 1);
                    if (val.Length == 0)
                        val = "'\"'";
                }
                else
                    front = false;
                if (val.EndsWith('"'))
                {
                    val = val.Remove(val.Length - 1);
                    back = true;
                }
                else
                    back = false;
                val = val.Replace("\"", "\" + '\"' + \"");
                if (front)
                    val = "'\"' + \"" + val;
                else
                    val = "\"" + val;
                if (back)
                    val += "\" + '\"'";
                else
                    val += "\"";
            }
            sb.Append(val);
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTBoolean : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Boolean;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public bool Value;
        public ASTBoolean(bool value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value ? "true" : "false");
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTUnary : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Unary;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public Instruction Instruction;
        public ASTUnary(Instruction inst, ASTNode node)
        {
            Instruction = inst;
            Children = new() { node };
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            if (NeedsParentheses)
                sb.Append('(');
            switch (Instruction.Kind)
            {
                case Instruction.Opcode.Neg:
                    sb.Append('-');
                    break;
                case Instruction.Opcode.Not:
                    if (Instruction.Type1 == Instruction.DataType.Boolean)
                        sb.Append('!');
                    else
                        sb.Append('~');
                    break;
            }
            Children[0].Write(ctx, sb);
            if (NeedsParentheses)
                sb.Append(')');
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            Children[0] = Children[0].Clean(ctx);
            if (Children[0].Kind == ASTNode.StatementKind.Binary || Children[0].Kind == ASTNode.StatementKind.ShortCircuit)
                Children[0].NeedsParentheses = true;
            return this;
        }
    }

    public class ASTBinary : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Binary;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public Instruction Instruction;
        public ASTBinary(Instruction inst, ASTNode left, ASTNode right)
        {
            Instruction = inst;
            Children = new() { left, right };
        }

        public static bool IsTypeTheSame(ASTBinary a, ASTBinary b)
        {
            if (a.Instruction.Kind != b.Instruction.Kind)
                return false;
            if (a.Instruction.ComparisonKind != b.Instruction.ComparisonKind)
                return false;
            return true;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            if (NeedsParentheses)
                sb.Append('(');

            Children[0].Write(ctx, sb);

            string op = null;
            switch (Instruction.Kind)
            {
                case Instruction.Opcode.Mul: op = " * "; break;
                case Instruction.Opcode.Div: op = " / "; break;
                case Instruction.Opcode.Rem: op = " div "; break;
                case Instruction.Opcode.Mod: op = " % "; break;
                case Instruction.Opcode.Add: op = " + "; break;
                case Instruction.Opcode.Sub: op = " - "; break;
                case Instruction.Opcode.And:
                    if (Instruction.Type1 == Instruction.DataType.Boolean &&
                        Instruction.Type2 == Instruction.DataType.Boolean)
                        op = " && "; // Non-short-circuit
                    else
                        op = " & ";
                    break;
                case Instruction.Opcode.Or:
                    if (Instruction.Type1 == Instruction.DataType.Boolean &&
                        Instruction.Type2 == Instruction.DataType.Boolean)
                        op = " || "; // Non-short-circuit
                    else
                        op = " | ";
                    break;
                case Instruction.Opcode.Xor:
                    if (Instruction.Type1 == Instruction.DataType.Boolean &&
                        Instruction.Type2 == Instruction.DataType.Boolean)
                        op = " ^^ ";
                    else
                        op = " ^ ";
                    break;
                case Instruction.Opcode.Shl: op = " << "; break;
                case Instruction.Opcode.Shr: op = " >> "; break;
                case Instruction.Opcode.Cmp:
                    op = Instruction.ComparisonKind switch
                    {
                        Instruction.ComparisonType.LT => " < ",
                        Instruction.ComparisonType.LTE => " <= ",
                        Instruction.ComparisonType.EQ => " == ",
                        Instruction.ComparisonType.NEQ => " != ",
                        Instruction.ComparisonType.GTE => " >= ",
                        Instruction.ComparisonType.GT => " > ",
                        _ => null
                    };
                    break;
            }

            sb.Append(op);

            Children[1].Write(ctx, sb);

            if (NeedsParentheses)
                sb.Append(')');
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i] = Children[i].Clean(ctx);
                if ((Children[i].Kind == ASTNode.StatementKind.Binary && !IsTypeTheSame(this, Children[i] as ASTBinary)) || 
                     Children[i].Kind == ASTNode.StatementKind.ShortCircuit)
                    Children[i].NeedsParentheses = true;
            }
            return this;
        }
    }

    public class ASTFunction : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Function;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public GMFunctionEntry Function;
        public ASTFunction(GMFunctionEntry function, List<ASTNode> args)
        {
            Function = function;
            Children = args;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            bool arrayLiteral = Function.Name.Content == "@@NewGMLArray@@";

            if (arrayLiteral)
                sb.Append('[');
            else
            {
                sb.Append(Function.Name.Content);
                sb.Append('(');
            }

            if (Children.Count >= 1)
                Children[0].Write(ctx, sb);
            for (int i = 1; i < Children.Count; i++)
            {
                sb.Append(", ");
                Children[i].Write(ctx, sb);
            }

            if (arrayLiteral)
                sb.Append(']');
            else
                sb.Append(')');
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
                Children[i] = Children[i].Clean(ctx);
            return this;
        }
    }

    public class ASTExit : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Exit;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("exit");
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTReturn : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Exit;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public ASTReturn(ASTNode arg) => Children = new() { arg };

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("return ");
            Children[0].Write(ctx, sb);
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            Children[0] = Children[0].Clean(ctx);
            return this;
        }
    }

    public class ASTVariable : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Variable;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public GMVariable Variable;
        public ASTNode Left;
        public Instruction.VariableType VarType;
        public Instruction.Opcode Opcode;

        public ASTVariable(GMVariable var, Instruction.VariableType varType, Instruction.Opcode opcode)
        {
            Variable = var;
            VarType = varType;
            Opcode = opcode;
        }

        public bool IsSameAs(ASTVariable other)
        {
            if (Variable != other.Variable)
                return false;
            if (VarType != other.VarType)
                return false;
            if (Left.Kind != other.Left.Kind)
                return false;
            if (Children != null)
            {
                if (Children.Count != other.Children.Count)
                    return false;
                for (int i = 0; i < Children.Count; i++)
                    if (Children[i] != other.Children[i])
                        return false;
            }
            return true;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            if (Left.Kind == ASTNode.StatementKind.Int16 ||
                Left.Kind == ASTNode.StatementKind.TypeInst)
            {
                int value;
                if (Left.Kind == ASTNode.StatementKind.Int16)
                    value = (Left as ASTInt16).Value;
                else
                    value = (Left as ASTTypeInst).Value;

                if (value < 0)
                {
                    // Builtin constants
                    switch (value)
                    {
                        case -5:
                            sb.Append("global.");
                            break;
                        case -2:
                            sb.Append("other.");
                            break;
                        case -3:
                            sb.Append("all.");
                            break;
                        case -16:
                            sb.Append("static.");
                            break;
                    }
                }
                else if (value < ctx.Project.Objects.Count)
                {
                    // Object names
                    sb.Append(ctx.Project.Objects[value].Name);
                    sb.Append('.');
                }
                else
                {
                    // Unknown number
                    sb.Append('(');
                    sb.Append(value);
                    sb.Append(").");
                }
            }
            else if (Left.Kind == ASTNode.StatementKind.Variable)
            {
                // Variable expression
                Left.Write(ctx, sb);
                sb.Append('.');
            }
            else
            {
                // Unknown expression
                sb.Append('(');
                Left.Write(ctx, sb);
                sb.Append(").");
            }

            // The actual variable name
            sb.Append(Variable.Name.Content);

            // Handle arrays
            if (ctx.Data.VersionInfo.IsNumberAtLeast(2, 3))
            {
                // ... for GMS2.3
                if (Children != null)
                {
                    foreach (var c in Children)
                    {
                        sb.Append('[');
                        c.Write(ctx, sb);
                        sb.Append(']');
                    }
                }
            }
            else if (Children != null)
            {
                // ... for pre-GMS2.3
                sb.Append('[');
                Children[0].Write(ctx, sb);
                if (Children.Count == 2)
                {
                    sb.Append(", ");
                    Children[1].Write(ctx, sb);
                }
                sb.Append(']');
            }
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children?.Count; i++)
                Children[i] = Children[i].Clean(ctx);
            return this;
        }
    }

    public class ASTTypeInst : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.TypeInst;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public int Value;

        public ASTTypeInst(int value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            // Doesn't really do anything on its own here
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }

    public class ASTAssign : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Assign;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }
        public Instruction Compound;
        public CompoundType CompoundKind = CompoundType.None;
        public enum CompoundType
        {
            None,
            Normal,
            Prefix,
            Postfix
        }    

        public ASTAssign(ASTVariable var, ASTNode node, Instruction compound = null)
        {
            Children = new() { var, node };
            Compound = compound;
            CompoundKind = (compound == null) ? CompoundType.None : CompoundType.Normal;
        }
        public ASTAssign(Instruction inst, ASTNode variable, bool isPrefix)
        {
            Compound = inst;
            Children = new() { variable };
            CompoundKind = isPrefix ? CompoundType.Prefix : CompoundType.Postfix;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            switch (CompoundKind)
            {
                case CompoundType.Normal:
                    Children[0].Write(ctx, sb);
                    sb.Append(' ');
                    switch (Compound.Kind)
                    {
                        case Instruction.Opcode.Add: sb.Append('+'); break;
                        case Instruction.Opcode.Sub: sb.Append('-'); break;
                        case Instruction.Opcode.Mul: sb.Append('*'); break;
                        case Instruction.Opcode.Div: sb.Append('/'); break;
                        case Instruction.Opcode.Mod: sb.Append('%'); break;
                        case Instruction.Opcode.And: sb.Append('&'); break;
                        case Instruction.Opcode.Or: sb.Append('|'); break;
                        case Instruction.Opcode.Xor: sb.Append('^'); break;
                    }
                    sb.Append("= ");
                    Children[1].Write(ctx, sb);
                    break;
                case CompoundType.Prefix:
                    if (Compound.Kind == Instruction.Opcode.Add)
                        sb.Append("++");
                    else
                        sb.Append("--");
                    Children[0].Write(ctx, sb);
                    break;
                case CompoundType.Postfix:
                    Children[0].Write(ctx, sb);
                    if (Compound.Kind == Instruction.Opcode.Add)
                        sb.Append("++");
                    else
                        sb.Append("--");
                    break;
                default:
                    Children[0].Write(ctx, sb);
                    sb.Append(" = ");
                    Children[1].Write(ctx, sb);
                    break;
            }
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
                Children[i] = Children[i].Clean(ctx);

            // Check for postfix and compounds
            if (CompoundKind == CompoundType.None &&
                Children[0].Kind == ASTNode.StatementKind.Variable && Children[1].Kind == ASTNode.StatementKind.Binary)
            {
                ASTBinary bin = Children[1] as ASTBinary;
                if (bin.Children[0].Kind == ASTNode.StatementKind.Variable)
                {
                    ASTVariable var = bin.Children[0] as ASTVariable;
                    if (var.IsSameAs(Children[0] as ASTVariable))
                    {
                        // This is one of the two
                        if (bin.Children[1].Kind == ASTNode.StatementKind.Int16)
                        {
                            ASTInt16 i16 = bin.Children[1] as ASTInt16;
                            if (i16.Opcode == Instruction.Opcode.Push && i16.Value == 1)
                            {
                                CompoundKind = CompoundType.Postfix;
                                Compound = bin.Instruction;
                                return this;
                            }
                        }

                        if (ctx.Data.VersionInfo.FormatID >= 15 && var.Opcode != Instruction.Opcode.Push && var.Variable.VariableType != Instruction.InstanceType.Self)
                            return this; // Actually, this is a false positive (uses a different instruction in bytecode)

                        CompoundKind = CompoundType.Normal;
                        Compound = bin.Instruction;
                        Children[1] = bin.Children[1];
                    }
                }
            }

            return this;
        }
    }

    public class ASTIfStatement : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.IfStatement;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; } = new List<ASTNode>(3);
        public bool ElseIf { get; set; } = false;

        // Temporary ternary detection variables
        public int StackCount { get; set; }
        public ASTNode Parent { get; set; }

        public ASTIfStatement(ASTNode condition) => Children.Add(condition);

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
#if DEBUG
            if (Children.Count == 4)
                throw new Exception("Ternary logic broke");
#endif
            if (Children.Count == 5)
            {
                // This is a ternary
                if (NeedsParentheses)
                    sb.Append('(');
                Children[0].Write(ctx, sb);
                sb.Append(" ? ");
                Children[3].Write(ctx, sb);
                sb.Append(" : ");
                Children[4].Write(ctx, sb);
                if (NeedsParentheses)
                    sb.Append(')');
                return;
            }

            sb.Append("if (");
            Children[0].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[1].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
            }

            // Else block
            if (Children.Count >= 3)
            {
                ASTNode.Newline(ctx, sb);
                sb.Append("else");

                if (ElseIf)
                {
                    sb.Append(' ');
                    Children[2].Write(ctx, sb);
                }
                else if (Children[2].Kind != ASTNode.StatementKind.Block)
                {
                    ctx.IndentationLevel++;
                    ASTNode.Newline(ctx, sb);
                    Children[2].Write(ctx, sb);
                    ctx.IndentationLevel--;
                }
                else
                {
                    ASTNode.Newline(ctx, sb);
                    Children[2].Write(ctx, sb);
                }
            }
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
                Children[i] = Children[i].Clean(ctx);

            if (Children.Count == 3 && Children[2].Kind == ASTNode.StatementKind.Block &&
                Children[2].Children.Count == 1 && Children[2].Children[0].Kind == ASTNode.StatementKind.IfStatement)
            {
                // This is an else if chain, so mark this as such
                ElseIf = true;
                Children[2] = Children[2].Children[0];
            }
            else if (Children.Count == 5)
            {
                // This is a ternary. Check if parentheses are needed for operands.
                var kind = Children[0].Kind;
                if (kind == ASTNode.StatementKind.Binary || kind == ASTNode.StatementKind.ShortCircuit || kind == ASTNode.StatementKind.IfStatement)
                    Children[0].NeedsParentheses = true;
                kind = Children[3].Kind;
                if (kind == ASTNode.StatementKind.Binary || kind == ASTNode.StatementKind.ShortCircuit || kind == ASTNode.StatementKind.IfStatement)
                    Children[3].NeedsParentheses = true;
                kind = Children[4].Kind;
                if (kind == ASTNode.StatementKind.Binary || kind == ASTNode.StatementKind.ShortCircuit || kind == ASTNode.StatementKind.IfStatement)
                    Children[4].NeedsParentheses = true;
            }

            return this;
        }
    }

    public class ASTShortCircuit : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.ShortCircuit;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public ShortCircuit.ShortCircuitType ShortCircuitKind;
        public ASTShortCircuit(ShortCircuit.ShortCircuitType kind, List<ASTNode> conditions)
        {
            ShortCircuitKind = kind;
            Children = conditions;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            if (NeedsParentheses)
                sb.Append('(');

            Children[0].Write(ctx, sb);
            string op = (ShortCircuitKind == ShortCircuit.ShortCircuitType.And) ? " && " : " || ";
            for (int i = 1; i < Children.Count; i++)
            {
                sb.Append(op);
                Children[i].Write(ctx, sb);
            }

            if (NeedsParentheses)
                sb.Append(')');
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i] = Children[i].Clean(ctx);
                if (Children[i].Kind == ASTNode.StatementKind.ShortCircuit)
                    Children[i].NeedsParentheses = true;
            }
            return this;
        }
    }

    public class ASTWhileLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.WhileLoop;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public bool ContinueUsed { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("while (");
            Children[1].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[0].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
            }
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
                Children[i] = Children[i].Clean(ctx);
            return this;
        }
    }

    public class ASTForLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.ForLoop;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public bool HasInitializer { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("for (");
            if (HasInitializer)
                Children[3].Write(ctx, sb);
            sb.Append("; ");
            Children[2].Write(ctx, sb);
            sb.Append("; ");
            Children[0].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[1].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
            }
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
                Children[i] = Children[i].Clean(ctx);
            return this;
        }
    }

    public class ASTDoUntilLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.DoUntilLoop;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("do");

            // Main block
            if (Children[0].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
            }

            ASTNode.Newline(ctx, sb);
            sb.Append("until (");
            Children[1].Write(ctx, sb);
            sb.Append(')');
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
                Children[i] = Children[i].Clean(ctx);
            return this;
        }
    }

    public class ASTRepeatLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.RepeatLoop;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();

        public ASTRepeatLoop(ASTNode expr) => Children.Add(expr);
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("repeat (");
            Children[0].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[1].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
            }
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
                Children[i] = Children[i].Clean(ctx);
            return this;
        }
    }

    public class ASTWithLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.WithLoop;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();

        public ASTWithLoop(ASTNode expr) => Children.Add(expr);
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("with (");
            Children[0].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[1].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
            }
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            for (int i = 0; i < Children.Count; i++)
                Children[i] = Children[i].Clean(ctx);
            return this;
        }
    }

    public class ASTSwitchStatement : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.SwitchStatement;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("switch (");
            Children[0].Write(ctx, sb);
            sb.Append(')');

            ASTNode.Newline(ctx, sb);
            sb.Append('{');
            ctx.IndentationLevel += 2;
            for (int i = 1; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child.Kind == ASTNode.StatementKind.SwitchCase ||
                    child.Kind == ASTNode.StatementKind.SwitchDefault)
                {
                    ctx.IndentationLevel--;
                    ASTNode.Newline(ctx, sb);
                    child.Write(ctx, sb);
                    ctx.IndentationLevel++;
                }
                else
                {
                    ASTNode.Newline(ctx, sb);
                    child.Write(ctx, sb);
                }
            }
            ctx.IndentationLevel -= 2;
            ASTNode.Newline(ctx, sb);
            sb.Append('}');
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            ASTBlock.WhileForConversion(ctx, Children);
            return this;
        }
    }

    public class ASTSwitchCase : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.SwitchCase;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }

        public ASTSwitchCase(ASTNode expr) => Children = new() { expr };

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("case ");
            Children[0].Write(ctx, sb);
            sb.Append(':');
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            Children[0] = Children[0].Clean(ctx);
            return this;
        }
    }

    public class ASTSwitchDefault : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.SwitchDefault;
        public bool Duplicated { get; set; }
        public bool NeedsParentheses { get; set; }
        public Instruction.DataType DataType { get; set; } = Instruction.DataType.Unset;
        public List<ASTNode> Children { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("default:");
        }

        public ASTNode Clean(DecompileContext ctx)
        {
            return this;
        }
    }
}
