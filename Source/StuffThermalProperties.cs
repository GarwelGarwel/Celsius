﻿using Verse;

namespace TemperaturesPlus
{
    public class StuffThermalProperties : DefModExtension
    {
        public float specificHeatCapacity;
        public float conductivity = 1;

        public override string ToString() => $"Specific heat capacity: {specificHeatCapacity} J/kg/C. Conductivity: {conductivity:F1} J/C.";
    }
}