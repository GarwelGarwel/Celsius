using RimWorld;
using Verse;

namespace Celsius
{
    public class StatWorker_HeatConductivity : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            CellThermalProps thermalProps = req.Thing?.TryGetComp<CompThermal>()?.ThermalProperties;
            return thermalProps != null ? thermalProps.conductivity : 0;
        }
    }
}
