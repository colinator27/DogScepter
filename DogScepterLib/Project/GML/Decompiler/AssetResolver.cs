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
            Object
        }

        public static void ResolveFunction(DecompileContext ctx, ASTFunction func)
        {
            if (AssetResolverBuiltins.Functions.TryGetValue(func.Function.Name.Content, out AssetType[] types))
            {
                for (int i = 0; i < func.Children.Count && i < types.Length; i++)
                {
                    switch (types[i])
                    {
                        case AssetType.Object:
                            if (func.Children[i].Kind == ASTNode.StatementKind.Int16)
                                func.Children[i] = ResolveObject(ctx, func.Children[i] as ASTInt16);
                            break;
                    }
                }
            }
        }

        public static ASTNode ResolveObject(DecompileContext ctx, ASTInt16 int16)
        {
            if (int16.Value < 0)
            {
                switch (int16.Value)
                {
                    case -1:
                        return new ASTAsset("self");
                    case -2:
                        return new ASTAsset("other");
                    case -3:
                        return new ASTAsset("all");
                    case -4:
                        return new ASTAsset("noone");
                }
                return int16;
            }

            if (int16.Value < ctx.Project.Objects.Count)
                return new ASTAsset(ctx.Project.Objects[int16.Value].Name);

            return int16;
        }
    }
}
