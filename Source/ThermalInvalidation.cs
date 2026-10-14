using HarmonyLib;
using Verse;

namespace Celsius
{
    // All hooks run on the game thread. They mark a small input snapshot dirty rather than
    // rebuilding the entire property cache periodically from a background thread.
    internal static class ThermalInvalidation
    {
        internal static void Install(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(SnowGrid), "CheckVisualOrPathCostChange"),
                prefix: new HarmonyMethod(typeof(SnowMeshUpdates), nameof(SnowMeshUpdates.VisibilityCrossing)),
                transpiler: new HarmonyMethod(typeof(SnowMeshUpdates), nameof(SnowMeshUpdates.Transpiler)));
            Patch(harmony, "Verse.ThingGrid:RegisterInCell", nameof(ThingChanged));
            Patch(harmony, "Verse.ThingGrid:DeregisterInCell", nameof(ThingChanged));
            Patch(harmony, "Verse.TerrainGrid:SetTerrain", nameof(TerrainChanged));
            Patch(harmony, "Verse.TerrainGrid:RemoveTopLayer", nameof(TerrainChanged));
            Patch(harmony, "Verse.RoofGrid:SetRoof", nameof(RoofChanged));
            Patch(harmony, "Verse.SnowGrid:AddDepth", nameof(SnowChanged));
            Patch(harmony, "Verse.SnowGrid:SetDepth", nameof(SnowChanged));
            Patch(harmony, "Verse.Room:Notify_RoomShapeChanged", nameof(RoomChanged));
            harmony.Patch(AccessTools.Method("Verse.Profile.MemoryUtility:ClearAllMapsAndWorld"),
                prefix: new HarmonyMethod(typeof(ThermalInvalidation), nameof(StopAll)));
        }
        static void Patch(Harmony h, string target, string callback) =>
            h.Patch(AccessTools.Method(target), postfix: new HarmonyMethod(typeof(ThermalInvalidation), callback));

        static void ThingChanged(Thing __0, IntVec3 __1, Map ___map)
        {
            if (CompThermal.ShouldApplyTo(__0.def)) ___map.TemperatureInfo()?.InvalidateCell(__1);
        }
        static void TerrainChanged(IntVec3 __0, Map ___map) => ___map.TemperatureInfo()?.InvalidateCell(__0);
        static void RoofChanged(IntVec3 __0, Map ___map) => ___map.TemperatureInfo()?.InvalidateCell(__0);
        static void SnowChanged(IntVec3 __0, Map ___map) => ___map.TemperatureInfo()?.InvalidateSnow(__0);
        static void RoomChanged(Room __instance) => __instance.Map?.TemperatureInfo()?.InvalidateRooms();
        static void StopAll()
        {
            foreach (TemperatureInfo info in TemperatureUtility.temperatureInfos.Values) info.StopWorker();
            TemperatureUtility.temperatureInfos.Clear();
        }
    }
}
