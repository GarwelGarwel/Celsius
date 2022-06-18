using Verse;

namespace Celsius
{
    public class StuffThermalProperties : DefModExtension
    {
        public float volumetricHeatCapacity = 3000000;
        public float conductivity = 1;

        public override string ToString() => $"Volumetric heat capacity: {volumetricHeatCapacity} J/kg/C. Conductivity: {conductivity:F1} W/C.";
    }
}
