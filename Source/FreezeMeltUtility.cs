using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Celsius
{
    static class FreezeMeltUtility
    {
        public const float FreezeTemperature = -0.5f;
        public const float MeltTemperature = 0.5f;

        public static bool ShouldFreeze(this TerrainDef terrain, float temperature) => temperature < FreezeTemperature && terrain.IsWater;

        public static bool ShouldMelt(this TerrainDef terrain, float temperature) => temperature > MeltTemperature && terrain == TerrainDefOf.Ice;

        /// <summary>
        /// Returns best guess for what kind of water terrain should be placed in a cell (if Ice melts there)
        /// </summary>
        public static TerrainDef BestUnderIceTerrain(this IntVec3 cell, Map map)
        {
            TerrainDef terrain = map.terrainGrid.UnderTerrainAt(cell), underTerrain;
            if (terrain != null)
                return terrain;

            bool foundGround = false;
            foreach (IntVec3 c in GenAdjFast.AdjacentCells8Way(cell))
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

        public static void FreezeTerrain(this IntVec3 cell, Map map, bool log = false)
        {
            TerrainDef terrain = cell.GetTerrain(map);
            if (log)
                LogUtility.Log($"{terrain} freezes at {cell}.");
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Ice);
            map.terrainGrid.SetUnderTerrain(cell, terrain);
        }

        public static void MeltTerrain(this IntVec3 cell, Map map, bool log = false)
        {
            TerrainDef meltedTerrain = cell.BestUnderIceTerrain(map);
            // Removing things that can't stay on the melted terrain
            List<Thing> things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = things[i];
                if (meltedTerrain.passability == Traversability.Impassable)
                    if (thing is Pawn pawn)
                    {
                        LogUtility.Log($"{pawn.LabelCap} drowns in {meltedTerrain.label}.");
                        pawn.health?.AddHediff(DefOf.Celsius_Hediff_Drown, dinfo: new DamageInfo(DefOf.Celsius_Damage_Drown, 1));
                        pawn.Corpse?.Destroy();
                    }
                    else
                    {
                        LogUtility.Log($"{thing.LabelCap} sinks in {meltedTerrain.label}.");
                        thing.Destroy();
                    }
                else if (thing is Building_Grave grave && grave.HasAnyContents)
                {
                    LogUtility.Log($"Grave with {grave.ContainedThing?.LabelShort} is uncovered due to melting.");
                    grave.EjectContents();
                    grave.Destroy();
                }
                else
                {
                    TerrainAffordanceDef terrainAffordance = thing.TerrainAffordanceNeeded;
                    if (terrainAffordance != null && !meltedTerrain.affordances.Contains(terrainAffordance))
                    {
                        LogUtility.Log($"{thing.LabelCap}'s terrain affordance: {terrainAffordance}. {meltedTerrain.LabelCap} provides: {meltedTerrain.affordances.Select(def => def.defName).ToCommaList()}. {thing.LabelCap} can't stand on {meltedTerrain.label} and is destroyed.");
                        thing.Destroy();
                    }
                }
            }

            // Changing terrain
            if (map.terrainGrid.UnderTerrainAt(cell) == null)
                map.terrainGrid.SetUnderTerrain(cell, meltedTerrain);
            if (log)
                LogUtility.Log($"Ice at {cell} melts into {meltedTerrain.defName}.");
            map.terrainGrid.RemoveTopLayer(cell, false);
            map.snowGrid.SetDepth(cell, 0);
        }

        public static float SnowMeltAmountAt(float temperature) => temperature * Mathf.Lerp(0, 0.0058f, temperature / 10);
    }
}
