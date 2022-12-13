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
            return thermalProps == null ? 0 : thermalProps.Conductivity;
        }

        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
        {
            string str = base.GetExplanationUnfinalized(req, numberSense);
            CompThermal compThermal = req.Thing?.TryGetComp<CompThermal>();
            if (compThermal == null)
                return str;
            ThingThermalProperties thingThermalProperties = compThermal.ThingThermalProperties;
            if (thingThermalProperties == null)
                return str;
            str += $"\n{req.Thing.def.LabelCap} insulation: {thingThermalProperties.insulation}";
            StuffThermalProperties stuffThermalProperties = compThermal.StuffThermalProperties;
            if (stuffThermalProperties != null)
                str += $"\nStuff insulation: x{stuffThermalProperties.insulationFactor.ToStringPercent()}";
            if (thingThermalProperties.airflow != thingThermalProperties.airflowWhenOpen && compThermal.IsOpen)
                str += $"\nThe {req.Thing.LabelNoCount} is open.".Colorize(Color.yellow);
            ThermalProps thermalProps = compThermal.ThermalProperties;
            if (thermalProps != null)
            {
                if (thermalProps.airflow != 0)
                {
                    str += $"\nAirflow: {thermalProps.airflow.ToStringPercent()}";
                    str += $"\nActual insulation: {thermalProps.insulation}";
                }
                str += $"\nConductivity: {Settings.ConductivityPowerBase} ^ {thermalProps.insulation} = {thermalProps.Conductivity.ToStringPercent()}";
            }
            return str;
        }
    }
}
