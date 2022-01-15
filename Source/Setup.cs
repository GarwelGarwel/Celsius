using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TemperaturesPlus
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

            harmony = new Harmony("Garwel.TemperaturesPlus");
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
            temperature = TemperatureUtility.GetTemperatureForCell(c, map);
            __result = true;
            return false;
        }

        // Replaces Room.Temperature with room's average temperature (e.g. for displaying in the bottom right corner)
        public static bool Room_Temperature_get(ref float __result, Room __instance)
        {
            float oldResult = __result;
            TemperatureInfo temperatureInfo = __instance.Map.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo unavailable for {__instance?.Map}.", LogLevel.Error);
                return true;
            }
            __result = __instance.Cells.Average(cell => temperatureInfo.GetTemperatureForCell(cell));
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
