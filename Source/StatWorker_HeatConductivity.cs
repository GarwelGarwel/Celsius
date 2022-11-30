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
            str += $"\n{req.Thing.def.LabelCap} isolation: {thingThermalProperties.isolation}";
            StuffThermalProperties stuffThermalProperties = compThermal.StuffThermalProperties;
            if (stuffThermalProperties != null)
                str += $"\nStuff isolation: x{stuffThermalProperties.isolation.ToStringPercent()}";
            if (thingThermalProperties.airflow > 0)
                str += $"\nAirflow: {thingThermalProperties.airflow.ToStringPercent()}";
            if (thingThermalProperties.airflowWhenOpen > 0)
                str += $"\nAirflow when open: {thingThermalProperties.airflowWhenOpen.ToStringPercent()}";
            if (compThermal.IsOpen)
                str += $"\nThe {req.Thing.LabelNoCount} is open".Colorize(Color.yellow);
            ThermalProps thermalProps = compThermal.ThermalProperties;
            if (thermalProps != null)
                str += $"\nConductivity: 2 ^ {-thermalProps.isolation} = {thermalProps.Conductivity.ToStringPercent()}";
            return str;
        }
    }
}
