using Verse;

namespace Celsius
{
    public class CellThermalProps
    {
        public static readonly CellThermalProps Empty = new CellThermalProps();

        public static readonly CellThermalProps Air = new CellThermalProps()
        {
            heatCapacity = Settings.AirHeatCapacity,
            airflow = 1,
            conductivity = 0.2f * Settings.HeatConductivityFactor
        };

        public float heatCapacity;
        public float airflow;
        public float conductivity = 1;

        public CellThermalProps()
        { }

        public override string ToString() => $"Heat capacity: {heatCapacity} J/C. Conductivity: {conductivity} W/C. Airflow: {airflow:P0}.";

        public bool Equals(CellThermalProps props) => props.heatCapacity == heatCapacity && props.airflow == airflow && props.conductivity == conductivity;

        public override int GetHashCode() => (heatCapacity, airflow, conductivity).GetHashCode();
    }
}
