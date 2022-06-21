using Verse;

namespace Celsius
{
    public class ThingThermalProperties : DefModExtension
    {
        public static readonly ThingThermalProperties Empty = new ThingThermalProperties() { };

        public static readonly ThingThermalProperties Air = new ThingThermalProperties()
        {
            heatCapacity = Settings.AirHeatCapacity,
            conductivity = 0.17f
        };

        public float heatCapacity;
        public float volume;
        public float conductivity = 1;
        public float conductivityWhenOpen = 1;

        public ThingThermalProperties()
        { }

        public ThingThermalProperties(ThingThermalProperties copyFrom, bool open = false)
        {
            if (copyFrom == null)
                return;
            heatCapacity = copyFrom.heatCapacity;
            volume = copyFrom.volume;
            conductivity = open ? copyFrom.conductivityWhenOpen : copyFrom.conductivity;
        }

        public override string ToString() => $"Heat capacity: {heatCapacity} J/C. Volume: {volume} m^3. Conductivity: {conductivity} W/C ({conductivityWhenOpen} when open).";

        public override bool Equals(object obj) =>
            obj is ThingThermalProperties props
            && props.heatCapacity == heatCapacity
            && props.volume == volume
            && props.conductivity == conductivity
            && props.conductivityWhenOpen == conductivityWhenOpen;

        public override int GetHashCode() => (heatCapacity, volume, conductivity, conductivityWhenOpen).GetHashCode();
    }
}
