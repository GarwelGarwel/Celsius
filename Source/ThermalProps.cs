using UnityEngine;

namespace Celsius
{
    public class ThermalProps
    {
        public static readonly ThermalProps Empty = new ThermalProps();

        public static readonly ThermalProps Air = new ThermalProps()
        {
            heatCapacity = 1,
            isolation = 1,
            airflow = 1
        };

        public float heatCapacity;
        public float isolation = 1;
        public float airflow;

        public float Conductivity => Mathf.Pow(Settings.ConductivityPowerBase, isolation);

        public float HeatFlow => heatCapacity * Conductivity;

        public bool IsAir => airflow == 1;

        public ThermalProps()
        { }

        public override string ToString() => $"Heat capacity: {heatCapacity}. Isolation: {isolation}. Conductivity: {Conductivity:P1}. Airflow: {airflow:P0}.";

        public bool Equals(ThermalProps props) => props.heatCapacity == heatCapacity && props.isolation == isolation && props.airflow == airflow;

        public override int GetHashCode() => (heatCapacity, isolation, airflow).GetHashCode();
    }
}
