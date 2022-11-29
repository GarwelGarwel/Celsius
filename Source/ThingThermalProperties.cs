using UnityEngine;
using Verse;

namespace Celsius
{
    public class ThingThermalProperties : DefModExtension
    {
        public float heatCapacity;
        public float volume;
        //public float conductivity = 1;
        public float isolation = 1;
        public float airflow;
        public float airflowWhenOpen;

        ThermalProps defaultProps;

        public ThingThermalProperties()
        { }

        public float Conductivity => Mathf.Pow(0.5f, isolation);

        public ThermalProps GetCellThermalProps()
        {
            if (defaultProps == null)
                defaultProps = new ThermalProps()
                {
                    heatCapacity = heatCapacity,
                    isolation = isolation
                    //airflow = airflow,
                    //conductivity = conductivity * Settings.HeatConductivityFactor
                };
            return defaultProps;
        }

        public ThermalProps GetCellThermalProps(StuffThermalProperties stuffProps, bool open)
        {
            if (stuffProps == null)
            {
                if (heatCapacity <= 0)
                    return null;
                if (!open)
                    return GetCellThermalProps();
                return new ThermalProps()
                {
                    heatCapacity = heatCapacity,
                    isolation = GenMath.WeightedAverage(1, airflowWhenOpen, isolation, 1 - airflowWhenOpen)
                    //airflow = airflowWhenOpen,
                    //conductivity = GenMath.WeightedAverage(ThermalProps.Air.conductivity, airflowWhenOpen, conductivity * Settings.HeatConductivityFactor, 1 - airflowWhenOpen)
                };
            }

            float airflow = open ? airflowWhenOpen : this.airflow;
            return new ThermalProps()
            {
                heatCapacity = stuffProps.volumetricHeatCapacity * volume + Settings.AirHeatCapacity * (1 - volume / 1000),
                isolation = GenMath.WeightedAverage(1, airflowWhenOpen, isolation * stuffProps.isolation, 1 - airflowWhenOpen)
                //airflow = airflow,
                //conductivity = GenMath.WeightedAverage(ThermalProps.Air.conductivity, airflow, conductivity * stuffProps.conductivity * Settings.HeatConductivityFactor, 1 - airflow)
            };
        }

        public void Reset() => defaultProps = null;

        public override string ToString() => $"Heat capacity: {heatCapacity}. Volume: {volume} m^3. Isolation: {isolation}. Conductivity: {Conductivity:P1}. Airflow: {airflow:P0} ({airflowWhenOpen:P0} when open).";
    }
}
