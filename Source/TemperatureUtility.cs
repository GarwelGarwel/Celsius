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

        internal static Dictionary<Map, TemperatureInfo> temperatureInfos = new Dictionary<Map, TemperatureInfo>();

        public static TemperatureInfo TemperatureInfo(this Map map)
        {
            if (temperatureInfos.TryGetValue(map, out TemperatureInfo temperatureInfo))
                return temperatureInfo;
            temperatureInfo = map.GetComponent<TemperatureInfo>();
            if (temperatureInfo != null)
                temperatureInfos.Add(map, temperatureInfo);
            return temperatureInfo;
        }

        internal static void SettingsChanged()
        {
            FloatRange vanillaAutoIgnitionTemperatureRange = Settings.AutoignitionEnabled ? new FloatRange(10000, float.MaxValue) : new FloatRange(240, 1000);
            AccessTools.Field(typeof(SteadyEnvironmentEffects), "AutoIgnitionTemperatureRange").SetValue(null, vanillaAutoIgnitionTemperatureRange);

            // Resetting thermal props for all ThingDefs & Things
            for (int i = 0; i < DefDatabase<ThingDef>.AllDefsListForReading.Count; i++)
                DefDatabase<ThingDef>.AllDefsListForReading[i].GetModExtension<ThingThermalProperties>()?.Reset();
            if (Find.Maps != null)
                for (int m = 0; m < Find.Maps.Count; m++)
                    Find.Maps[m]?.TemperatureInfo()?.ResetAllThings();
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
            foreach (IntVec3 c in GenAdjFast.AdjacentCellsCardinal(cell))
                sum += c.InBounds(map) ? c.GetTemperatureForCell(map) : cell.GetTemperatureForCell(map);
            return sum / 5;
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

        public static bool HasTemperature(this TerrainDef terrain)
        {
            ThermalProps terrainProps = terrain?.GetModExtension<ThingThermalProperties>()?.GetThermalProps();
            return terrainProps != null && terrainProps.heatCapacity > 0;
        }

        #endregion TEMPERATURE

        #region DIFFUSION

        public static void CalculateHeatTransfer(float homeTemperature, float interactingTemperature, ThermalProps props, float airflow, ref float energy, ref float heatFlow, bool log = false)
        {
            // Air has heat capacity = 1 and conductivity = 1
            if (airflow == 1 && props.IsAir)
            {
                energy += interactingTemperature - homeTemperature;
                heatFlow++;
                return;
            }

            // If one of the interacting cells is not air, need to take airflow into account
            float hf = airflow == 0 || props.airflow == 0
                ? props.HeatFlowNoConvection
                : props.HeatFlow * Mathf.Pow(Settings.ConvectionConductivityEffect, airflow * props.airflow - 1);
            if (log)
                LogUtility.Log($"Interacting temperature: {interactingTemperature:F1}C. Mutual airflow: {airflow * props.airflow}. Heatflow: {hf}.");
            energy += (interactingTemperature - homeTemperature) * hf;
            heatFlow += hf;
        }

        public static void CalculateHeatTransferTerrain(float cellTemperature, float terrainTemperature, ThermalProps props, ref float energy, ref float heatFlow)
        {
            energy += (terrainTemperature - cellTemperature) * props.HeatFlowNoConvection;
            heatFlow += props.HeatFlowNoConvection;
        }

        public static void CalculateHeatTransferEnvironment(float cellTemperature, float environmentTemperature, ThermalProps props, ref float energy, ref float heatFlow, bool log = false)
        {
            if (props.IsAir)
            {
                energy += (environmentTemperature - cellTemperature) * Settings.EnvironmentDiffusionFactor;
                heatFlow += Settings.EnvironmentDiffusionFactor;
            }
            float hf = props.HeatFlow * Settings.EnvironmentDiffusionFactor / Settings.ConvectionConductivityEffect;
            energy += (environmentTemperature - cellTemperature) * hf;
            heatFlow += hf;
        }

        #endregion DIFFUSION

        #region THERMAL PROPERTIES

        public static ThingDef GetUnderlyingStuff(this Thing thing) => thing.Stuff ?? thing.def.defaultStuff;

        public static float GetInsulationWithAirflow(float insulation, float airflow) => Mathf.Lerp(insulation, 1, airflow);

        #endregion THERMAL PROPERTIES

        #region HEAT PUSH AND SNOW MELTING

        public static bool TryPushHeat(IntVec3 cell, Map map, float energy)
        {
            if (Prefs.DevMode && Settings.DebugMode && Find.PlaySettings.showTemperatureOverlay && UI.MouseCell() == cell)
                LogUtility.Log($"Pushing {energy} heat at {cell}.");
            TemperatureInfo temperatureInfo = map.TemperatureInfo();
            if (temperatureInfo == null || !cell.InBounds(map))
            {
                LogUtility.Log($"TemperatureInfo for {map} unavailable or cell {cell} is outside map boundaries!", LogLevel.Warning);
                return false;
            }
            int index = map.cellIndices.CellToIndex(cell);
            temperatureInfo.SetTemperatureForCell(index, temperatureInfo.GetTemperatureForCell(index) + energy * Settings.HeatPushEffect / temperatureInfo.GetThermalPropertiesAt(index).heatCapacity);
            return true;
        }

        public static float SnowMeltAmountAt(float temperature) => temperature * Mathf.Lerp(0, 0.0058f, temperature / 10);

        #endregion HEAT PUSH AND SNOW MELTING
    }
}
