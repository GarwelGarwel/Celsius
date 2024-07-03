using RimWorld;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;

using static Celsius.LogUtility;

namespace Celsius
{
    static class FreezeMeltUtility
    {
        public const float FreezeTemperature = -0.5f;
        public const float MeltTemperature = 0.5f;

#if DEBUG
        static Stopwatch stopwatch = new Stopwatch();
        static int iterations;
#endif
        public static bool Freezable(this TerrainDef terrain) => terrain.HasTag("Freezable");

        public static bool Meltable(this TerrainDef terrain) => terrain.HasTag("Meltable");

        public static bool ShouldFreeze(this TerrainDef terrain, float temperature) => temperature < FreezeTemperature && terrain.Freezable();

        public static bool ShouldMelt(this TerrainDef terrain, float temperature) => temperature > MeltTemperature && terrain.Meltable();

        //like GenAdjFast.AdjacentCells8Way, but faster because it works with indices
        public static int[] AdjacentCells8WayFast(int centerIndex, int mapSizeX)
        {
            int[] cells = new int[8]; //I have no idea if this is even worth it, but it's probably faster than setting up 2 loops
            cells[0] = centerIndex - mapSizeX - 1;
            cells[1] = centerIndex - mapSizeX;
            cells[2] = centerIndex - mapSizeX + 1;
            cells[3] = centerIndex - 1;
            cells[4] = centerIndex + 1;
            cells[5] = centerIndex + mapSizeX - 1;
            cells[6] = centerIndex + mapSizeX;
            cells[7] = centerIndex + mapSizeX + 1;
            return cells;
        }

        /// <summary>
        /// Returns best guess for what kind of water terrain should be placed in a cell (if Ice melts there)
        /// </summary>
        public static TerrainDef BestUnderIceTerrain(this IntVec3 cell, Map map, int index)
        {
            TerrainDef terrain = map.terrainGrid.UnderTerrainAt(index);
            if (terrain != null)
                return terrain;

            bool foundGround = false;
            var adjCells = AdjacentCells8WayFast(index, map.Size.x);
            for (int i = 0; i < 8; i++)
            {
                int adjIndex = adjCells[i];
                if (adjIndex >= 0 && adjIndex < (map.Size.x * map.Size.z))
                    continue;
                terrain = map.terrainGrid.TerrainAt(adjIndex);
                if (terrain.Freezable())
                    return terrain;
                var underTerrain = map.terrainGrid.UnderTerrainAt(adjIndex);
                if (underTerrain != null && underTerrain.Freezable())
                    return underTerrain;
                if (!terrain.Meltable() || (underTerrain != null && !underTerrain.Meltable()))
                    foundGround = true;
            }

            if (foundGround)
                return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanShallow : TerrainDefOf.WaterShallow;
            return map.Biome == BiomeDefOf.SeaIce ? TerrainDefOf.WaterOceanDeep : TerrainDefOf.WaterDeep;
        }

#if DEBUG
        static void LogStopwatch()
        {
            stopwatch.Stop();
            if (++iterations == 0)
                Log($"{iterations} freeze/melt cycles @ {stopwatch.Elapsed.TotalMilliseconds / iterations:F3} ms.");
        }
#endif

        /// <summary>
        /// Turns the given cell into ice
        /// </summary>
        public static void FreezeTerrain(this IntVec3 cell, Map map, int cellIndex, object mutex, bool log = false)
        {
#if DEBUG
            stopwatch.Start();
#endif
            TerrainDef terrain = map.terrainGrid.TerrainAt(cellIndex);
            if (log)
                Log($"{terrain} freezes at {map.cellIndices.IndexToCell(cellIndex)}.");
            lock (mutex)
            {
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Ice);
                map.terrainGrid.SetUnderTerrain(cell, terrain);
            }
#if DEBUG
            LogStopwatch();
#endif
        }

        /// <summary>
        /// Turns the given cell into the appropriate kind of water terrain; 
        /// </summary>
        public static void MeltTerrain(this IntVec3 cell, Map map, int index, object mutex, bool log = false)
        {
#if DEBUG
            stopwatch.Start();
#endif
            TerrainDef meltedTerrain = cell.BestUnderIceTerrain(map, index);
            // Removing things that can't stay on the melted terrain
            List<Thing> things = map.thingGrid.ThingsListAtFast(index);
            lock (mutex)
            {
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    Thing thing = things[i];
                    if (meltedTerrain.passability == Traversability.Impassable)
                        if (thing is Pawn pawn)
                        {
                            Log($"{pawn.LabelCap} drowns in {meltedTerrain.label}.");
                            pawn.health?.AddHediff(DefOf.Celsius_Hediff_Drown, dinfo: new DamageInfo(DefOf.Celsius_Damage_Drown, 1));
                            pawn.Corpse?.Destroy();
                        }
                        else
                        {
                            Log($"{thing.LabelCap} sinks in {meltedTerrain.label}.");
                            CompDissolution compDissolution = thing.TryGetComp<CompDissolution>();
                            if (compDissolution != null)
                            {
                                Log($"Applying dissolution effects of {thing.def}.");
                                compDissolution.TriggerDissolutionEvent(thing.stackCount);
                            }
                            else thing.Destroy();
                        }
                    else
                    {
                        TerrainAffordanceDef terrainAffordance = thing.TerrainAffordanceNeeded;
                        if (terrainAffordance != null && !meltedTerrain.affordances.Contains(terrainAffordance))
                        {
                            Log($"{thing.def}'s terrain affordance: {terrainAffordance}. {meltedTerrain.LabelCap} provides: {meltedTerrain.affordances.Select(def => def.defName).ToCommaList()}. {thing.LabelCap} can't stand on {meltedTerrain.label} and is destroyed.");
                            if (thing is Building_Grave grave && grave.HasAnyContents)
                            {
                                Log($"Grave with {grave.ContainedThing?.LabelShort} is uncovered due to melting.");
                                grave.EjectContents();
                            }
                            thing.Destroy();
                        }
                    }
                }

                // Changing terrain
                if (map.terrainGrid.UnderTerrainAt(index) == null)
                    map.terrainGrid.SetUnderTerrain(cell, meltedTerrain);
                if (log)
                    Log($"Ice at {cell} melts into {meltedTerrain}.");
                map.terrainGrid.RemoveTopLayer(cell, false);
                if (map.snowGrid.GetDepth(cell) > 0)
                    map.snowGrid.SetDepth(cell, 0);
            }
            
#if DEBUG
            LogStopwatch();
#endif
        }

        public static float SnowMeltAmountAt(float temperature) => temperature * Mathf.Lerp(0, 0.0058f, temperature / 10);
    }
}
