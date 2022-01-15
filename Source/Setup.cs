using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static RimWorld.ColonistBar;

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
                AccessTools.PropertyGetter(typeof(Thing), "AmbientTemperature"),
                prefix: new HarmonyMethod(type.GetMethod("Thing_AmbientTemperature_get")));
            harmony.Patch(
                AccessTools.Method($"Verse.GenTemperature:PushHeat", new Type[] { typeof(IntVec3), typeof(Map), typeof(float) }),
                prefix: new HarmonyMethod(type.GetMethod($"GenTemperature_PushHeat")));
            harmony.Patch(
                AccessTools.Method($"Verse.GenTemperature:ControlTemperatureTempChange"),
                postfix: new HarmonyMethod(type.GetMethod($"GenTemperature_ControlTemperatureTempChange")));
            harmony.Patch(
                AccessTools.Method($"Verse.AttachableThing:Destroy"),
                prefix: new HarmonyMethod(type.GetMethod($"AttachableThing_Destroy")));
            LogUtility.Log($"Harmony initialization complete.");

            // Adding CompThermal and ThingThermalProperties to all applicable Things
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(def => CompThermal.ShouldApplyTo(def)))
            {
                if (def.IsMeat)
                {
                    if (def.modExtensions == null)
                        def.modExtensions = new List<DefModExtension>();
                    def.modExtensions.Add(ThingThermalProperties.Meat);
                }
                def.comps.Add(new CompProperties(typeof(CompThermal)));
            }
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

        // Replaces Thing.AmbientTemperature with thing's own temperature if it has one
        public static bool Thing_AmbientTemperature_get(ref float __result, Thing __instance)
        {
            CompThermal comp = __instance.TryGetComp<CompThermal>();
            if (comp == null || !__instance.Spawned || !comp.HasTemperature)
                return true;
            __result = comp.temperature;
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

        // Replaces AttachableThing.Destroy to reduce temperature when a Fire is destroyed to the ignition temperature
        public static void AttachableThing_Destroy(AttachableThing __instance)
        {
            LogUtility.Log($"AttachableThing_Destroy({__instance})");
            if (__instance is Fire)
            {
                TemperatureInfo temperatureInfo = __instance.Map?.TemperatureInfo();
                if (temperatureInfo != null)
                {
                    float temperature = temperatureInfo.GetIgnitionTemperatureForCell(__instance.Position);
                    LogUtility.Log($"Setting temperature at {__instance.Position} to {temperature:F0}C...");
                    temperatureInfo.SetTempteratureForCell(__instance.Position, Mathf.Min(temperatureInfo.GetTemperatureForCell(__instance.Position), temperature));
                    foreach (CompThermal compThermal in __instance.Position.GetThingList(__instance.Map)
                        .OfType<ThingWithComps>()
                        .Select(thing => thing.GetComp<CompThermal>())
                        .Where(comp => comp != null && comp.HasTemperature))
                        compThermal.temperature = Mathf.Min(compThermal.temperature, temperature);
                }
            }
        }
    }
}
