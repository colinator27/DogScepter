using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DogScepterLib.Project.GML.Decompiler.AssetResolverTypes;

namespace DogScepterLib.Project.GML.Decompiler
{
    public static class AssetResolver
    {
        public enum AssetType
        {
            None,
            Boolean,
            Object,
            Sprite,
            Room,
            Font,
            Sound,
            Path,

            Color,
            Keyboard,

            Macro_PathEndAction,
        }

        public static ASTNode ResolveAny(DecompileContext ctx, ASTNode node, ASTNode parent, ConditionalAssetType type)
        {
            // Check if this type has a condition that needs to be satisfied
            if (type.Condition != null)
            {
                if (type.Condition.Evaluate(ctx, node, parent))
                {
                    if (type.Condition.EvaluateOnce)
                        type = new(type); // Make a new type without the condition (it has been satisfied)
                }
                else
                {
                    // Check if there's any valid alternative type
                    if (type.Alternatives != null)
                    {
                        bool evaluated = false;
                        for (int i = 0; i < type.Alternatives.Length; i++)
                        {
                            var curr = type.Alternatives[i];
                            if (curr.Condition == null || curr.Condition.Evaluate(ctx, node, parent))
                            {
                                evaluated = true;
                                if (curr.Condition != null && curr.Condition.EvaluateOnce)
                                    type = new(curr); // Make a new type without the condition (it has been satisfied)
                                else
                                    type = curr;
                                break;
                            }
                        }

                        if (!evaluated)
                            return node;
                    }
                    else
                        return node;
                }
            }

            switch (node.Kind)
            {
                case ASTNode.StatementKind.Int16:
                case ASTNode.StatementKind.Int32:
                    return ResolveInt(ctx, node, type);
                case ASTNode.StatementKind.IfStatement:
                    if (node.Children.Count == 5)
                    {
                        node.Children[3] = ResolveAny(ctx, node.Children[3], node, type);
                        node.Children[4] = ResolveAny(ctx, node.Children[4], node, type);
                    }
                    break;
                case ASTNode.StatementKind.Binary:
                    node.Children[0] = ResolveAny(ctx, node.Children[0], node, type);
                    node.Children[1] = ResolveAny(ctx, node.Children[1], node, type);
                    break;
                case ASTNode.StatementKind.Function:
                    if ((node as ASTFunction).Function.Name.Content == "@@NewGMLArray@@")
                    {
                        for (int i = 0; i < node.Children.Count; i++)
                            node.Children[i] = ResolveAny(ctx, node.Children[i], node, type);
                    }
                    break;
            }

            return node;
        }

        public static ASTNode ResolveInt(DecompileContext ctx, ASTNode intNode, ConditionalAssetType type)
        {
            int value = (intNode.Kind == ASTNode.StatementKind.Int16) ? (intNode as ASTInt16).Value : (intNode as ASTInt32).Value;
            switch (type.Kind)
            {
                case AssetType.Boolean:
                    if (value == 0)
                        return new ASTBoolean(false);
                    else if (value == 1)
                        return new ASTBoolean(true);
                    break;
                case AssetType.Object:
                    if (intNode.Kind == ASTNode.StatementKind.Int16)
                        return ResolveObject(ctx, intNode as ASTInt16);
                    break;
                case AssetType.Sprite:
                    if (value >= 0 && value < ctx.Project.Sprites.Count)
                        return new ASTAsset(ctx.Project.Sprites[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case AssetType.Room:
                    if (value >= 0 && value < ctx.Project.Rooms.Count)
                        return new ASTAsset(ctx.Project.Rooms[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case AssetType.Font:
                    if (value >= 0 && value < ctx.Project.Fonts.Count)
                        return new ASTAsset(ctx.Project.Fonts[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case AssetType.Sound:
                    if (value >= 0 && value < ctx.Project.Sounds.Count)
                        return new ASTAsset(ctx.Project.Sounds[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case AssetType.Path:
                    if (value >= 0 && value < ctx.Project.Paths.Count)
                        return new ASTAsset(ctx.Project.Paths[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case AssetType.Color:
                    if (value >= 0)
                    {
                        if (ctx.Cache.Types.ColorMacros.TryGetValue(value, out string color))
                            return new ASTAsset(color);
                        return new ASTAsset((ctx.Data.VersionInfo.IsNumberAtLeast(2) ? "0x" : "$") + value.ToString("X6", CultureInfo.InvariantCulture));
                    }
                    break;
                case AssetType.Keyboard:
                    if (value >= 0)
                    {
                        if (ctx.Cache.Types.KeyboardMacros.TryGetValue(value, out string keyboard))
                            return new ASTAsset(keyboard);
                        if (value >= '0' && value <= 'Z')
                            return new ASTAsset("ord(\"" + (char)value + "\")");
                    }
                    break;
                case AssetType.Macro_PathEndAction:
                    {
                        if (ctx.Cache.Types.PathEndActionMacros.TryGetValue(value, out string macro))
                            return new ASTAsset(macro);
                    }
                    break;
            }
            return intNode;
        }

        public static void ResolveFunction(DecompileContext ctx, ASTFunction func)
        {
            // Handle code-entry-specific types
            if (ctx.CodeAssetTypes.HasValue)
            {
                if (ctx.CodeAssetTypes.Value.FunctionArgs != null &&
                    ctx.CodeAssetTypes.Value.FunctionArgs.TryGetValue(func.Function.Name.Content, out AssetType[] types))
                {
                    for (int i = 0; i < func.Children.Count && i < types.Length; i++)
                        func.Children[i] = ResolveAny(ctx, func.Children[i], func, new(types[i]));
                    return;
                }

                if (ctx.CodeAssetTypes.Value.FunctionArgsCond != null &&
                         ctx.CodeAssetTypes.Value.FunctionArgsCond.TryGetValue(func.Function.Name.Content, out var cond))
                {
                    for (int i = 0; i < func.Children.Count && i < cond.Length; i++)
                        func.Children[i] = ResolveAny(ctx, func.Children[i], func, cond[i]);
                    return;
                }
            }

            // Handle global types
            {
                if (ctx.Cache.Types.FunctionArgs.TryGetValue(func.Function.Name.Content, out AssetType[] types))
                {
                    for (int i = 0; i < func.Children.Count && i < types.Length; i++)
                        func.Children[i] = ResolveAny(ctx, func.Children[i], func, new(types[i]));
                    return;
                }

                if (ctx.Cache.Types.FunctionArgsCond.TryGetValue(func.Function.Name.Content, out var cond))
                {
                    for (int i = 0; i < func.Children.Count && i < cond.Length; i++)
                        func.Children[i] = ResolveAny(ctx, func.Children[i], func, cond[i]);
                    return;
                }
            }
        }

        public static void ResolveAssign(DecompileContext ctx, ASTAssign assign)
        {
            ConditionalAssetType left = GetExpressionType(ctx, assign.Children[0]);
            if (left.Kind != AssetType.None)
                assign.Children[1] = ResolveAny(ctx, assign.Children[1], assign, left);
        }

        public static void ResolveBinary(DecompileContext ctx, ASTBinary bin)
        {
            ConditionalAssetType left = GetExpressionType(ctx, bin.Children[0]);
            ConditionalAssetType right = GetExpressionType(ctx, bin.Children[1]);
            if (left.Kind != AssetType.None && right.Kind == AssetType.None)
                bin.Children[1] = ResolveAny(ctx, bin.Children[1], bin, left);
            else if (left.Kind == AssetType.None && right.Kind != AssetType.None)
                bin.Children[0] = ResolveAny(ctx, bin.Children[0], bin, right);
        }

        public static ConditionalAssetType GetExpressionType(DecompileContext ctx, ASTNode node)
        {
            switch (node.Kind)
            {
                case ASTNode.StatementKind.Variable:
                    {
                        ASTVariable variable = node as ASTVariable;

                        // Handle code-entry-specific types
                        if (ctx.CodeAssetTypes.HasValue)
                        {
                            if (ctx.CodeAssetTypes.Value.VariableTypes != null &&
                                ctx.CodeAssetTypes.Value.VariableTypes.TryGetValue(variable.Variable.Name.Content, out AssetType type))
                                return new(type);
                            if (ctx.CodeAssetTypes.Value.VariableTypesCond != null &&
                                ctx.CodeAssetTypes.Value.VariableTypesCond.TryGetValue(variable.Variable.Name.Content, out var cond))
                                return cond;
                        }

                        // Handle global types
                        {
                            if (ctx.Cache.Types.VariableTypes.TryGetValue(variable.Variable.Name.Content, out AssetType type))
                                return new(type);
                            if (ctx.Cache.Types.VariableTypesCond.TryGetValue(variable.Variable.Name.Content, out var cond))
                                return cond;
                        }

                        if (variable.Variable.VariableID == -6)
                        {
                            // This is a builtin variable; check for builtin list
                            if (ctx.Cache.Types.VariableTypesBuiltin.TryGetValue(variable.Variable.Name.Content, out AssetType type))
                                return new(type);
                        }
                    }
                    break;
                case ASTNode.StatementKind.Function:
                    {
                        ASTFunction func = node as ASTFunction;

                        // Handle code-entry-specific types
                        if (ctx.CodeAssetTypes.HasValue)
                        {
                            if (ctx.CodeAssetTypes.Value.FunctionReturns != null &&
                                ctx.CodeAssetTypes.Value.FunctionReturns.TryGetValue(func.Function.Name.Content, out AssetType type))
                                return new(type);
                        }

                        // Handle global types
                        {
                            if (ctx.Cache.Types.FunctionReturns.TryGetValue(func.Function.Name.Content, out AssetType type))
                                return new(type);
                        }
                    }
                    break;
            }

            return new(AssetType.None);
        }

        public static ASTNode ResolveObject(DecompileContext ctx, ASTInt16 int16)
        {
            if (int16.Value < 0)
            {
                return int16.Value switch
                {
                    -1 => new ASTAsset("self"),
                    -2 => new ASTAsset("other"),
                    -3 => new ASTAsset("all"),
                    -4 => new ASTAsset("noone"),
                    _ => int16,
                };
            }

            if (int16.Value < ctx.Project.Objects.Count)
                return new ASTAsset(ctx.Project.Objects[int16.Value].Name);

            return int16;
        }
    }
}
