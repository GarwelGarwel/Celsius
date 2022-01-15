using RimWorld;
using Verse;

namespace TemperaturesPlus
{
    public class StatWorker_HeatCapacity : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            CompThermal compThermal = req.Thing?.TryGetComp<CompThermal>();
            return compThermal != null ? compThermal.ThermalProperties.heatCapacity : 0;
        }
    }
}
