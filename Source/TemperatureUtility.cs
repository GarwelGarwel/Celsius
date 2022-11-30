using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Celsius
{
    static class TemperatureUtility
    {
        public const float TemperatureChangePrecision = 0.01f;
        public const float MinFreezingTemperature = -3;

        public static TemperatureInfo TemperatureInfo(this Map map) => map.GetComponent<TemperatureInfo>();

        internal static void SettingsChanged()
        {
            FloatRange vanillaAutoIgnitionTemperatureRange = Settings.AutoignitionEnabled ? new FloatRange(10000, float.MaxValue) : new FloatRange(240, 1000);
            AccessTools.Field(typeof(SteadyEnvironmentEffects), "AutoIgnitionTemperatureRange").SetValue(null, vanillaAutoIgnitionTemperatureRange);

            // Resetting thermal props for all things and thingDefs
            if (Find.Maps != null)
                for (int m = 0; m < Find.Maps.Count; m++)
                {
                    List<Thing> things = Find.Maps[m]?.listerThings?.AllThings;
                    if (things != null)
                        for (int i = 0; i < things.Count; i++)
                            things[i].TryGetComp<CompThermal>()?.Reset();
                }
            for (int i = 0; i < DefDatabase<ThingDef>.AllDefsListForReading.Count; i++)
                DefDatabase<ThingDef>.AllDefsListForReading[i].GetModExtension<ThingThermalProperties>()?.Reset();
        }

        #region TEMPERATURE

        public static float GetTemperatureForCell(this IntVec3 cell, Map map)
        {
            TemperatureInfo tempInfo = map.TemperatureInfo();
            if (tempInfo == null)
                return map.mapTemperature.OutdoorTemp;
            return tempInfo.GetTemperatureForCell(cell);
        }

        public static float GetSurroundingTemperature(this IntVec3 cell, Map map)
        {
            TemperatureInfo tempInfo = map.TemperatureInfo();
            if (tempInfo == null || !cell.InBounds(map))
                return map.mapTemperature.OutdoorTemp;
            float sum = cell.GetTemperatureForCell(map);
            foreach (IntVec3 c in cell.AdjacentCells())
                sum += c.InBounds(map) ? c.GetTemperatureForCell(map) : cell.GetTemperatureForCell(map);
            return sum / 9;
        }

        public static float GetTemperature(this Room room)
        {
            if (room == null)
            {
                LogUtility.Log($"Trying to GetTemperature for null room!", LogLevel.Error);
                return TemperatureTuning.DefaultTemperature;
            }
            TemperatureInfo temperatureInfo = room.Map?.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo unavailable for {room.Map}.", LogLevel.Error);
                return room.Temperature;
            }
            if (room.TouchesMapEdge)
                return room.Map.mapTemperature.OutdoorTemp;
            return temperatureInfo.GetRoomTemperature(room);
        }

        #endregion TEMPERATURE

        #region DIFFUSION

        public static void CalculateHeatTransfer(float homeTemperature, float interactingTemperature, ThermalProps props, float airflow, ref float energy, ref float heatFlow, bool log = false)
        {
            float hf = props.HeatFlow;
            if (airflow != 1 || !props.IsAir)
            {
                if (airflow == 0 || props.airflow == 0)
                    hf /= Settings.ConvectionConductivityEffect;
                else hf /= Mathf.Pow(Settings.ConvectionConductivityEffect, 1 - airflow * props.airflow);
            }
            if (log)
                LogUtility.Log($"Interacting temperature: {interactingTemperature:F1}C. Mutual airflow: {airflow * props.airflow}. Heatflow: {hf}.");
            energy += (interactingTemperature - homeTemperature) * hf;
            heatFlow += hf;
        }

        public static void CalculateHeatTransferEnvironment(float cellTemperature, float environmentTemperature, ThermalProps props, ref float energy, ref float heatFlow, bool log = false)
        {
            float hf = props.HeatFlow * Settings.EnvironmentDiffusionFactor;
            if (!props.IsAir)
                hf /= Settings.ConvectionConductivityEffect;
            if (log)
                LogUtility.Log($"Environment temperature: {environmentTemperature:F1}C. Heatflow: {hf}.");
            energy += (environmentTemperature - cellTemperature) * hf;
            heatFlow += hf;
        }

        #endregion DIFFUSION

        #region THERMAL PROPERTIES

        public static ThermalProps GetThermalProperties(this IntVec3 cell, Map map)
        {
            if (cell.InBounds(map))
            {
                List<Thing> thingsList = map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0; i < thingsList.Count; i++)
                    if (CompThermal.ShouldApplyTo(thingsList[i].def))
                    {
                        ThermalProps thermalProps = thingsList[i].TryGetComp<CompThermal>()?.ThermalProperties;
                        if (thermalProps != null)
                            return thermalProps;
                    }
            }
            return ThermalProps.Air;
        }

        public static float GetHeatCapacity(this IntVec3 cell, Map map) => cell.GetThermalProperties(map).heatCapacity;

        public static ThingDef GetUnderlyingStuff(this Thing thing) => thing.Stuff ?? thing.def.defaultStuff;

        public static float GetIsolationWithAirflow(float isolation, float airflow) => Mathf.Lerp(isolation, 1, airflow);

        #endregion THERMAL PROPERTIES

        #region TERRAIN

        public static bool HasTerrainTemperature(this IntVec3 cell, Map map) => cell.GetTerrain(map).HasModExtension<ThingThermalProperties>();

        public static float FreezingPoint(this TerrainDef water)
        {
            switch (water.defName)
            {
                case "WaterOceanDeep":
                case "WaterOceanShallow":
                    return -2;

                case "WaterMovingChestDeep":
                    return -3;

                case "WaterMovingShallow":
                    return -2;
            }
            return 0;
        }

        #endregion TERRAIN

        #region HEAT PUSH AND SNOW MELTING

        public static bool TryPushHeat(IntVec3 cell, Map map, float energy)
        {
            if (Prefs.DevMode && UI.MouseCell() == cell)
                LogUtility.Log($"Pushing {energy} heat at {cell}.");
            TemperatureInfo temperatureInfo = map.TemperatureInfo();
            if (temperatureInfo == null)
            {
                LogUtility.Log($"TemperatureInfo for {map} unavailable!", LogLevel.Warning);
                return false;
            }
            temperatureInfo.SetTemperatureForCell(cell, temperatureInfo.GetTemperatureForCell(cell) + energy * GenTicks.TicksPerRealSecond * Settings.HeatPushEffect / cell.GetHeatCapacity(map));
            return true;
        }

        public static float SnowMeltAmountAt(float temperature) => temperature * Mathf.Lerp(0, 0.0058f, temperature / 10);

        #endregion HEAT PUSH AND SNOW MELTING
    }
}
