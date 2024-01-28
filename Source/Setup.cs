using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
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

            LogUtility.Log($"Initializing Celsius {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}...", LogLevel.Important);

            harmony = new Harmony("Garwel.Celsius");
            Type type = typeof(Setup);

            harmony.Patch(
                AccessTools.Method("Verse.GenTemperature:TryGetDirectAirTemperatureForCell"),
                prefix: new HarmonyMethod(type.GetMethod($"GenTemperature_TryGetDirectAirTemperatureForCell")));
            harmony.Patch(
                AccessTools.PropertyGetter(typeof(Room), "Temperature"),
                prefix: new HarmonyMethod(type.GetMethod("Room_Temperature_get")));
            harmony.Patch(
                AccessTools.Method("Verse.GenTemperature:PushHeat", new Type[] { typeof(IntVec3), typeof(Map), typeof(float) }),
                prefix: new HarmonyMethod(type.GetMethod("GenTemperature_PushHeat_IntVec3")));
            harmony.Patch(
                AccessTools.Method("Verse.GenTemperature:PushHeat", new Type[] { typeof(Thing), typeof(float) }),
                prefix: new HarmonyMethod(type.GetMethod("GenTemperature_PushHeat_Thing")));
            harmony.Patch(
                AccessTools.Method("Verse.GenTemperature:ControlTemperatureTempChange"),
                postfix: new HarmonyMethod(type.GetMethod("GenTemperature_ControlTemperatureTempChange")));
            harmony.Patch(
                AccessTools.Method("RimWorld.SteadyEnvironmentEffects:MeltAmountAt"),
                postfix: new HarmonyMethod(type.GetMethod("SteadyEnvironmentEffects_MeltAmountAt")));
            harmony.Patch(
                AccessTools.Method("Verse.AttachableThing:Destroy"),
                prefix: new HarmonyMethod(type.GetMethod("AttachableThing_Destroy")));
            harmony.Patch(
                AccessTools.Method("RimWorld.JobGiver_SeekSafeTemperature:ClosestRegionWithinTemperatureRange"),
                prefix: new HarmonyMethod(type.GetMethod("JobGiver_SeekSafeTemperature_ClosestRegionWithinTemperatureRange")));
            harmony.Patch(
                AccessTools.Method("Verse.DangerUtility:GetDangerFor"),
                postfix: new HarmonyMethod(type.GetMethod("DangerUtility_GetDangerFor")));
            harmony.Patch(
                AccessTools.Method("Verse.MapTemperature:TemperatureUpdate"),
                prefix: new HarmonyMethod(type.GetMethod("MapTemperature_TemperatureUpdate")));
            harmony.Patch(
                AccessTools.Method("RimWorld.GlobalControls:TemperatureString"),
                prefix: new HarmonyMethod(type.GetMethod("GlobalControls_TemperatureString")));
            harmony.Patch(
                AccessTools.Method("RimWorld.Building_Door:DoorOpen"),
                postfix: new HarmonyMethod(type.GetMethod("Building_Door_DoorOpen")));
            harmony.Patch(
                AccessTools.Method("RimWorld.Building_Door:DoorTryClose"),
                postfix: new HarmonyMethod(type.GetMethod("Building_Door_DoorTryClose")));
            harmony.Patch(
                AccessTools.Method("RimWorld.CompRitualFireOverlay:CompTick"),
                postfix: new HarmonyMethod(type.GetMethod("CompRitualFireOverlay_CompTick")));
            if (AccessTools.Method("VanillaVehiclesExpanded.GarageDoor:SpawnGarage") != null)
                harmony.Patch(
                    AccessTools.Method("VanillaVehiclesExpanded.GarageDoor:SpawnGarage"),
                    postfix: new HarmonyMethod(type.GetMethod("VVE_GarageDoor_SpawnGarage")));

            LogUtility.Log($"Harmony initialization complete.", LogLevel.Important);

            // Adding CompThermal to all applicable Things
            List<ThingThermalProperties> ttpList;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs.Where(def => CompThermal.ShouldApplyTo(def)))
            {
                def.comps.Add(new CompProperties(typeof(CompThermal)));
                if ((ttpList = def.modExtensions.OfType<ThingThermalProperties>().ToList()).Count > 1)
                    LogUtility.Log($"{def.defName} has {ttpList.Count} ThingThermalProperties extensions:\n{ttpList.Select(ttp => ttp.ToString()).ToLineList("- ")}", LogLevel.Warning);
            }

            if (Settings.DebugMode)
                foreach (TerrainDef def in DefDatabase<TerrainDef>.AllDefs)
                {
                    if (def.Freezable() || def.Meltable())
                        LogUtility.Log($"Terrain {def.defName}. Tags: {def.tags.ToCommaList()}.");
                }

            TemperatureUtility.SettingsChanged();
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
            if (__instance?.Map?.TemperatureInfo() == null)
                return true;
            __result = __instance.GetTemperature();
            return false;
        }

        // Replaces GenTemperature.PushHeat(IntVec3, Map, float) to change temperature at the specific cell instead of the whole room
        public static bool GenTemperature_PushHeat_IntVec3(ref bool __result, IntVec3 c, Map map, float energy) => __result = TemperatureUtility.TryPushHeat(c, map, energy);

        // Replaces GenTemperature.PushHeat(Thing, float) to push heat evenly from big things (e.g. geysers)
        public static bool GenTemperature_PushHeat_Thing(Thing t, float energy)
        {
            if (t.def.Size.x == 1 && t.def.Size.z == 1)
                return !TemperatureUtility.TryPushHeat(t.PositionHeld, t.MapHeld, energy);
            TemperatureInfo temperatureInfo = t.MapHeld?.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo unavailable for map {t.MapHeld} where {t} is held!", LogLevel.Warning);
                return true;
            }
            CellRect cells = t.OccupiedRect();
            energy /= cells.Area;
            for (int x = cells.minX; x <= cells.maxX; x++)
                for (int z = cells.minZ; z <= cells.maxZ; z++)
                    temperatureInfo.PushHeat(new IntVec3(x, 0, z), energy);
            return false;
        }

        // Attaches to GenTemperature.ControlTemperatureTempChange to implement heat pushing for temperature control things (Heater, Cooler, Vent)
        public static float GenTemperature_ControlTemperatureTempChange(float result, IntVec3 cell, Map map, float energyLimit, float targetTemperature)
        {
            Room room = cell.GetRoom(map);
            float roomTemp = room != null && !room.TouchesMapEdge ? room.GetTemperature() : cell.GetSurroundingTemperature(map);
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

        // Disables vanilla snow melting
        public static float SteadyEnvironmentEffects_MeltAmountAt(float result, float temperature) => 0;

        // Attaches to AttachableThing.Destroy to reduce temperature when a Fire is destroyed to the ignition temperature
        public static void AttachableThing_Destroy(AttachableThing __instance)
        {
            if (Settings.AutoignitionEnabled && __instance is Fire)
            {
                TemperatureInfo temperatureInfo = __instance.Map?.TemperatureInfo();
                if (temperatureInfo != null)
                {
                    float temperature = temperatureInfo.GetIgnitionTemperatureForCell(__instance.Position);
                    if (temperature < temperatureInfo.GetTemperatureForCell(__instance.Position))
                    {
                        LogUtility.Log($"Setting temperature at {__instance.Position} to {temperature:F0}C...");
                        temperatureInfo.SetTemperatureForCell(__instance.Position, temperature);
                    }
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
            RegionTraverser.BreadthFirstTraverse(region, (Region from, Region r) => r.Allows(traverseParms, false), regionProcessor);
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

        // Disable MapTemperature.TemperatureUpdate, because vanilla temperature overlay is not used anymore
        public static bool MapTemperature_TemperatureUpdate() => false;

        // Replaces temperature display in the global controls view (bottom right)
        public static bool GlobalControls_TemperatureString(ref string __result)
        {
            IntVec3 cell = UI.MouseCell();
            TemperatureInfo temperatureInfo = Find.CurrentMap?.TemperatureInfo();
            if (temperatureInfo == null || !cell.InBounds(Find.CurrentMap) || cell.Fogged(Find.CurrentMap))
                return true;
            __result = temperatureInfo.GetTemperatureForCell(cell).ToStringTemperature(Settings.TemperatureDisplayFormatString);
            if (temperatureInfo.HasTerrainTemperatures)
            {
                float terrainTemperature = temperatureInfo.GetTerrainTemperature(cell);
                if (!float.IsNaN(terrainTemperature))
                    // Localization Key: Celsius_Terrain - Terrain
                    __result += $" / {"Celsius_Terrain".Translate()} {terrainTemperature.ToStringTemperature(Settings.TemperatureDisplayFormatString)}";
            }
            return false;
        }

        // When door is opening, update its state and thermal values
        public static void Building_Door_DoorOpen(Building_Door __instance)
        {
            CompThermal compThermal = __instance?.TryGetComp<CompThermal>();
            if (compThermal != null)
                compThermal.IsOpen = true;
        }

        // When door is closing, update its state and thermal values
        public static void Building_Door_DoorTryClose(Building_Door __instance, bool __result)
        {
            if (__result)
            {
                CompThermal compThermal = __instance?.TryGetComp<CompThermal>();
                if (compThermal != null)
                    compThermal.IsOpen = false;
            }
        }

        const float HeatPushPerFireSize = 21;

        // Adds heat push to ritual fires
        public static void CompRitualFireOverlay_CompTick(CompRitualFireOverlay __instance)
        {
            if (GenTicks.TicksAbs % 60 == 0 && __instance.FireSize > 0)
                TemperatureUtility.TryPushHeat(__instance.parent.Position, __instance.parent.Map, __instance.FireSize * HeatPushPerFireSize);
        }

        // Vanilla Vehicles Expanded: When opening or closing a garage door, update its state and thermal values
        public static void VVE_GarageDoor_SpawnGarage(Building newGarage)
        {
            if (newGarage == null)
            {
                LogUtility.Log($"Error in VVE_GarageDoor_SpawnGarage: newGarage is null!", LogLevel.Error);
                return;
            }
            CompThermal compThermal = newGarage.TryGetComp<CompThermal>();
            if (compThermal != null)
                compThermal.IsOpen = newGarage.def.defName.EndsWith("Opened");
            else LogUtility.Log($"There is no CompThermal for {newGarage.ThingID}.", LogLevel.Warning);
        }
    }
}
