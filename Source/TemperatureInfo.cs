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
        static float heatTransferSpeed = 0.5f;

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
            IntVec3 cell = UI.MouseCell();
            if (!cell.InBounds(map))
                return;
            Widgets.Label(new Rect(UI.MousePositionOnUIInverted.x + 20, UI.MousePositionOnUIInverted.y + 20, 100, 40), GetTemperatureForCell(cell).ToStringTemperature());
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 120 != 7)
                return;

            LogUtility.Log($"Updating temperatures for {map} on tick {Find.TickManager.TicksGame}.");
            float[,] newTemperatures = (float[,])temperatures.Clone();
            for (int i = 0; i < map.Size.x; i++)
                for (int j = 0; j < map.Size.z; j++)
                {
                    IntVec3 cell = new IntVec3(i, 0, j);
                    newTemperatures[i, j] = Mathf.Lerp(GetTemperatureForCell(cell), cell.GetAverageAdjacentTemperatures(map), heatTransferSpeed);
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
