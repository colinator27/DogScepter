using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Project.GML.Decompiler.AssetResolver;

namespace DogScepterLib.Project.GML.Decompiler
{
    public static class AssetResolverBuiltins
    {
        public static Dictionary<string, AssetType[]> Functions = new Dictionary<string, AssetType[]>()
        {
            { "instance_create", new[] { AssetType.None, AssetType.None, AssetType.Object } },
            { "instance_create_depth", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.Object } },
            { "instance_exists", new[] { AssetType.Object } },
        };
    }
}
