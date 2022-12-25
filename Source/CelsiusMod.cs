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
            // Localization key: Celsius_settings_vanilla - Use vanilla colors | Celsius_settings_vanilla_ToolTip - Use vanilla color scheme for heat overlay instead of Celsius'.
            content.CheckboxLabeled("Celsius_settings_Vanilla".Translate(), ref UseVanillaTemperatureColors, "Celsius_settings_Vanilla_ToolTip".Translate());
            // Localization key: Celsius_settings_showtemptooltip - Show temperature tooltip | Celsius_settings_showtemptooltip_ToolTip - When heat overlay is on, show a tooltip next to the cursor with the cell's exact temperature.
            content.CheckboxLabeled("Celsius_settings_Showtemptooltip".Translate(), ref ShowTemperatureTooltip, "Celsius_settings_Showtemptooltip_ToolTip".Translate());
            // Localization key: Celsius_settings_FreezingAndMelting - Freezing and melting | Celsius_settings_FreezingAndMelting_ToolTip - Water can freeze and ice can melt into water.
            content.CheckboxLabeled("Celsius_settings_FreezingAndMelting".Translate(), ref FreezingAndMeltingEnabled, "Celsius_settings_FreezingAndMelting_ToolTip".Translate());
            // Localization key: Celsius_settings_AdvAutoignition - Advanced autoignition | Celsius_settings_AdvAutoignition_ToolTip - Flammable things can spontaneously catch fire when they get too hot. Replaces vanilla autoignition.
            content.CheckboxLabeled("Celsius_settings_AdvAutoignition".Translate(), ref AutoignitionEnabled, "Celsius_settings_AdvAutoignition_ToolTip".Translate());

            // Localization key: Celsius_settings_TempDisplayDigits - Temperature display digits:{TemperatureDisplayDigits} | Celsius_settings_TempDisplayDigits_ToolTip - How many digits to print after point for temperatures. Default value: {TemperatureDisplayDigits_Default}.
            content.Label("Celsius_settings_TempDisplayDigits".Translate(TemperatureDisplayDigits), tooltip: "Celsius_settings_TempDisplayDigits_ToolTip".Translate(TemperatureDisplayDigits_Default));
            TemperatureDisplayDigits = Mathf.RoundToInt(content.Slider(TemperatureDisplayDigits, 0, 2));
            TemperatureDisplayFormatString = $"F{TemperatureDisplayDigits}";

            content.Gap();
            // Localization key: Celsius_settings_MountainTemp - Mountain temperature:
            content.Label("Celsius_settings_MountainTemp".Translate());

            void AddMountainmodeOption(MountainTemperatureMode mode, string tooltip)
            {
                if (Find.CurrentMap?.TemperatureInfo() != null)
                    // Localization key: Celsius_settings_CurrentMapTemptooltip - Currently: {Find.CurrentMap.TemperatureInfo().GetMountainTemperatureFor(mode).ToStringTemperature(Settings.TemperatureDisplayFormatString)}
                    tooltip += "\n" + "Celsius_settings_CurrentMapTemptooltip".Translate(Find.CurrentMap.TemperatureInfo().GetMountainTemperatureFor(mode).ToStringTemperature(Settings.TemperatureDisplayFormatString));
                // Localization key: Celsius_MountainTemperatureMode_Vanilla - Vanilla
                //                   Celsius_MountainTemperatureMode_AnnualAverage - Annual Average
                //                   Celsius_MountainTemperatureMode_SeasonAverage - Season Average
                //                   Celsius_MountainTemperatureMode_AmbientAir - Ambient Air
                //                   Celsius_MountainTemperatureMode_Manual- Manual
                if (content.RadioButton($"Celsius_MountainTemperatureMode_{mode.ToStringSafe()}".Translate(), Settings.MountainTemperatureMode == mode, (int)mode, tooltip))
                    Settings.MountainTemperatureMode = mode;
            }

            // Localization key: Celsius_MountainTemperatureMode_Vanilla_tooltip - Mountains provide a constant underground temperature (similar to vanilla behaviour).
            AddMountainmodeOption(MountainTemperatureMode.Vanilla, "Celsius_MountainTemperatureMode_Vanilla_tooltip".Translate());
            // Localization key: Celsius_MountainTemperatureMode_AnnualAverage_tooltip - Mountains provide a stable temperature throughout the year, which is defined by the map's climate.
            AddMountainmodeOption(MountainTemperatureMode.AnnualAverage, "Celsius_MountainTemperatureMode_AnnualAverage_tooltip".Translate());
            // Localization key: Celsius_MountainTemperatureMode_SeasonAverage_tooltip - Mountains provide a temperature that gradually changes with season.
            AddMountainmodeOption(MountainTemperatureMode.SeasonAverage, "Celsius_MountainTemperatureMode_SeasonAverage_tooltip".Translate());
            // Localization key: Celsius_MountainTemperatureMode_AmbientAir_tooltip - Mountains have no special thermal effect, their temperature is the same as outdoor temperature.
            AddMountainmodeOption(MountainTemperatureMode.AmbientAir, "Celsius_MountainTemperatureMode_AmbientAir_tooltip".Translate());
            // Localization key: Celsius_MountainTemperatureMode_Manual_tooltip - Set mountain temperature to your wishes.
            AddMountainmodeOption(MountainTemperatureMode.Manual, "Celsius_MountainTemperatureMode_Manual_tooltip".Translate());
            if (Settings.MountainTemperatureMode == MountainTemperatureMode.Manual)
            {
                // Localization key: Celsius_settings_ManualTemperature - Temperature: {MountainTemperature.ToStringTemperature("F0")}
                content.Label("Celsius_settings_ManualTemperature".Translate(MountainTemperature.ToStringTemperature("F0")));
                MountainTemperature = Mathf.Round(content.Slider(MountainTemperature, -100, 100));
            }

            content.Gap();
            // Localization key: Celsius_settings_TakeOwnRisk - Change the values below at your own risk.
            content.Label("Celsius_settings_TakeOwnRisk".Translate().Colorize(Color.red));

            // Localization key: Celsius_settings_ConductivitySpeed - Conductivity speed: {(1 / Mathf.Log(ConductivityPowerBase, ConductivityPowerBase_Default)).ToStringPercent()}
            //                   Celsius_settings_ConductivitySpeed_tooltip - How quickly temperature changes.
            content.Label($"Celsius_settings_ConductivitySpeed".Translate((1 / Mathf.Log(ConductivityPowerBase, ConductivityPowerBase_Default)).ToStringPercent()), tooltip: "Celsius_settings_ConductivitySpeed_tooltip".Translate());
            ConductivityPowerBase = (float)Math.Round(content.Slider(ConductivityPowerBase, 0.1f, 0.9f), 2);

            // Localization key: Celsius_settings_ConvectionConductivityEffect - Convection conductivity effect: x{ConvectionConductivityEffect}
            //                   Celsius_settings_ConvectionConductivityEffect_tooltip - How much air convection increases air conductivity. Recommended value: {ConvectionConductivityEffect_Default}.
            content.Label($"{"Celsius_settings_ConvectionConductivityEffect".Translate()} x{ ConvectionConductivityEffect}", tooltip: "Celsius_settings_ConvectionConductivityEffect_tooltip".Translate(ConvectionConductivityEffect_Default));
            ConvectionConductivityEffect = Mathf.Round(content.Slider(ConvectionConductivityEffect, 1, 50));

            // Localization key: Celsius_settings_EnvironmentDiffusion - Environment diffusion: {EnvironmentDiffusionFactor.ToStringPercent()}
            //                   Celsius_settings_EnvironmentDiffusion_tooltip - How strongly environment (e.g. outdoor) temperature affects cell temperatures. Recommended value: {EnvironmentDiffusionFactor_Default.ToStringPercent()}.
            content.Label("Celsius_settings_EnvironmentDiffusion".Translate(EnvironmentDiffusionFactor.ToStringPercent()), tooltip: "Celsius_settings_EnvironmentDiffusion_tooltip".Translate(EnvironmentDiffusionFactor_Default.ToStringPercent()));
            EnvironmentDiffusionFactor = Mathf.Round(content.Slider(EnvironmentDiffusionFactor, 0, 1) / 0.1f) * 0.1f;

            // Localization key: Celsius_settings_HeatPush - Heat push: {HeatPushMultiplier.ToStringPercent()}
            //                   Celsius_settings_HeatPush_tooltip - Effect of things that produce or reduce heat (e.g. fires, heaters and coolers).
            content.Label("Celsius_settings_HeatPush".Translate(HeatPushMultiplier.ToStringPercent()), tooltip: "Celsius_settings_HeatPush_tooltip".Translate());
            HeatPushMultiplier = Mathf.Round(content.Slider(HeatPushMultiplier, 0, 2) / 0.1f) * 0.1f;
            HeatPushEffect = HeatPushEffect_Base * HeatPushMultiplier;

            // Localization key: Celsius_settings_DebugMode - Debug logging mode
            //                   Celsius_settings_DebugMode_tooltip - Verbose logging of Celsius' work. Necessary for bug reports.
            content.CheckboxLabeled("Celsius_settings_DebugMode".Translate(), ref DebugMode, "Celsius_settings_DebugMode_tooltip".Translate());

            // Localization key: Celsius_settings_ResetDefault - Reset to default
            if (content.ButtonText("Celsius_settings_ResetDefault".Translate()))
                Reset();

            content.End();
        }

        public override string SettingsCategory() => "Celsius";

        public override void WriteSettings()
        {
            base.WriteSettings();
            LogUtility.Log("Settings changed.");
            Print();
            TemperatureUtility.SettingsChanged();
        }
    }
}
