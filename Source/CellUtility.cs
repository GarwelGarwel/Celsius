using RimWorld;
using System.Collections.Generic;
using Verse;

namespace Celsius
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

        /// <summary>
        /// Returns best guess for what kind of water terrain should be placed in a cell (if Ice melts there)
        /// </summary>
        public static TerrainDef BestUnderIceTerrain(this IntVec3 cell, Map map)
        {
            TerrainDef terrain = map.terrainGrid.UnderTerrainAt(cell), underTerrain;
            if (terrain != null)
                return terrain;

            bool foundGround = false;
            foreach (IntVec3 c in cell.AdjacentCells())
            {
                if (!c.InBounds(map))
                    continue;
                terrain = c.GetTerrain(map);
                if (terrain.IsWater)
                    return terrain;
                underTerrain = map.terrainGrid.UnderTerrainAt(c);
                if (underTerrain != null && underTerrain.IsWater)
                    return underTerrain;
                if (terrain != TerrainDefOf.Ice || (underTerrain != null && underTerrain != TerrainDefOf.Ice))
                    foundGround = true;
            }
            
            if (foundGround)
                return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanShallow : TerrainDefOf.WaterShallow;
            return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanDeep : TerrainDefOf.WaterDeep;
        }
    }
}
