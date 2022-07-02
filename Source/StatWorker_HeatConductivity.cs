using RimWorld;
using UnityEngine;
using Verse;

namespace Celsius
{
    public class StatWorker_HeatConductivity : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            CellThermalProps thermalProps = req.Thing?.TryGetComp<CompThermal>()?.ThermalProperties;
            if (thermalProps == null)
                return 0;
            float conductivity = thermalProps.conductivity * CellThermalProps.Air.conductivity;
            if (thermalProps.airflow > 0)
                conductivity *= Mathf.Pow(Settings.ConvectionConductivityEffect, thermalProps.airflow);
            return conductivity;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            string str = base.GetExplanationUnfinalized(req, numberSense);
            CellThermalProps thermalProps = req.Thing?.TryGetComp<CompThermal>()?.ThermalProperties;
            if (thermalProps == null)
                return str;
            str += $"\n{req.Thing.LabelCapNoCount} own conductivity: {thermalProps.conductivity.ToStringDecimalIfSmall()}";
            str += $"\nAir conductivity: x{CellThermalProps.Air.conductivity.ToStringDecimalIfSmall()}";
            if (thermalProps.airflow > 0)
                str += $"\nConvection (air flow {thermalProps.airflow.ToStringPercent()}): x{Mathf.Pow(Settings.ConvectionConductivityEffect, thermalProps.airflow).ToStringDecimalIfSmall()}";
            return str;
        }
    }
}
