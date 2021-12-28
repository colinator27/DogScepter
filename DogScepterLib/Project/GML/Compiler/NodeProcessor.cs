using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Compiler
{
    public static class NodeProcessor
    {
        public static Node ProcessNode(CompileContext ctx, Node n)
        {
            for (int i = 0; i < n.Children.Count; i++)
            {
                n.Children[i] = ProcessNode(ctx, n.Children[i]);
            }

            switch (n.Kind)
            {
                case NodeKind.ChainReference:
                    if (ctx.ResolveEnums && n.Children.Count == 2 && n.Children[0].Kind == NodeKind.Variable && n.Children[1].Kind == NodeKind.Variable)
                    {
                        // Try to resolve enum
                        string enumName = (n.Children[0].Token.Value as TokenVariable).Name;
                        if (ctx.Enums.TryGetValue(enumName, out Enum baseEnum))
                        {
                            string valName = (n.Children[1].Token.Value as TokenVariable).Name;
                            if (baseEnum.TryGetValue(valName, out EnumValue enumVal))
                            {
                                if (ctx.ReferencedEnums.Count != 0)
                                {
                                    // Currently resolving interdependent enums
                                    if (ctx.ReferencedEnums.Add(enumName))
                                    {
                                        if (enumVal.HasValue)
                                        {
                                            return new Node(NodeKind.Constant, new Token(n.Children[0].Token.Context, new TokenConstant(enumVal.Value), -1));
                                        }
                                        else
                                            n.Children[0].Token.Context.Error($"Too much enum reference complexity", n.Children[0].Token);
                                    }
                                    else
                                        n.Children[0].Token.Context.Error($"Cross-referenced enums not supported", n.Children[0].Token);
                                }
                                else
                                {
                                    // Not resolving interdependent enums, but can resolve some now
                                    if (enumVal.HasValue)
                                    {
                                        return new Node(NodeKind.Constant, new Token(n.Children[0].Token.Context, new TokenConstant(enumVal.Value), -1));
                                    }
                                }
                            }
                            else
                                n.Children[1].Token.Context.Error($"Nonexistent enum value '{valName}' in enum '{enumName}'", n.Children[1].Token);
                        }
                    }
                    break;
            }

            return n;
        }    
    }
}
