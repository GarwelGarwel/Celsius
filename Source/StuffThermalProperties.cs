using Verse;

namespace Celsius
{
    public class StuffThermalProperties : DefModExtension
    {
        public float specificHeatCapacity;
        public float conductivity = 1;

        public override string ToString() => $"Specific heat capacity: {specificHeatCapacity} J/kg/C. Conductivity: {conductivity:F1} J/C/s.";
    }
}
