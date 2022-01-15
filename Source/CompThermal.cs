using RimWorld;
using Verse;

namespace TemperaturesPlus
{
    public class CompThermal : ThingComp
    {
        public float temperature = -9999;
        ThingThermalProperties thermalProps;

        float Mass => parent.def.EverHaulable ? parent.GetStatValue(StatDefOf.Mass) : parent.def.CostStuffCount;

        public bool HasTemperature => ThermalProperties.heatCapacity > 0 && !ThermalProperties.replacesAirProperties;

        public ThingThermalProperties ThermalProperties
        {
            get
            {
                if (parent is Building_Door door && door.Open)
                    return ThingThermalProperties.Empty;

                if (parent is Building_Vent vent && vent.GetComp<CompFlickable>().SwitchIsOn)
                    return ThingThermalProperties.Empty;

                if (thermalProps != null)
                    return thermalProps;

                thermalProps = new ThingThermalProperties(parent.def.GetModExtension<ThingThermalProperties>());
                StuffThermalProperties stuffProps = parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>();
                if (stuffProps != null)
                {
                    if (thermalProps.mass == 0)
                        thermalProps.mass = Mass;
                    if (thermalProps.heatCapacity == 0)
                        thermalProps.heatCapacity = stuffProps.specificHeatCapacity * thermalProps.mass;
                    thermalProps.conductivity *= stuffProps.conductivity;
                }
                thermalProps.heatCapacity *= parent.stackCount;
                return thermalProps;
            }
        }

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Item || thingDef.category == ThingCategory.Building;

        public override string CompInspectStringExtra() => HasTemperature ? $"Temperature: {temperature.ToStringTemperature()}" : "";

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (HasTemperature && temperature < -2000)
                parent.Map.TemperatureInfo().TryGetEnvironmentTemperatureForCell(parent.Position, out temperature);
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            if (HasTemperature)
                temperature = GenMath.WeightedAverage(temperature, parent.stackCount, otherStack.GetTemperature(), count);
            thermalProps = null;
        }

        public override void PostSplitOff(Thing piece)
        {
            thermalProps = null;
        }
    }
}
