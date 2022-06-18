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
        public float volume;

        public ThingThermalProperties()
        { }

        public ThingThermalProperties(ThingThermalProperties copyFrom)
        {
            if (copyFrom == null)
                return;
            heatCapacity = copyFrom.heatCapacity;
            conductivity = copyFrom.conductivity;
            volume = copyFrom.volume;
        }

        public override string ToString() => $"Heat capacity: {heatCapacity} J/C. Conductivity: {conductivity} W/C. Volume: {volume} m^3.";

        public override bool Equals(object obj) =>
            obj is ThingThermalProperties props && props.heatCapacity == heatCapacity && props.conductivity == conductivity && props.volume == volume;

        public override int GetHashCode() => (heatCapacity, conductivity, volume).GetHashCode();
    }
}
