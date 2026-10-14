using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace Celsius
{
    // Snow opacity can change without changing any Thing's printed geometry.
    internal static class SnowMeshUpdates
    {
        [ThreadStatic] internal static bool CelsiusPublication;
        static readonly AccessTools.FieldRef<SnowGrid, Map> SnowMapOf = AccessTools.FieldRefAccess<SnowGrid, Map>("map");
        static readonly AccessTools.FieldRef<MapDrawer, Map> MapOf = AccessTools.FieldRefAccess<MapDrawer, Map>("map");

        // A filtered later refresh must not leave an earlier hide/show crossing stale.
        public static void VisibilityCrossing(SnowGrid __instance, IntVec3 __0, float __1, float __2)
        {
            if (!CelsiusPublication) return;
            Map map = SnowMapOf(__instance);
            if (VisibilityChanged(map, __0, __1, __2))
                map.mapDrawer.MapMeshDirty(__0, (ulong)MapMeshFlagDefOf.Things, true, false);
        }

        internal static bool VisibilityChanged(Map map, IntVec3 cell, float oldDepth, float newDepth)
        {
            float sandDepth = cell.GetSandDepth(map);
            List<Thing> things = map.thingGrid.ThingsListAt(cell);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing;
                // Conservatively retain redraws if another patch changes the list during this callback.
                try { thing = things[i]; }
                catch (ArgumentOutOfRangeException) { return true; }
                if (thing?.def == null) return true;
                float threshold = thing.def.hideAtSnowOrSandDepth;
                if (threshold < 1f && (Math.Max(oldDepth, sandDepth) > threshold) != (Math.Max(newDepth, sandDepth) > threshold))
                    return true;
            }
            return false;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.Method(typeof(MapDrawer), nameof(MapDrawer.MapMeshDirty),
                new[] { typeof(IntVec3), typeof(ulong), typeof(bool), typeof(bool) });
            MethodInfo replacement = AccessTools.Method(typeof(SnowMeshUpdates), nameof(MarkMeshDirty));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(original))
                {
                    // Retain labels on the first instruction of the replacement sequence.
                    yield return new CodeInstruction(OpCodes.Ldarg_2).WithLabels(instruction.labels);
                    yield return new CodeInstruction(OpCodes.Ldarg_3);
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                }
                else yield return instruction;
            }
        }

        static void MarkMeshDirty(MapDrawer drawer, IntVec3 cell, ulong flags,
            bool adjacentCells, bool adjacentSections, float oldDepth, float newDepth)
        {
            if (CelsiusPublication && flags == (ulong)MapMeshFlagDefOf.Things && !NeedsThingsUpdate(MapOf(drawer), cell, oldDepth, newDepth))
                return;
            drawer.MapMeshDirty(cell, flags, adjacentCells, adjacentSections);
        }

        internal static bool NeedsThingsUpdate(Map map, IntVec3 cell, float oldDepth, float newDepth)
        {
            float sandDepth = cell.GetSandDepth(map);
            List<Thing> things = map.thingGrid.ThingsListAt(cell);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing;
                // Conservatively retain redraws if another patch changes the list during this callback.
                try { thing = things[i]; }
                catch (ArgumentOutOfRangeException) { return true; }
                if (thing?.def == null) return true;
                float threshold = thing.def.hideAtSnowOrSandDepth;
                if (threshold < 1f && (Math.Max(oldDepth, sandDepth) > threshold) != (Math.Max(newDepth, sandDepth) > threshold))
                    return true;
                // Custom building graphics may depend on snow without a hide threshold.
                if (thing.def.category == ThingCategory.Building)
                    return true;
            }
            // Retain nearby wall/linked-building redraws for snow-overlay mods.
            foreach (IntVec3 offset in GenAdj.AdjacentCells)
            {
                IntVec3 adjacent = cell + offset;
                if (adjacent.InBounds(map) && map.edificeGrid[adjacent] != null)
                    return true;
            }
            return false;
        }
    }
}
