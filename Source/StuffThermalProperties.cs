using Verse;

namespace Celsius
{
    public class StuffThermalProperties : DefModExtension
    {
        public float volumetricHeatCapacity = 1000;
        public float insulationFactor = 1;

        public override string ToString() => $"Volumetric heat capacity: {volumetricHeatCapacity}/m3. Insulation: {insulationFactor:P1}.";
    }
}
