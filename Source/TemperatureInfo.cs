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
        const float heatPushEffect = ticksPerUpdate / 2;
        const float outdoorEffect = 3;

        float[,] temperatures;

        public TemperatureInfo(Map map)
            :base(map)
        { }

        public override void FinalizeInit()
        {
            temperatures = new float[map.Size.x, map.Size.z];
            for (int i = 0; i < temperatures.GetLength(0); i++)
                for (int j = 0; j < temperatures.GetLength(1); j++)
                    temperatures[i, j] = new IntVec3(i, 0, j).GetTemperature(map);
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
            if (heatPusher == null)
                return 0;
            float temp = GetTemperatureForCell(cell);
            if (temp > heatPusher.heatPushMinTemperature + 20 && temp < heatPusher.heatPushMaxTemperature - 20)
                return heatPusher.heatPerSecond;
            return 0;
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
                    float weightedAvg = GetWeightedAverageTemperatureAroundCell(cell);
                    newTemperatures[i, j] = Mathf.Lerp(weightedAvg, GetTemperatureForCell(cell), lerpFactor);
                    float outdoorLerpFactor = Mathf.Pow(lerpFactor, outdoorEffect);
                    if (!cell.Roofed(map))
                        newTemperatures[i, j] = Mathf.Lerp(map.mapTemperature.OutdoorTemp, newTemperatures[i, j], outdoorLerpFactor);
                    float heatPush = HeatPushFromCell(cell);
                    newTemperatures[i, j] += heatPush * heatPushEffect / cell.GetHeatCapacity(map);
                    if (Prefs.DevMode && cell == UI.MouseCell())
                        LogUtility.Log($"{CellInfo(cell)} Weighted average temp: {weightedAvg:F1}. Lerp factor: {lerpFactor}. Diff with outside temp: {newTemperatures[i, j] - map.mapTemperature.OutdoorTemp:F1}. Heat push: {heatPush}.\nNeighbours:\n{cell.AdjacentCells().Select(c => CellInfo(c)).ToLineList("- ")}");
                }

            temperatures = (float[,])newTemperatures.Clone();
        }

        public float GetTemperatureForCell(IntVec3 cell)
        {
            if (cell.InBounds(map))
                return temperatures[cell.x, cell.z];
            return map.mapTemperature.OutdoorTemp;
        }

        public void SetTemperatureForCell(IntVec3 cell, float temperature)
        {
            if (cell.InBounds(map))
                temperatures[cell.x, cell.z] = temperature;
        }
    }
}
