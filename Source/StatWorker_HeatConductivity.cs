using RimWorld;
using UnityEngine;
using Verse;

namespace Celsius
{
    public class StatWorker_HeatConductivity : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            ThermalProps thermalProps = req.Thing?.TryGetComp<CompThermal>()?.ThermalProperties;
            if (thermalProps == null)
                return 0;
            float conductivity = thermalProps.Conductivity * ThermalProps.Air.Conductivity;
            //if (thermalProps.airflow > 0)
            //    conductivity *= Mathf.Pow(Settings.ConvectionConductivityEffect, thermalProps.airflow);
            return conductivity;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            string str = base.GetExplanationUnfinalized(req, numberSense);
            ThermalProps thermalProps = req.Thing?.TryGetComp<CompThermal>()?.ThermalProperties;
            if (thermalProps == null)
                return str;
            str += $"\n{req.Thing.LabelCapNoCount} isolation: {thermalProps.isolation.ToStringPercent()}";
            str += $"\n{req.Thing.LabelCapNoCount} own conductivity: {thermalProps.Conductivity.ToStringDecimalIfSmall()}";
            //str += $"\nAir conductivity: x{ThermalProps.Air.Conductivity.ToStringDecimalIfSmall()}";
            //if (thermalProps.airflow > 0)
            //    str += $"\nConvection (air flow {thermalProps.airflow.ToStringPercent()}): x{Mathf.Pow(Settings.ConvectionConductivityEffect, thermalProps.airflow).ToStringDecimalIfSmall()}";
            return str;
        }
    }
}
