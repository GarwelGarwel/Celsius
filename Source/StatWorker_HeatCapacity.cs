using RimWorld;
using Verse;

namespace TemperaturesPlus
{
    public class StatWorker_HeatCapacity : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            Thing thing = req.Thing as ThingWithComps;
            if (thing == null || thing.def.category != ThingCategory.Item)
                return 0;
            return thing.GetStatValue(StatDefOf.Mass) * thing.GetSpecificHeatCapacity() * thing.stackCount; // Replace with more accurate specific heat capacity
        }

        public override bool IsDisabledFor(Thing thing) =>
            !(thing is ThingWithComps) || thing.def.category != ThingCategory.Item;
    }
}
