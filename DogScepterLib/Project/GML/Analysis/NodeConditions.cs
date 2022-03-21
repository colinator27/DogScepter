using DogScepterLib.Project.GML.Decompiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Analysis
{
    public class ConditionContext
    {
        public DecompileContext DecompileContext { get; set; }
        public Stack<ASTNode> Parents { get; set; } = new();

        public ConditionContext(DecompileContext dctx)
        {
            DecompileContext = dctx;
        }
    }

    public interface Condition
    {
        public enum ConditionType
        {
            Any,
            All,
            Not,
            FunctionArg,
            Nonzero,
            NonNode,
            Child,
            Parent,
            ValueContains,
        }

        public ConditionType Kind { get; set; }
        public bool EvaluateOnce { get; set; }

        public bool Evaluate(ConditionContext ctx, ASTNode node);
    }

    // Evaluates to true if at least one sub-condition evaluates to true. Evaluates once by default, but can be changed.
    public class ConditionAny : Condition
    {
        public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.Any;
        public bool EvaluateOnce { get; set; } = true;

        public Condition[] Conditions { get; set; }

        public bool Evaluate(ConditionContext ctx, ASTNode node)
        {
            foreach (var cond in Conditions)
                if (cond.Evaluate(ctx, node))
                    return true;
            return false;
        }
    }

    // Evaluates to true if every sub-condition evaluates to true. Evaluates once by default, but can be changed.
    public class ConditionAll : Condition
    {
        public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.All;
        public bool EvaluateOnce { get; set; } = true;

        public Condition[] Conditions { get; set; }

        public bool Evaluate(ConditionContext ctx, ASTNode node)
        {
            foreach (var cond in Conditions)
                if (!cond.Evaluate(ctx, node))
                    return false;
            return true;
        }
    }

    // Evaluates the opposite of result the sub-condition. Evaluates once by default, but can be changed.
    public class ConditionNot : Condition
    {
        public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.Not;
        public bool EvaluateOnce { get; set; } = true;

        public Condition Condition { get; set; }

        public bool Evaluate(ConditionContext ctx, ASTNode node)
        {
            return !Condition.Evaluate(ctx, node);
        }
    }

    // Evaluates to true if this node is the child of a function, which has a specific node argument. Evaluates once.
    public class ConditionFunctionArg : Condition
    {
        public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.FunctionArg;
        public bool EvaluateOnce { get; set; } = true;

        public int Index { get; set; }
        public ASTNode.StatementKind NodeKind { get; set; }
        public string NodeValue { get; set; }

        public bool Evaluate(ConditionContext ctx, ASTNode node)
        {
            if (ctx.Parents.Count == 0 || ctx.Parents.Peek().Kind != ASTNode.StatementKind.Function)
                return false;
            ASTFunction func = ctx.Parents.Peek() as ASTFunction;
            if (Index >= func.Children.Count)
                return false;
            ASTNode arg = func.Children[Index];
            if (arg.Kind != NodeKind)
                return false;
            if (arg.ToString() != NodeValue)
                return false;
            return true;
        }
    }

    // Evaluates to true if the node is not an int16 zero. Evaluates multiple times.
    public class ConditionNonzero : Condition
    {
        public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.Nonzero;
        public bool EvaluateOnce { get; set; } = false;

        public bool Evaluate(ConditionContext ctx, ASTNode node)
        {
            if (node.Kind == ASTNode.StatementKind.Int16)
                if ((node as ASTInt16).Value == 0)
                    return false;
            // todo? other types like int32/64?
            return true;
        }
    }

    // Evaluates to true if the node is not a given node. Evaluates multiple times.
    public class ConditionNonNode : Condition
    {
        public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.NonNode;
        public bool EvaluateOnce { get; set; } = false;

        public ASTNode.StatementKind NodeKind { get; set; }
        public string NodeValue { get; set; }

        public bool Evaluate(ConditionContext ctx, ASTNode node)
        {
            if (node.Kind != NodeKind)
                return true;
            if (node.ToString() != NodeValue)
                return true;
            return false;
        }
    }

    // Evaluates to true if this node has a child (that optionally satisfies a condition, or is at a specific index). Evaluates once.
    public class ConditionChild : Condition
    {
        public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.Child;
        public bool EvaluateOnce { get; set; } = true;

        public int Index { get; set; } = -1;
        public ASTNode.StatementKind NodeKind { get; set; } = ASTNode.StatementKind.None;
        public string NodeValue { get; set; } = null;
        public Condition Condition { get; set; } = null;
        public bool Recursive { get; set; } = false; // Set to true to check all children of the children (and so on), if initial evaluations fail

        public bool Evaluate(ConditionContext ctx, ASTNode node)
        {
            if (Index == -1)
            {
                // No index specified; check this node, then every child
                if (NodeKind == ASTNode.StatementKind.None || node.Kind == NodeKind)
                {
                    if (NodeValue == null || node.ToString() == NodeValue)
                    {
                        if (Condition == null || Condition.Evaluate(ctx, node))
                            return true;
                    }
                }

                if (node.Children == null)
                    return false;
                foreach (ASTNode child in node.Children)
                {
                    if (NodeKind != ASTNode.StatementKind.None && child.Kind != NodeKind)
                    {
                        if (EvaluateChildren(ctx, child))
                            return true;
                        continue;
                    }
                    if (NodeValue != null && child.ToString() != NodeValue)
                    {
                        if (EvaluateChildren(ctx, child))
                            return true;
                        continue;
                    }
                    if (Condition != null && !Condition.Evaluate(ctx, child))
                    {
                        if (EvaluateChildren(ctx, child))
                            return true;
                        continue;
                    }
                    return true;
                }
                return false;
            }
            else
            {
                // Index specified; only check for that child
                if (node.Children == null)
                    return false;
                if (Index >= node.Children.Count)
                    return false;
                ASTNode child = node.Children[Index];
                if (NodeKind != ASTNode.StatementKind.None && child.Kind != NodeKind)
                    return EvaluateChildren(ctx, child);
                if (NodeValue != null && child.ToString() != NodeValue)
                    return EvaluateChildren(ctx, child);
                if (Condition != null && !Condition.Evaluate(ctx, child))
                    return EvaluateChildren(ctx, child);
                return true;
            }
        }

        private bool EvaluateChildren(ConditionContext ctx, ASTNode child)
        {
            if (Recursive && child.Children != null)
            {
                // Evaluate same condition against all children
                // Index is invalid for those, so temporarily change it
                int prevIndex = Index;
                Index = -1;
                foreach (var subChild in child.Children)
                {
                    if (Evaluate(ctx, subChild))
                    {
                        Index = prevIndex;
                        return true;
                    }
                }
                Index = prevIndex;
            }
            return false;
        }
    }

    // Evaluates to true if there is a parent node of a given type (that optionally satisfies a condition). Evaluates once.
    public class ConditionParent : Condition
    {
        public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.Parent;
        public bool EvaluateOnce { get; set; } = true;

        public ASTNode.StatementKind NodeKind { get; set; } = ASTNode.StatementKind.None;
        public string NodeValue { get; set; } = null;
        public Condition Condition { get; set; } = null;
        public bool Immediate { get; set; } = false; // Set to true if this is for the immediate parent only

        public bool Evaluate(ConditionContext ctx, ASTNode node)
        {
            if (Immediate)
            {
                // Only check immediate parent
                if (ctx.Parents.Count == 0)
                    return false;
                ASTNode parent = ctx.Parents.Peek();
                if (NodeKind != ASTNode.StatementKind.None && parent.Kind != NodeKind)
                    return false;
                if (NodeValue != null && parent.ToString() != NodeValue)
                    return false;
                ctx.Parents.Pop();
                if (Condition != null && !Condition.Evaluate(ctx, parent))
                {
                    ctx.Parents.Push(parent);
                    return false;
                }
                ctx.Parents.Push(parent);
                return true;
            }
            else
            {
                // Check all parents (from immediate to outer-most)
                foreach (var parent in ctx.Parents)
                {
                    if (NodeKind != ASTNode.StatementKind.None && parent.Kind != NodeKind)
                        continue;
                    if (NodeValue != null && parent.ToString() != NodeValue)
                        continue;
                    if (Condition != null && !Condition.Evaluate(ctx, parent))
                        continue;
                    return true;
                }
                return false;
            }
        }
    }

    // Evaluates to true if this node's value contains a substring. Evaluates once.
    public class ConditionValueContains : Condition
    {
        public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.ValueContains;
        public bool EvaluateOnce { get; set; } = true;
        
        public string Substring { get; set; }
        public bool CaseSensitive { get; set; } = true;

        public bool Evaluate(ConditionContext ctx, ASTNode node)
        {
            if (node.ToString().Contains(Substring, CaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase))
                return true;
            return false;
        }
    }

    public class ConditionConverter : JsonConverter<Condition>
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert == typeof(Condition);

        public override Condition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Utf8JsonReader readerClone = reader;
            if (readerClone.TokenType != JsonTokenType.StartObject)
                throw new JsonException();
            readerClone.Read();
            if (readerClone.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();
            if (readerClone.GetString() != "Kind")
                throw new JsonException();
            readerClone.Read();
            if (readerClone.TokenType != JsonTokenType.String)
                throw new JsonException();
            if (Enum.TryParse(readerClone.GetString(), out Condition.ConditionType kind))
            {
                return kind switch
                {
                    Condition.ConditionType.Any => JsonSerializer.Deserialize<ConditionAny>(ref reader, options),
                    Condition.ConditionType.All => JsonSerializer.Deserialize<ConditionAll>(ref reader, options),
                    Condition.ConditionType.Not => JsonSerializer.Deserialize<ConditionNot>(ref reader, options),
                    Condition.ConditionType.FunctionArg => JsonSerializer.Deserialize<ConditionFunctionArg>(ref reader, options),
                    Condition.ConditionType.Nonzero => JsonSerializer.Deserialize<ConditionNonzero>(ref reader, options),
                    Condition.ConditionType.NonNode => JsonSerializer.Deserialize<ConditionNonNode>(ref reader, options),
                    Condition.ConditionType.Child => JsonSerializer.Deserialize<ConditionChild>(ref reader, options),
                    Condition.ConditionType.Parent => JsonSerializer.Deserialize<ConditionParent>(ref reader, options),
                    Condition.ConditionType.ValueContains => JsonSerializer.Deserialize<ConditionValueContains>(ref reader, options),
                    _ => throw new JsonException()
                };
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Condition value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
