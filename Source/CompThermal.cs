using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TemperaturesPlus
{
    public class CompThermal : ThingComp
    {
        public float temperature;

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Item;

        public override string CompInspectStringExtra() =>
            $"Temperature: {temperature.ToStringTemperature()}\nHeat capacity: {parent.GetStatValue(DefOf.HeatCapacity)}";

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (!respawningAfterLoad)
                temperature = parent.Position.GetTemperatureForCell(parent.Map);
        }

        public override void PreAbsorbStack(Thing otherStack, int count) =>
            temperature = GenMath.WeightedAverage(temperature, parent.stackCount, otherStack.TryGetComp<CompThermal>().temperature, count);
    }
}
