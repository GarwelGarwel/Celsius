using Verse;

namespace Celsius
{
    public class ThingThermalProperties : DefModExtension
    {
        public static readonly ThingThermalProperties Empty = new ThingThermalProperties();

        public static readonly ThingThermalProperties Air = new ThingThermalProperties()
        {
            heatCapacity = Settings.AirHeatCapacity,
            conductivity = 0.03f
        };

        public float heatCapacity;
        public float conductivity = 1;

        public ThingThermalProperties()
        { }

        public ThingThermalProperties(ThingThermalProperties copyFrom)
        {
            if (copyFrom == null)
                return;
            heatCapacity = copyFrom.heatCapacity;
            conductivity = copyFrom.conductivity;
        }

        public override string ToString() => $"Heat capacity: {heatCapacity} J/C. Conductivity: {conductivity} W/C.";
    }
}
