using RimWorld;
using Verse;

namespace Celsius
{
    public class CompThermal : ThingComp
    {
        [Unsaved]
        ThermalProps thermalProps;

        [Unsaved]
        ThermalProps thermalPropsOpen;

        public bool IsOpen => (parent is Building_Door door && door.Open) || (parent is Building_Vent && parent.GetComp<CompFlickable>()?.SwitchIsOn == true);

        ThermalProps GetCachedThermalProps(bool open) => open ? thermalPropsOpen : thermalProps;

        public ThingThermalProperties ThingThermalProperties => parent.def.GetModExtension<ThingThermalProperties>();

        public StuffThermalProperties StuffThermalProperties =>
            ThingThermalProperties.volume > 0
            ? parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>()
            : null;

        public ThermalProps ThermalProperties
        {
            get
            {
                bool open = IsOpen;
                ThermalProps cachedProps = GetCachedThermalProps(open);
                if (cachedProps != null)
                    return cachedProps;
                if (open)
                    thermalPropsOpen = ThingThermalProperties.GetThermalProps(StuffThermalProperties, true);
                else thermalProps = ThingThermalProperties.GetThermalProps(StuffThermalProperties, false);
                return GetCachedThermalProps(open);
            }
        }

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Building && thingDef.HasModExtension<ThingThermalProperties>();

        internal void Reset() => thermalProps = thermalPropsOpen = null;
    }
}
