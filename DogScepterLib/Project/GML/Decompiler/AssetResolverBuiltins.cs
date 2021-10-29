using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Project.GML.Decompiler.AssetResolver;

namespace DogScepterLib.Project.GML.Decompiler
{
    public class AssetResolverBuiltins
    {
        public Dictionary<string, AssetType[]> FunctionArgs;
        public Dictionary<string, AssetType> FunctionReturns;
        public Dictionary<string, AssetType> VariableTypes;

        public AssetResolverBuiltins()
        {
            FunctionArgs = new Dictionary<string, AssetType[]>()
            {
                { "instance_create", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_create_depth", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_exists", new[] { AssetType.Object } },
                { "instance_create_layer", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.Object } }, // GMS2
                { "instance_activate_object", new[] { AssetType.Object } },
                { "instance_change", new[] { AssetType.Object, AssetType.Boolean } },
                { "instance_copy", new[] { AssetType.Boolean } },
                { "instance_destroy", new[] { AssetType.Object, AssetType.Boolean } },
                { "instance_find", new[] { AssetType.Object, AssetType.None } },
                { "instance_furthest", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_nearest", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_number", new[] { AssetType.Object } },
                { "instance_place", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_position", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_deactivate_all", new[] { AssetType.Boolean } },
                { "application_surface_enable", new[] { AssetType.Boolean } },
                { "application_surface_draw_enable", new[] { AssetType.Boolean } },
                { "instance_deactivate_object", new[] { AssetType.Object } },
                { "instance_activate_region", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Boolean } },
                { "instance_deactivate_region", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Boolean , AssetType.Boolean } },
            };

            FunctionReturns = new Dictionary<string, AssetType>()
            {
                { "instance_exists", AssetType.Boolean }
            }; 
            
            VariableTypes = new Dictionary<string, AssetType>()
            {
                { "sprite_index", AssetType.Sprite },
                { "room", AssetType.Room },
            };
        }
    }
}
