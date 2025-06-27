using UnityEngine;

namespace Celsius
{
    public class ThermalProps
    {
        static ThermalProps air;
        public static ThermalProps Air => air;

        static ThermalProps() => Init();

        internal static void Init() => air = new ThermalProps(1, 1, 1, true);

        public float heatCapacity;
        public float insulation;  // Effective insulation (taking into cosideration airflow)
        public float airflow;
        public bool isAir;

        public float conductivity;
        public float heatflow;

        public float HeatFlow => heatflow;

        public ThermalProps(float heatCapacity, float insulation, float airflow, bool isAir = false)
        {
            this.heatCapacity = heatCapacity;
            this.insulation = TemperatureUtility.GetInsulationWithAirflow(insulation, airflow);
            this.airflow = airflow;
            conductivity = Mathf.Pow(Settings.ConductivityPowerBase, insulation);
            heatflow = heatCapacity * conductivity / Settings.ConvectionConductivityEffect;
            this.isAir = isAir;
        }

        public override string ToString() => $"Heat capacity: {heatCapacity}. Insulation: {insulation}. Conductivity: {conductivity:P1}. Heat flow: {HeatFlow:F3}.";
    }
}
