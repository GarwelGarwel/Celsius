using UnityEngine;
using Verse;

namespace Celsius
{
    public class ThermalProps
    {
        public static readonly ThermalProps Empty = new ThermalProps();

        public static readonly ThermalProps Air = new ThermalProps()
        {
            heatCapacity = 1,
            //airflow = 1,
            isolation = 1
        };

        public float heatCapacity;
        //public float airflow;
        public float isolation = 1;

        public float Conductivity => Mathf.Pow(0.5f, isolation);

        public bool IsAir => this == Air;

        public ThermalProps()
        { }

        public override string ToString() => $"Heat capacity: {heatCapacity}. Isolation: {isolation}. Conductivity: {Conductivity:P1}.";

        public bool Equals(ThermalProps props) => props.heatCapacity == heatCapacity && props.isolation == isolation;

        public override int GetHashCode() => (heatCapacity, isolation).GetHashCode();
    }
}
