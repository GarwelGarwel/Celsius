using Verse;

namespace Celsius
{
    public class StuffThermalProperties : DefModExtension
    {
        public float volumetricHeatCapacity = 1000;
        //public float conductivity = 1;
        public float isolation = 1;

        public override string ToString() => $"Volumetric heat capacity: {volumetricHeatCapacity}. Isolation: {isolation:P1}.";
    }
}
