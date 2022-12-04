using System;
using UnityEngine;
using Verse;

using static Celsius.Settings;

namespace Celsius
{
    public class CelsiusMod : Mod
    {
        public CelsiusMod(ModContentPack content)
            : base(content) =>
            GetSettings<Settings>();

        public override void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard content = new Listing_Standard();
            content.Begin(rect);

            content.CheckboxLabeled("Use vanilla colors", ref UseVanillaTemperatureColors, "Use vanilla color scheme for heat overlay instead of Celsius'.");
            content.CheckboxLabeled("Show temperature tooltip", ref ShowTemperatureTooltip, "When heat overlay is on, show a tooltip next to the cursor with the cell's exact temperature.");
            content.CheckboxLabeled("Freezing and melting", ref FreezingAndMeltingEnabled, "Water can freeze and ice can melt into water.");
            content.CheckboxLabeled("Advanced autoignition", ref AutoignitionEnabled, "Flammable things can spontaneously catch fire when they get too hot. Replaces vanilla autoignition.");

            content.Gap();
            content.Label("Mountain temperature:");

            void AddMountainmodeOption(MountainTemperatureMode mode, string tooltip)
            {
                if (Find.CurrentMap?.TemperatureInfo() != null)
                    tooltip += $"\nCurrently: {Find.CurrentMap.TemperatureInfo().GetMountainTemperatureFor(mode).ToStringTemperature()}";
                if (content.RadioButton(GenText.SplitCamelCase(mode.ToStringSafe()), Settings.MountainTemperatureMode == mode, (int)mode, tooltip))
                    Settings.MountainTemperatureMode = mode;
            }

            AddMountainmodeOption(MountainTemperatureMode.Vanilla, "Mountains provide a constant underground temperature (similar to vanilla behaviour).");
            AddMountainmodeOption(MountainTemperatureMode.AnnualAverage, "Mountains provide a stable temperature throughout the year, which is defined by the map's climate.");
            AddMountainmodeOption(MountainTemperatureMode.SeasonAverage, "Mountains provide a temperature that gradually changes with season.");
            AddMountainmodeOption(MountainTemperatureMode.AmbientAir, "Mountains have no special thermal effect, their temperature is the same as outdoor temperature.");
            AddMountainmodeOption(MountainTemperatureMode.Manual, "Set mountain temperature to your wishes.");
            if (Settings.MountainTemperatureMode == MountainTemperatureMode.Manual)
            {
                content.Label($"Temperature: {MountainTemperature.ToStringTemperature("F0")}");
                MountainTemperature = (float)Math.Round(content.Slider(MountainTemperature, -100, 100));
            }

            content.Gap();
            content.Label($"Change the following values at your own risk.".Colorize(Color.red));

            content.Label($"Convection conductivity effect: x{ConvectionConductivityEffect}", tooltip: $"How much air convection increases air conductivity. Recommended value: {ConvectionConductivityEffect_Default}.");
            ConvectionConductivityEffect = (float)Math.Round(content.Slider(ConvectionConductivityEffect, 1, 500));

            content.Label($"Environment diffusion: {EnvironmentDiffusionFactor.ToStringPercent()}", tooltip: $"How strongly environment (e.g. outdoor) temperature affects cell temperatures. Recommended value: {EnvironmentDiffusionFactor_Default.ToStringPercent()}.");
            EnvironmentDiffusionFactor = (float)Math.Round(content.Slider(EnvironmentDiffusionFactor, 0, 1), 1);

            content.Label($"Heat push: {HeatPushMultiplier.ToStringPercent()}", tooltip: "Effect of things that produce or reduce heat (fires, heaters, coolers, pawns).");
            HeatPushMultiplier = (float)Math.Round(content.Slider(HeatPushMultiplier, 0, 5), 1);
            HeatPushEffect = HeatPushEffect_Base * HeatPushMultiplier;

            content.CheckboxLabeled("Debug logging mode", ref DebugMode, "Verbose logging of Celsius' work.");

            if (content.ButtonText("Reset to default"))
                Reset();

            content.End();
        }

        public override string SettingsCategory() => "Celsius";

        public override void WriteSettings()
        {
            base.WriteSettings();
            Print();
            TemperatureUtility.SettingsChanged();
        }
    }
}
