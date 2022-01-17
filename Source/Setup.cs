using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Celsius
{
    /// <summary>
    /// Harmony patches and Defs preparation on game startup
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class Setup
    {
        static Harmony harmony;

        static Setup()
        {
            // Setting up Harmony
            if (harmony != null)
                return;

            harmony = new Harmony("Garwel.Celsius");
            Type type = typeof(Setup);

            harmony.Patch(
                AccessTools.Method($"Verse.GenTemperature:TryGetDirectAirTemperatureForCell"),
                prefix: new HarmonyMethod(type.GetMethod($"GenTemperature_TryGetDirectAirTemperatureForCell")));
            harmony.Patch(
                AccessTools.PropertyGetter(typeof(Room), "Temperature"),
                prefix: new HarmonyMethod(type.GetMethod("Room_Temperature_get")));
            harmony.Patch(
                AccessTools.Method($"Verse.GenTemperature:PushHeat", new Type[] { typeof(IntVec3), typeof(Map), typeof(float) }),
                prefix: new HarmonyMethod(type.GetMethod($"GenTemperature_PushHeat")));
            harmony.Patch(
                AccessTools.Method($"Verse.GenTemperature:ControlTemperatureTempChange"),
                postfix: new HarmonyMethod(type.GetMethod($"GenTemperature_ControlTemperatureTempChange")));
            harmony.Patch(
                AccessTools.Method($"Verse.AttachableThing:Destroy"),
                prefix: new HarmonyMethod(type.GetMethod($"AttachableThing_Destroy")));
            harmony.Patch(
                AccessTools.Method($"RimWorld.JobGiver_SeekSafeTemperature:ClosestRegionWithinTemperatureRange"),
                prefix: new HarmonyMethod(type.GetMethod($"JobGiver_SeekSafeTemperature_ClosestRegionWithinTemperatureRange")));
            harmony.Patch(
                AccessTools.Method($"Verse.DangerUtility:GetDangerFor"),
                postfix: new HarmonyMethod(type.GetMethod($"DangerUtility_GetDangerFor")));
            LogUtility.Log($"Harmony initialization complete.");

            // Adding CompThermal and ThingThermalProperties to all applicable Things
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(def => CompThermal.ShouldApplyTo(def)))
            {
                if (def.IsMeat)
                {
                    if (def.modExtensions == null)
                        def.modExtensions = new List<DefModExtension>();
                }
                def.comps.Add(new CompProperties(typeof(CompThermal)));
            }

            TemperatureUtility.RecalculateAirProperties();
        }

        // Replaces GenTemperature.TryGetDirectAirTemperatureForCell by providing cell-specific temperature
        public static bool GenTemperature_TryGetDirectAirTemperatureForCell(ref bool __result, IntVec3 c, Map map, out float temperature)
        {
            temperature = c.GetTemperatureForCell(map);
            __result = true;
            return false;
        }

        // Replaces Room.Temperature with room's average temperature (e.g. for displaying in the bottom right corner)
        public static bool Room_Temperature_get(ref float __result, Room __instance)
        {
            if (__instance.Map.GetComponent<TemperatureInfo>() == null)
                return true;
            __result = __instance.GetTemperature();
            return false;
        }

        // Replaces GenTemperature.PushHeat(IntVec3, Map, float) to change temperature at the specific cell instead of the whole room
        public static bool GenTemperature_PushHeat(ref bool __result, IntVec3 c, Map map, float energy) => __result = TemperatureUtility.TryPushHeat(c, map, energy);

        // Attaches to GenTemperature.ControlTemperatureTempChange to implement heat pushing for temperature control things (Heater, Cooler, Vent)
        public static float GenTemperature_ControlTemperatureTempChange(float result, IntVec3 cell, Map map, float energyLimit, float targetTemperature)
        {
            Room room = cell.GetRoom(map);
            float roomTemp = room != null ? room.GetTemperature() : cell.GetTemperatureForCell(map);
            if (UI.MouseCell() == cell)
                LogUtility.Log($"ControlTemperatureTempChange({cell}, {energyLimit}, {targetTemperature}). Room temperature: {roomTemp:F1}C.");

            if (energyLimit > 0)
                if (roomTemp < targetTemperature - TemperatureUtility.TemperatureChangePrecision)
                {
                    TemperatureUtility.TryPushHeat(cell, map, energyLimit);
                    return energyLimit;
                }
                else return 0;
            else if (roomTemp > targetTemperature + TemperatureUtility.TemperatureChangePrecision)
            {
                TemperatureUtility.TryPushHeat(cell, map, energyLimit);
                return energyLimit;
            }
            else return 0;
        }

        // Attaches to AttachableThing.Destroy to reduce temperature when a Fire is destroyed to the ignition temperature
        public static void AttachableThing_Destroy(AttachableThing __instance)
        {
            if (Settings.AutoignitionEnabled && __instance is Fire)
            {
                TemperatureInfo temperatureInfo = __instance.Map?.TemperatureInfo();
                if (temperatureInfo != null)
                {
                    float temperature = temperatureInfo.GetIgnitionTemperatureForCell(__instance.Position);
                    LogUtility.Log($"Setting temperature at {__instance.Position} to {temperature:F0}C...");
                    temperatureInfo.SetTempteratureForCell(__instance.Position, Mathf.Min(temperatureInfo.GetTemperatureForCell(__instance.Position), temperature));
                }
            }
        }

        // Replaces JobGiver_SeekSafeTemperature.ClosestRegionWithinTemperatureRange to only seek regions with no dangerous cells
        public static bool JobGiver_SeekSafeTemperature_ClosestRegionWithinTemperatureRange(ref Region __result, JobGiver_SeekSafeTemperature __instance, IntVec3 root, Map map, FloatRange tempRange, TraverseParms traverseParms)
        {
            LogUtility.Log($"JobGiver_SeekSafeTemperature_ClosestRegionWithinTemperatureRange for {root.GetFirstPawn(map)} at {root} (t = {root.GetTemperatureForCell(map):F1}C)");
            Region region = root.GetRegion(map, RegionType.Set_Passable);
            if (region == null)
                return false;
            RegionEntryPredicate entryCondition = (Region from, Region r) => r.Allows(traverseParms, false);
            Region foundReg = null;
            RegionProcessor regionProcessor = delegate (Region r)
            {
                if (r.IsDoorway)
                    return false;
                if (r.Cells.All(cell => tempRange.Includes(cell.GetTemperatureForCell(map))))
                {
                    foundReg = r;
                    return true;
                }
                return false;
            };
            RegionTraverser.BreadthFirstTraverse(region, entryCondition, regionProcessor, 9999, RegionType.Set_Passable);
            LogUtility.Log($"Safe region found: {foundReg}");
            __result = foundReg;
            return false;
        }

        // Attaches to DangerUtility.GetDangerFor to mark specific (too hot or too cold) cells as dangerous
        public static Danger DangerUtility_GetDangerFor(Danger result, IntVec3 c, Pawn p, Map map)
        {
            float temperature = c.GetTemperatureForCell(map);
            FloatRange range = p.SafeTemperatureRange();
            Danger danger = range.Includes(temperature) ? Danger.None : (range.ExpandedBy(80).Includes(temperature) ? Danger.Some : Danger.Deadly);
            return danger > result ? danger : result;
        }
    }
}
