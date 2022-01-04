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
        float[,] temperatures;

        public TemperatureInfo(Map map)
            :base(map)
        { }

        public override void FinalizeInit()
        {
            temperatures = new float[map.Size.x, map.Size.z];
            for (int i = 0; i < temperatures.GetLength(0); i++)
                for (int j = 0; j < temperatures.GetLength(1); j++)
                    temperatures[i, j] = GenTemperature.GetTemperatureForCell(new IntVec3(i, 0, j), map);
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
            Widgets.Label(new Rect(UI.MousePositionOnUIInverted, new Vector2(200, 40)), GetTemperatureForCell(cell).ToStringTemperature());
        }

        public float GetTemperatureForCell(IntVec3 cell)
        {
            if (cell.InBounds(map))
                return temperatures[cell.x, cell.z];
            return 21;
        }

        public void SetTemperatureForCell(IntVec3 cell, float temperature)
        {
            if (cell.InBounds(map))
                temperatures[cell.x, cell.z] = temperature;
        }
    }
}
