using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Converters
{
    public class PathConverter : AssetConverter<AssetPath>
    {
        public override void ConvertData(ProjectFile pf, int index)
        {
            GMPath asset = (GMPath)pf.Paths[index].DataAsset;

            AssetPath projectAsset = new AssetPath()
            {
                Name = asset.Name?.Content,
                Smooth = asset.Smooth,
                Closed = asset.Closed,
                Precision = asset.Precision,
                Points = new List<AssetPath.Point>()
            };
            foreach (GMPath.Point point in asset.Points)
                projectAsset.Points.Add(new AssetPath.Point() { X = point.X, Y = point.Y, Speed = point.Speed });

            pf.Paths[index].Asset = projectAsset;
        }

        public override void ConvertData(ProjectFile pf)
        {
            EmptyRefsForNamed(pf.DataHandle.GetChunk<GMChunkPATH>().List, pf.Paths);
        }

        public override void ConvertProject(ProjectFile pf)
        {
            GMList<GMPath> dataAssets = pf.DataHandle.GetChunk<GMChunkPATH>().List;

            dataAssets.Clear();
            for (int i = 0; i < pf.Paths.Count; i++)
            {
                AssetPath assetPath = pf.Paths[i].Asset;
                if (assetPath == null)
                {
                    // This asset was never converted
                    dataAssets.Add((GMPath)pf.Paths[i].DataAsset);
                    continue;
                }

                dataAssets.Add(new GMPath()
                {
                    Name = pf.DataHandle.DefineString(assetPath.Name),
                    Smooth = assetPath.Smooth,
                    Closed = assetPath.Closed,
                    Precision = assetPath.Precision,
                    Points = new GMList<GMPath.Point>()
                });

                GMPath gmPath = dataAssets[dataAssets.Count - 1];
                foreach (AssetPath.Point point in assetPath.Points)
                    gmPath.Points.Add(new GMPath.Point() { X = point.X, Y = point.Y, Speed = point.Speed });
            }
        }
    }
}
