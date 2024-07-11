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

        public static void SettingsChanged()
        {
            LogUtility.Log("Mod settings have changed. Updating data.", LogLevel.Important);
            Settings.RecalculateValues();
            ThermalProps.Init();
            AccessTools.Field(typeof(SteadyEnvironmentEffects), "AutoIgnitionTemperatureRange").SetValue(null, Settings.AutoignitionEnabled
                ? new FloatRange(10000, float.MaxValue)
                : new FloatRange(240, 1000));

            // Resetting thermal props for all ThingDefs & Things
            for (int i = 0; i < DefDatabase<ThingDef>.AllDefsListForReading.Count; i++)
                DefDatabase<ThingDef>.AllDefsListForReading[i].GetModExtension<ThingThermalProperties>()?.Reset();

            // Resetting maps' temperature info cache and trying to re-initialize terrain temperatures
            if (temperatureInfos != null)
                foreach (TemperatureInfo temperatureInfo in temperatureInfos.Values)
                {
                    temperatureInfo.ResetAllThings();
                    if (Settings.FreezingAndMeltingEnabled && !temperatureInfo.HasTerrainTemperatures)
                        temperatureInfo.InitializeTerrainTemperatures();
                }
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
            return temperatureInfo.GetRoomAverageTemperature(room);
        }

        public static bool HasTemperature(this TerrainDef terrain)
        {
            ThermalProps terrainProps = terrain?.GetModExtension<ThingThermalProperties>()?.GetThermalProps();
            return terrainProps != null && terrainProps.heatCapacity > 0;
        }

        #endregion TEMPERATURE

        #region DIFFUSION

        public static void CalculateHeatTransferCells(float interactingTemperature, ThermalProps props, float airflow, ref float energy, ref float heatFlow, bool log = false)
        {
            // Air has heat capacity = 1 and conductivity = 1
            if (airflow == 1 && props.IsAir)
            {
                heatFlow++;
                energy += interactingTemperature;
                return;
            }

            // If one of the interacting cells is not air, need to take airflow into account
            float hf = airflow == 0 || props.airflow == 0
                ? props.HeatFlow
                : props.HeatFlow * Mathf.Pow(Settings.ConvectionConductivityEffect, airflow * props.airflow);
            if (log)
                LogUtility.Log($"Interacting temperature: {interactingTemperature:F1}C. Mutual airflow: {airflow * props.airflow}. Heatflow: {hf:F3}.");
            heatFlow += hf;
            energy += interactingTemperature * hf;
        }

        public static void CalculateHeatTransferTerrain(float terrainTemperature, ThermalProps props, ref float energy, ref float heatFlow)
        {
            heatFlow += props.HeatFlow;
            energy += terrainTemperature * props.HeatFlow;
        }

        public static void CalculateHeatTransferEnvironment(float environmentTemperature, ThermalProps props, RoofDef roof, ref float energy, ref float heatFlow)
        {
            float hf;
            if (roof == null)
            {
                if (props.IsAir)  // Air-to-air exchange (most common way on most maps)
                {
                    heatFlow += Settings.EnvironmentDiffusionFactor;
                    energy += environmentTemperature * Settings.EnvironmentDiffusionFactor;
                    return;
                }
                // Buildings without a roof
                hf = Settings.EnvironmentDiffusionFactor * props.HeatFlow;
                if (props.airflow != 0)
                    hf *= Mathf.Pow(Settings.ConvectionConductivityEffect, props.airflow);
            }
            else if (!roof.isThickRoof)  // Thin roof: use insulation value from the settings (precomputed)
                hf = props.HeatFlow * Settings.RoofDiffusionFactor;
            else return;  // Thick roof: no heat exchange at all
            heatFlow += hf;
            energy += environmentTemperature * hf;
        }

        #endregion DIFFUSION

        #region MISC UTILITIES

        public static float GetInsulationWithAirflow(float insulation, float airflow) => Mathf.Lerp(insulation, 1, airflow);

        public static bool TryPushHeat(IntVec3 cell, Map map, float energy)
        {
            TemperatureInfo temperatureInfo = map.TemperatureInfo();
            if (temperatureInfo == null || !cell.InBounds(map))
            {
                LogUtility.Log($"TemperatureInfo for {map} unavailable or cell {cell} is outside map boundaries!", LogLevel.Warning);
                return false;
            }
            temperatureInfo.PushHeat(map.cellIndices.CellToIndex(cell), energy);
            return true;
        }

        #endregion MISC UTILITIES
    }
}
