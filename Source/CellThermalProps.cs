using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            conductivity = 0.2f
        };

        public float heatCapacity;
        public float airflow;
        public float conductivity = 1;

        public CellThermalProps()
        { }

        public static CellThermalProps Create(ThingThermalProperties thingThermalProperties)
        {
            if (thingThermalProperties == null)
                return Empty;
            return new CellThermalProps()
            {
                heatCapacity = thingThermalProperties.heatCapacity,
                airflow = thingThermalProperties.airflow,
                conductivity = thingThermalProperties.conductivity
            };
        }

        public static CellThermalProps Create(ThingThermalProperties thingThermalProperties, bool open)
        {
            if (thingThermalProperties == null)
                return Empty;
            float airflow = open ? thingThermalProperties.airflowWhenOpen : thingThermalProperties.airflow;
            return new CellThermalProps()
            {
                heatCapacity = thingThermalProperties.heatCapacity,
                airflow = airflow,
                conductivity = GenMath.WeightedAverage(Air.conductivity, airflow, thingThermalProperties.conductivity, 1 - airflow)
            };
        }

        public static CellThermalProps Create(ThingThermalProperties thingThermalProperties, StuffThermalProperties stuffProps, bool open)
        {
            if (stuffProps == null)
                return Create(thingThermalProperties, open);
            if (thingThermalProperties == null)
                return Empty;
            float airflow = open ? thingThermalProperties.airflowWhenOpen : thingThermalProperties.airflow;
            return new CellThermalProps()
            {
                heatCapacity = stuffProps.volumetricHeatCapacity * thingThermalProperties.volume + Settings.AirHeatCapacity * (1 - thingThermalProperties.volume / 1000),
                airflow = airflow,
                conductivity = GenMath.WeightedAverage(Air.conductivity, airflow, thingThermalProperties.conductivity * stuffProps.conductivity, 1 - airflow)
            };
        }

        public override string ToString() => $"Heat capacity: {heatCapacity} J/C. Conductivity: {conductivity} W/C. Airflow: {airflow:P0}.";

        public override bool Equals(object obj) =>
            obj is ThingThermalProperties props
            && props.heatCapacity == heatCapacity
            && props.airflow == airflow
            && props.conductivity == conductivity;

        public override int GetHashCode() => (heatCapacity, airflow, conductivity).GetHashCode();
    }
}
