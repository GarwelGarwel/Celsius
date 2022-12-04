using RimWorld;
using Verse;

namespace Celsius
{
    public class CompThermal : ThingComp
    {
        [Unsaved]
        ThermalProps thermalProps;

        [Unsaved]
        bool? isOpen;

        public bool IsOpen
        {
            get => isOpen ?? (IsOpen = (parent is Building_Door door && door.Open) || (parent is Building_Vent && parent.GetComp<CompFlickable>()?.SwitchIsOn == true));
            internal set
            {
                isOpen = value;
                thermalProps = ThingThermalProperties.GetThermalProps(StuffThermalProperties, value);
            }
        }

        public ThingThermalProperties ThingThermalProperties => parent.def.GetModExtension<ThingThermalProperties>();

        public StuffThermalProperties StuffThermalProperties =>
            ThingThermalProperties.volume > 0
            ? parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>()
            : null;

        public ThermalProps ThermalProperties => thermalProps ?? (thermalProps = ThingThermalProperties.GetThermalProps(StuffThermalProperties, IsOpen));

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Building && thingDef.HasModExtension<ThingThermalProperties>();

        internal void Reset()
        {
            thermalProps = null;
            isOpen = null;
        }
    }
}
