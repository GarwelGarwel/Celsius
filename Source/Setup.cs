using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TemperaturesPlus
{
    [StaticConstructorOnStartup]
    public static class Setup
    {
        static Harmony harmony;

        static Setup()
        {
            Harmony.DEBUG = Prefs.DevMode;

            if (harmony != null)
                return;

            harmony = new Harmony("Garwel.TemperaturesPlus");
            Type type = typeof(Setup);

            void LogPatchError(string methodName) => LogUtility.Log($"Error patching {methodName}.", LogLevel.Error);

            void Patch(string className, string methodName, bool patchPrefix = true, bool patchPostfix = false)
            {
                try
                {
                    if (harmony.Patch(
                        AccessTools.Method($"{className}:{methodName}"),
                        patchPrefix ? new HarmonyMethod(type.GetMethod($"{className}_{methodName}")) : null,
                        patchPostfix ? new HarmonyMethod(type.GetMethod($"{className}_{methodName}")) : null) == null)
                        LogPatchError($"{className}.{methodName}");
                }
                catch (Exception ex)
                { LogUtility.Log($"Exception while patching {className}.{methodName}: {ex}"); }
            }

            harmony.Patch(
                AccessTools.Method($"Verse.GenTemperature:TryGetDirectAirTemperatureForCell"),
                prefix: new HarmonyMethod(type.GetMethod($"GenTemperature_TryGetDirectAirTemperatureForCell")));
            harmony.Patch(
                AccessTools.PropertyGetter(typeof(Room), "Temperature"),
                postfix: new HarmonyMethod(type.GetMethod("Room_Temperature_get")));
            LogUtility.Log($"Initialization complete.");
        }

        public static bool GenTemperature_TryGetDirectAirTemperatureForCell(ref bool __result, IntVec3 c, Map map, out float temperature)
        {
            temperature = TemperatureUtility.GetTemperatureForCell(c, map);
            if (c == UI.MouseCell())
                LogUtility.Log($"Air temperature for {c} @ {map}: {temperature.ToStringTemperature()}.");
            __result = true;
            return false;
        }

        // Can change to prefix when tested enough
        public static void Room_Temperature_get(ref float __result, Room __instance)
        {
            float oldResult = __result;
            TemperatureInfo temperatureInfo = __instance.Map.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo unavailable for {__instance?.Map}.", LogLevel.Error);
                return;
            }
            __result = __instance.Cells.Average(cell => temperatureInfo.GetTemperatureForCell(cell));
            if (UI.MouseCell().GetRoom(__instance.Map) == __instance && Find.TickManager.TicksGame % 150 == 0)
                LogUtility.Log($"Room temperature for {__instance.ID} ({__instance.CellCount} cells): {__result:F1}. Vanilla result: {oldResult:F1}.");
        }
    }
}
