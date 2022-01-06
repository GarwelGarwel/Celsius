using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace TemperaturesPlus
{
    public class TemperatureInfo : MapComponent
    {
        const int ticksPerUpdate = 250;
        const int secondsPerUpdate = 3600 * ticksPerUpdate / 2500;
        const float convectionEffect = 1;
        const float heatPushEffect = ticksPerUpdate * 10;
        const float defaultTempEffect = 3;

        float[,] temperatures;

        public TemperatureInfo(Map map)
            :base(map)
        { }

        public override void FinalizeInit()
        {
            temperatures = new float[map.Size.x, map.Size.z];
            for (int i = 0; i < temperatures.GetLength(0); i++)
                for (int j = 0; j < temperatures.GetLength(1); j++)
                {
                    IntVec3 cell = new IntVec3(i, 0, j);
                    Room room = cell.GetRoom(map);
                    if (room != null)
                        temperatures[i, j] = room.TempTracker.Temperature;
                    else TryGetDefaultTemperatureForCell(cell, out temperatures[i, j]);
                }
            LogUtility.Log($"TemperatureInfo initialized for {map}.");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref temperatures, "temperatures");
        }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            if (!Prefs.DevMode)
                return;
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map))
                return;
            Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), $"Temp: {GetTemperatureForCell(cell).ToStringTemperature()}\nAvg: {GetWeightedAverageTemperatureAroundCell(cell).ToStringTemperature()}");
        }

        float GetWeightedAverageTemperatureAroundCell(IntVec3 cell) =>
            cell.AdjacentCells().AverageWeighted(c => c.GetHeatCapacity(map), c => GetTemperatureForCell(c));

        string CellInfo(IntVec3 cell) =>
            $"Cell {cell}. Temperature: {GetTemperatureForCell(cell):F1}. Material: {cell.GetMaterialType(map)}. Heat capacity: {cell.GetHeatCapacity(map)}.";

        float HeatPushFromCell(IntVec3 cell)
        {
            CompProperties_HeatPusher heatPusher = cell.GetFirstBuilding(map)?.GetComp<CompHeatPusher>()?.Props;
            return heatPusher != null ? heatPusher.heatPerSecond : 0;
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % ticksPerUpdate != 0)
                return;

            LogUtility.Log($"Updating temperatures for {map} on tick {Find.TickManager.TicksGame}.");
            float[,] newTemperatures = (float[,])temperatures.Clone();
            for (int i = 0; i < map.Size.x; i++)
                for (int j = 0; j < map.Size.z; j++)
                {
                    IntVec3 cell = new IntVec3(i, 0, j);
                    float lerpFactor = Mathf.Exp(-secondsPerUpdate * cell.GetHeatConductivity(map) / cell.GetHeatCapacity(map));

                    // Diffusion
                    float weightedAvg = GetWeightedAverageTemperatureAroundCell(cell);
                    newTemperatures[i, j] = Mathf.Lerp(weightedAvg, GetTemperatureForCell(cell), lerpFactor);
                    if (Prefs.DevMode && cell == UI.MouseCell())
                        LogUtility.Log($"{CellInfo(cell)} Weighted average temp: {weightedAvg:F1}. Diffusion lerp factor: {lerpFactor}.\nNeighbours:\n{cell.AdjacentCells().Select(c => CellInfo(c)).ToLineList("- ")}");

                    // Convection (for air only)
                    if (cell.GetMaterialType(map) == CellMaterialType.Air)
                    {
                        float nearbyAirTemp = 0;
                        int airCells = 0;
                        foreach (IntVec3 c in cell.AdjacentCells().Where(c => c.GetMaterialType(map) == CellMaterialType.Air))
                        {
                            nearbyAirTemp += GetTemperatureForCell(c);
                            airCells++;
                        }
                        if (airCells > 0)
                        {
                            newTemperatures[i, j] = Mathf.Lerp(nearbyAirTemp / airCells, newTemperatures[i, j], Mathf.Pow(lerpFactor, airCells * convectionEffect));
                            if (Prefs.DevMode && cell == UI.MouseCell())
                                LogUtility.Log($"Nearby air cells: {airCells}. Average air temp: {nearbyAirTemp / airCells:F1}. Convection lerp factor: {Mathf.Pow(lerpFactor, airCells * convectionEffect)}");
                        }
                    }

                    // Heat push or pull
                    if (TryGetDefaultTemperatureForCell(cell, out float defaultTemperature))
                        newTemperatures[i, j] = Mathf.Lerp(defaultTemperature, newTemperatures[i, j], Mathf.Pow(lerpFactor, defaultTempEffect));
                    float heatPush = HeatPushFromCell(cell);
                    newTemperatures[i, j] += heatPush * heatPushEffect / cell.GetHeatCapacity(map);
                    if (heatPush != 0 && Prefs.DevMode && cell == UI.MouseCell())
                        LogUtility.Log($"Heat push: {heatPush}.");
                }

            temperatures = (float[,])newTemperatures.Clone();
        }

        public bool TryGetDefaultTemperatureForCell(IntVec3 cell, out float temperature)
        {
            if (cell.GetFirstMineable(map) != null)
            {
                temperature = TemperatureTuning.DeepUndergroundTemperature;
                return true;
            }
            temperature = map.mapTemperature.OutdoorTemp;
            return !cell.InBounds(map) || (!cell.Roofed(map) && cell.GetMaterialType(map) == CellMaterialType.Air);
        }

        public float GetTemperatureForCell(IntVec3 cell) =>
            cell.InBounds(map) && temperatures != null ? temperatures[cell.x, cell.z] : map.mapTemperature.OutdoorTemp;
    }
}
