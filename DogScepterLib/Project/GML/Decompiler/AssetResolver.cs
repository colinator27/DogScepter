using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            Color,
            Keyboard
        }

        public static ASTNode ResolveAny(DecompileContext ctx, ASTNode node, AssetType type)
        {
            switch (node.Kind)
            {
                case ASTNode.StatementKind.Int16:
                case ASTNode.StatementKind.Int32:
                    return ResolveInt(ctx, node, type);
                case ASTNode.StatementKind.IfStatement:
                    if (node.Children.Count == 5)
                    {
                        node.Children[3] = ResolveAny(ctx, node.Children[3], type);
                        node.Children[4] = ResolveAny(ctx, node.Children[4], type);
                    }
                    break;
                case ASTNode.StatementKind.Binary:
                    node.Children[0] = ResolveAny(ctx, node.Children[0], type);
                    node.Children[1] = ResolveAny(ctx, node.Children[1], type);
                    break;
                case ASTNode.StatementKind.Function:
                    if ((node as ASTFunction).Function.Name.Content == "@@NewGMLArray@@")
                    {
                        for (int i = 0; i < node.Children.Count; i++)
                            node.Children[i] = ResolveAny(ctx, node.Children[i], type);
                    }
                    break;
            }

            return node;
        }

        public static ASTNode ResolveInt(DecompileContext ctx, ASTNode intNode, AssetType type)
        {
            int value = (intNode.Kind == ASTNode.StatementKind.Int16) ? (intNode as ASTInt16).Value : (intNode as ASTInt32).Value;
            switch (type)
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
                    break;
                case AssetType.Room:
                    if (value >= 0 && value < ctx.Project.Rooms.Count)
                        return new ASTAsset(ctx.Project.Rooms[value].Name);
                    break;
                case AssetType.Font:
                    if (value >= 0 && value < ctx.Project.Fonts.Count)
                        return new ASTAsset(ctx.Project.Fonts[value].Name);
                    break;
                case AssetType.Sound:
                    if (value >= 0 && value < ctx.Project.Sounds.Count)
                        return new ASTAsset(ctx.Project.Sounds[value].Name);
                    break;
                case AssetType.Color:
                    if (value >= 0)
                    {
                        if (ctx.Cache.Builtins.ColorMacros.TryGetValue(value, out string color))
                            return new ASTAsset(color);
                        return new ASTAsset((ctx.Data.VersionInfo.IsNumberAtLeast(2) ? "0x" : "$") + value.ToString("X6", CultureInfo.InvariantCulture));
                    }
                    break;
                case AssetType.Keyboard:
                    if (value >= 0)
                    {
                        if (ctx.Cache.Builtins.KeyboardMacros.TryGetValue(value, out string keyboard))
                            return new ASTAsset(keyboard);
                        if (value >= '0' && value <= 'Z')
                            return new ASTAsset("ord(\"" + (char)value + "\")");
                    }
                    break;
            }
            return intNode;
        }

        public static void ResolveFunction(DecompileContext ctx, ASTFunction func)
        {
            // TODO: Custom list here
            if (ctx.Cache.Builtins.FunctionArgs.TryGetValue(func.Function.Name.Content, out AssetType[] types))
            {
                for (int i = 0; i < func.Children.Count && i < types.Length; i++)
                    func.Children[i] = ResolveAny(ctx, func.Children[i], types[i]);
            }
        }

        public static void ResolveAssign(DecompileContext ctx, ASTAssign assign)
        {
            AssetType left = GetExpressionType(ctx, assign.Children[0]);
            if (left != AssetType.None)
                assign.Children[1] = ResolveAny(ctx, assign.Children[1], left);
        }

        public static void ResolveBinary(DecompileContext ctx, ASTBinary bin)
        {
            AssetType left = GetExpressionType(ctx, bin.Children[0]);
            AssetType right = GetExpressionType(ctx, bin.Children[1]);
            if (left != AssetType.None && right == AssetType.None)
                bin.Children[1] = ResolveAny(ctx, bin.Children[1], left);
            else if (left == AssetType.None && right != AssetType.None)
                bin.Children[0] = ResolveAny(ctx, bin.Children[0], right);
        }

        public static AssetType GetExpressionType(DecompileContext ctx, ASTNode node)
        {
            switch (node.Kind)
            {
                case ASTNode.StatementKind.Variable:
                    {
                        ASTVariable variable = node as ASTVariable;
                        if (variable.Variable.VariableID == -6)
                        {
                            // This is a builtin variable; check for builtin list
                            if (ctx.Cache.Builtins.VariableTypes.TryGetValue(variable.Variable.Name.Content, out AssetType type))
                                return type;
                        }
                        // TODO: Custom list here
                    }
                    break;
                case ASTNode.StatementKind.Function:
                    {
                        ASTFunction func = node as ASTFunction;
                        if (ctx.Cache.Builtins.FunctionReturns.TryGetValue(func.Function.Name.Content, out AssetType type))
                            return type;
                        // TODO: Custom list here
                    }
                    break;
            }

            return AssetType.None;
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
