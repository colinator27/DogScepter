using System;
using System.Collections.Generic;
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
            Room
        }

        public static ASTNode ResolveInt16(DecompileContext ctx, ASTInt16 i16, AssetType type)
        {
            switch (type)
            {
                case AssetType.Boolean:
                    {
                        if (i16.Value == 0)
                            return new ASTBoolean(false);
                        else if (i16.Value == 1)
                            return new ASTBoolean(true);
                    }
                    break;
                case AssetType.Object:
                    return ResolveObject(ctx, i16);
                case AssetType.Sprite:
                    {
                        if (i16.Value >= 0 && i16.Value < ctx.Project.Sprites.Count)
                            return new ASTAsset(ctx.Project.Sprites[i16.Value].Name);
                    }
                    break;
                case AssetType.Room:
                    {
                        if (i16.Value >= 0 && i16.Value < ctx.Project.Rooms.Count)
                            return new ASTAsset(ctx.Project.Rooms[i16.Value].Name);
                    }
                    break;
            }
            return i16;
        }

        public static void ResolveFunction(DecompileContext ctx, ASTFunction func)
        {
            // TODO: Custom list here
            if (ctx.Cache.Builtins.FunctionArgs.TryGetValue(func.Function.Name.Content, out AssetType[] types))
            {
                for (int i = 0; i < func.Children.Count && i < types.Length; i++)
                {
                    if (func.Children[i].Kind == ASTNode.StatementKind.Int16)
                        func.Children[i] = ResolveInt16(ctx, func.Children[i] as ASTInt16, types[i]);
                }
            }
        }

        public static void ResolveBinary(DecompileContext ctx, ASTBinary bin)
        {
            AssetType left = GetExpressionType(ctx, bin.Children[0]);
            AssetType right = GetExpressionType(ctx, bin.Children[1]);
            if (left != AssetType.None && right == AssetType.None)
            {
                if (bin.Children[1].Kind == ASTNode.StatementKind.Int16)
                    bin.Children[1] = ResolveInt16(ctx, bin.Children[1] as ASTInt16, left);
            }
            else if (left == AssetType.None && right != AssetType.None)
            {
                if (bin.Children[0].Kind == ASTNode.StatementKind.Int16)
                    bin.Children[0] = ResolveInt16(ctx, bin.Children[0] as ASTInt16, right);
            }
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
