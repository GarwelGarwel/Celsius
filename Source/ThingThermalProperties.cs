using Verse;

namespace Celsius
{
    public class ThingThermalProperties : DefModExtension
    {
        public float heatCapacity;
        public float volume;
        public float conductivity = 1;
        public float airflow;
        public float airflowWhenOpen;

        CellThermalProps defaultProps;

        public ThingThermalProperties()
        { }

        public float Conductivity => conductivity / (conductivity + 1);

        public CellThermalProps GetCellThermalProps()
        {
            if (defaultProps == null)
                defaultProps = new CellThermalProps()
                {
                    heatCapacity = heatCapacity,
                    airflow = airflow,
                    conductivity = conductivity * Settings.HeatConductivityFactor
                };
            return defaultProps;
        }

        public CellThermalProps GetCellThermalProps(StuffThermalProperties stuffProps, bool open)
        {
            if (stuffProps == null)
            {
                if (heatCapacity <= 0)
                    return null;
                if (!open)
                    return GetCellThermalProps();
                return new CellThermalProps()
                {
                    heatCapacity = heatCapacity,
                    airflow = airflowWhenOpen,
                    conductivity = GenMath.WeightedAverage(CellThermalProps.Air.conductivity, airflowWhenOpen, conductivity * Settings.HeatConductivityFactor, 1 - airflowWhenOpen)
                };
            }

            float airflow = open ? airflowWhenOpen : this.airflow;
            return new CellThermalProps()
            {
                heatCapacity = stuffProps.volumetricHeatCapacity * volume + Settings.AirHeatCapacity * (1 - volume / 1000),
                airflow = airflow,
                conductivity = GenMath.WeightedAverage(CellThermalProps.Air.conductivity, airflow, conductivity * stuffProps.conductivity * Settings.HeatConductivityFactor, 1 - airflow)
            };
        }

        public void Reset() => defaultProps = null;

        public override string ToString() => $"Heat capacity: {heatCapacity} J/C. Volume: {volume} m^3. Conductivity: {conductivity} W/C. Airflow: {airflow:P0} ({airflowWhenOpen:P0} when open).";
    }
}
