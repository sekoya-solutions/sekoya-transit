#nullable enable
using System.IO;
using System.Text;
using TransportTycoon.Core.World;

namespace TransportTycoon.Core.Tests.World
{
    internal static class WorldGenerationSnapshot
    {
        private const byte _schemaVersion = 1;

        public static byte[] Create(GeneratedWorld world)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(_schemaVersion);
                writer.Write((int)world.Map.Size);
                writer.Write(world.Map.Width);
                writer.Write(world.Map.Height);
                writer.Write(world.Map.TileCount);
                foreach (int value in world.Map.TerrainTypeIds)
                {
                    writer.Write(value);
                }

                foreach (int value in world.Map.ObjectIds)
                {
                    writer.Write(value);
                }

                foreach (int value in world.Map.OwnerIds)
                {
                    writer.Write(value);
                }

                writer.Write(world.TownAnchors.Count);
                foreach (TileCoordinate townAnchor in world.TownAnchors)
                {
                    writer.Write(townAnchor.X);
                    writer.Write(townAnchor.Y);
                }

                writer.Write(world.Industries.Count);
                foreach (PlacedIndustry industry in world.Industries)
                {
                    byte[] industryId = Encoding.UTF8.GetBytes(industry.IndustryId);
                    writer.Write(industryId.Length);
                    writer.Write(industryId);
                    writer.Write(industry.InstanceIndex);
                    writer.Write(industry.Coordinate.X);
                    writer.Write(industry.Coordinate.Y);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
