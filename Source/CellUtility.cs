using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace TemperaturesPlus
{
    static class CellUtility
    {
        public static IEnumerable<IntVec3> AdjacentCells(this IntVec3 cell)
        {
            yield return cell + IntVec3.North;
            yield return cell + IntVec3.South;
            yield return cell + IntVec3.West;
            yield return cell + IntVec3.East;
        }

        static TerrainDef WaterTerrainAtCell(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map))
                return null;
            TerrainDef def = cell.GetTerrain(map);
            if (def.IsWater)
                return def;
            if ((def = map.terrainGrid.UnderTerrainAt(cell)).IsWater)
                return def;
            return null;
        }

        /// <summary>
        /// Returns best guess for what kind of water terrain should be placed in a cell (if Ice melts there)
        /// </summary>
        public static TerrainDef BestWaterTerrain(this IntVec3 cell, Map map)
        {
            TerrainDef def = WaterTerrainAtCell(cell, map);
            if (def != null)
                return def;
            def = cell.AdjacentCells().Select(c => WaterTerrainAtCell(c, map)).FirstOrDefault(c => c != null);
            if (def != null)
                return def;
            if (cell.AdjacentCells().Any(c => c.InBounds(map) && !c.GetTerrain(map).IsWater && c.GetTerrain(map) != TerrainDefOf.Ice))
                return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanShallow : TerrainDefOf.WaterShallow;
            return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanDeep : TerrainDefOf.WaterDeep;
        }
    }
}
