using RimWorld;
using Verse;

namespace Celsius
{
    public class CompThermal : ThingComp
    {
        bool isOpen;

        ThermalProps thermalProps;

        public bool IsOpen
        {
            get => isOpen;
            set
            {
                isOpen = value;
                Reset();
            }
        }

        public ThingThermalProperties ThingThermalProperties => parent.def.GetModExtension<ThingThermalProperties>();

        public StuffThermalProperties StuffThermalProperties =>
            ThingThermalProperties.volume > 0
            ? parent.GetUnderlyingStuff()?.GetModExtension<StuffThermalProperties>() ?? parent.def.GetModExtension<StuffThermalProperties>()
            : null;

        public ThermalProps ThermalProperties => thermalProps ?? (thermalProps = ThingThermalProperties.GetThermalProps(StuffThermalProperties, IsOpen));

        internal static bool ShouldApplyTo(ThingDef thingDef) => thingDef.category == ThingCategory.Building && thingDef.HasModExtension<ThingThermalProperties>();

        internal void Reset() => thermalProps = ThingThermalProperties.GetThermalProps(StuffThermalProperties, IsOpen);

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            IsOpen |= (parent is Building_Door door && door.Open) || (parent is Building_Vent && parent.GetComp<CompFlickable>()?.SwitchIsOn == true);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref isOpen, "isOpen");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Reset();
        }

        public override void ReceiveCompSignal(string signal)
        {
            // Vents
            if (parent is Building_Vent)
                switch (signal.ToLowerInvariant())
                {
                    case "flickedon":
                        IsOpen = true;
                        break;

                    case "flickedoff":
                        IsOpen = false;
                        break;
                }
            // Windows mod
            else if (parent.GetType().FullName == "OpenTheWindows.Building_Window")
                switch (signal.ToLowerInvariant())
                {
                    case "airon":
                    case "bothon":
                        IsOpen = true;
                        break;

                    case "airoff":
                    case "bothoff":
                        IsOpen = false;
                        break;
                }
        }
    }
}
