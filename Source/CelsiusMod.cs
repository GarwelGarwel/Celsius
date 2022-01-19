using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Celsius
{
    public class CelsiusMod : Mod
    {
        float airHeatCapacity, heatConductivityFactor, convectionConductivityEffect;

        public CelsiusMod(ModContentPack content)
            : base(content)
        {
            GetSettings<Settings>();
            airHeatCapacity = Settings.AirHeatCapacity;
            heatConductivityFactor = Settings.HeatConductivityFactor;
            convectionConductivityEffect = Settings.ConvectionConductivityEffect;
        }

        public override void DoSettingsWindowContents(Rect rect)
        {
            Listing_Standard content = new Listing_Standard();
            content.Begin(rect);

            content.CheckboxLabeled("Show temperature map", ref Settings.ShowTemperatureMap, "Show heat map and a mouse cursor tooltip.");
            content.CheckboxLabeled("Freezing and melting", ref Settings.FreezingAndMeltingEnabled, "Water can freeze and ice can melt into water.");
            content.CheckboxLabeled("Autoignition", ref Settings.AutoignitionEnabled, "Flammable things can spontaneously catch fire when they get too hot.");
            content.Label($"Change the following values at your own risk.".Colorize(Color.red));

            content.Label($"Heat conductivity: {Settings.HeatConductivityMultiplier.ToStringPercent()}", tooltip: "How quickly heat travels and temperatures equalize.");
            Settings.HeatConductivityMultiplier = (float)Math.Round(content.Slider(Settings.HeatConductivityMultiplier, 0.1f, 2), 1);
            Settings.HeatConductivityFactor = Settings.HeatConductivityFactor_Base * Settings.HeatConductivityMultiplier;

            content.Label($"Convection conductivity effect: x{Settings.ConvectionConductivityEffect}", tooltip: $"How much air convection increases air conductivity. Recommended value: {Settings.ConvectionConductivityEffect_Default}.");
            Settings.ConvectionConductivityEffect = (float)Math.Round(content.Slider(Settings.ConvectionConductivityEffect, 1, 50));

            content.Label($"Heat push: {Settings.HeatPushMultiplier.ToStringPercent()}", tooltip: "Effect of things that produce or reduce heat (fires, heaters, coolers, pawns).");
            Settings.HeatPushMultiplier = (float)Math.Round(content.Slider(Settings.HeatPushMultiplier, 0, 5), 1);
            Settings.HeatPushEffect = Settings.HeatPushEffect_Base * Settings.HeatPushMultiplier;

            content.Label(
                $"Air heat capacity: {Settings.AirHeatCapacity:N0} J/C",
                tooltip: $"Heat capacity (how slowly air changes temperature) in Joules/Celsius. Recommended value: {Settings.AirHeatCapacity_Default:N0} J/C.");
            Settings.AirHeatCapacity = (float)Math.Round(content.Slider(Settings.AirHeatCapacity / 10, 40, 200)) * 10;

            content.CheckboxLabeled("Debug logging mode", ref Settings.DebugMode, "Verbose logging of Celsius' work.");

            if (content.ButtonText("Reset to default"))
                Settings.Reset();

            content.End();
        }

        public override string SettingsCategory() => "Celsius";

        public override void WriteSettings()
        {
            base.WriteSettings();
            if (airHeatCapacity != Settings.AirHeatCapacity
                || heatConductivityFactor != Settings.HeatConductivityFactor
                || convectionConductivityEffect != Settings.ConvectionConductivityEffect)
            {
                LogUtility.Log("Air-related settings have changed. Recalculating air properties.");
                TemperatureUtility.RecalculateAirProperties();
                airHeatCapacity = Settings.AirHeatCapacity;
                heatConductivityFactor = Settings.HeatConductivityFactor;
                convectionConductivityEffect = Settings.ConvectionConductivityEffect;
            }
        }
    }
}
