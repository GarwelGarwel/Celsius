using System;
using UnityEngine;
using Verse;

using static Celsius.Settings;

namespace Celsius
{
    public class CelsiusMod : Mod
    {
        Vector2 scrollPosition = new Vector2();
        Rect viewRect;
        Listing_Standard content = new Listing_Standard();

        public CelsiusMod(ModContentPack content)
            : base(content) =>
            GetSettings<Settings>();

        public override void DoSettingsWindowContents(Rect rect)
        {
            if (viewRect.height <= 0)
                viewRect = new Rect(0, 0, rect.width - GenUI.ScrollBarWidth, 0);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            content.ColumnWidth = viewRect.width;
            content.Begin(viewRect);

            // Localization key: Celsius_Settings_Vanilla - Use vanilla colors | Celsius_Settings_vanilla_tooltip - Use vanilla color scheme for heat overlay instead of Celsius'.
            content.CheckboxLabeled("Celsius_Settings_Vanilla".Translate(), ref UseVanillaTemperatureColors, "Celsius_Settings_Vanilla_tooltip".Translate());
            // Localization key: Celsius_Settings_ShowTempTooltip - Show temperature tooltip | Celsius_Settings_showtemptooltip_tooltip - When heat overlay is on, show a tooltip next to the cursor with the cell's exact temperature.
            content.CheckboxLabeled("Celsius_Settings_ShowTempTooltip".Translate(), ref ShowTemperatureTooltip, "Celsius_Settings_ShowTempTooltip_tooltip".Translate());
            // Localization key: Celsius_Settings_FreezingAndMelting - Freezing and melting | Celsius_Settings_FreezingAndMelting_tooltip - Water can freeze and ice can melt into water.
            content.CheckboxLabeled("Celsius_Settings_FreezingAndMelting".Translate(), ref FreezingAndMeltingEnabled, "Celsius_Settings_FreezingAndMelting_tooltip".Translate());
            // Localization key: Celsius_Settings_AdvAutoignition - Advanced autoignition | Celsius_Settings_AdvAutoignition_tooltip - Flammable things can spontaneously catch fire when they get too hot. Replaces vanilla autoignition.
            content.CheckboxLabeled("Celsius_Settings_AdvAutoignition".Translate(), ref AutoignitionEnabled, "Celsius_Settings_AdvAutoignition_tooltip".Translate());
            content.CheckboxLabeled("Celsius_Settings_PawnEnvironmentEffects".Translate(), ref PawnEnvironmentEffects, "Celsius_Settings_PawnEnvironmentEffects_tooltip".Translate());

            // Localization key: Celsius_Settings_TempDisplayDigits - Temperature display digits:{TemperatureDisplayDigits} | Celsius_Settings_TempDisplayDigits_tooltip - How many digits to print after point for temperatures. Default value: {TemperatureDisplayDigits_Default}.
            content.Label("Celsius_Settings_TempDisplayDigits".Translate(TemperatureDisplayDigits), tooltip: "Celsius_Settings_TempDisplayDigits_tooltip".Translate(TemperatureDisplayDigits_Default));
            TemperatureDisplayDigits = Mathf.RoundToInt(content.Slider(TemperatureDisplayDigits, 0, 2));
            TemperatureDisplayFormatString = $"F{TemperatureDisplayDigits}";

            content.Gap();
            // Localization key: Celsius_Settings_MountainTemp - Mountain temperature:
            content.Label("Celsius_Settings_MountainTemp".Translate());

            for (MountainTemperatureMode mode = 0; mode <= MountainTemperatureMode.Manual; mode++)
            {
                string tooltip = $"Celsius_MountainTemperatureMode_{mode.ToStringSafe()}_tooltip".Translate();
                if (Find.CurrentMap?.TemperatureInfo() != null)
                    // Localization key: Celsius_Settings_CurrentMapTemp_Tooltip - Currently: {Find.CurrentMap.TemperatureInfo().GetMountainTemperatureFor(mode).ToStringTemperature(Settings.TemperatureDisplayFormatString)}
                    tooltip += "\n" + "Celsius_Settings_CurrentMapTemp_tooltip".Translate(Find.CurrentMap.TemperatureInfo().GetMountainTemperatureFor(mode).ToStringTemperature(TemperatureDisplayFormatString));
                // Localization key: Celsius_MountainTemperatureMode_Vanilla - Vanilla
                //                   Celsius_MountainTemperatureMode_AnnualAverage - Annual Average
                //                   Celsius_MountainTemperatureMode_SeasonAverage - Season Average
                //                   Celsius_MountainTemperatureMode_AmbientAir - Ambient Air
                //                   Celsius_MountainTemperatureMode_Manual- Manual
                if (content.RadioButton($"Celsius_MountainTemperatureMode_{mode.ToStringSafe()}".Translate(), Settings.MountainTemperatureMode == mode, (int)mode, tooltip))
                    Settings.MountainTemperatureMode = mode;
            }

            if (Settings.MountainTemperatureMode == MountainTemperatureMode.Manual)
            {
                // Localization key: Celsius_Settings_ManualTemperature - Temperature: {MountainTemperature.ToStringTemperature("F0")}
                content.Label("Celsius_Settings_ManualTemperature".Translate(MountainTemperature.ToStringTemperature(TemperatureDisplayFormatString)));
                MountainTemperature = Mathf.Round(content.Slider(MountainTemperature, -100, 100));
            }

            content.Gap();
            // Localization key: Celsius_Settings_TakeOwnRisk - Change the values below at your own risk.
            content.Label("Celsius_Settings_TakeOwnRisk".Translate().Colorize(Color.red));

            // Localization key: Celsius_Settings_ConductivitySpeed - Conductivity speed: {(1 / Mathf.Log(ConductivityPowerBase, ConductivityPowerBase_Default)).ToStringPercent()}
            //                   Celsius_Settings_ConductivitySpeed_tooltip - How quickly temperature changes.
            content.Label($"Celsius_Settings_ConductivitySpeed".Translate((1 / Mathf.Log(ConductivityPowerBase, ConductivityPowerBase_Default)).ToStringPercent()), tooltip: "Celsius_Settings_ConductivitySpeed_tooltip".Translate());
            ConductivityPowerBase = (float)Math.Round(content.Slider(ConductivityPowerBase, 0.1f, 0.9f), 2);

            // Localization key: Celsius_Settings_ConvectionConductivityEffect - Convection conductivity effect: x{ConvectionConductivityEffect}
            //                   Celsius_Settings_ConvectionConductivityEffect_tooltip - How much air convection increases air conductivity. Recommended value: {ConvectionConductivityEffect_Default}.
            content.Label($"{"Celsius_Settings_ConvectionConductivityEffect".Translate()} x{ConvectionConductivityEffect}", tooltip: "Celsius_Settings_ConvectionConductivityEffect_tooltip".Translate(ConvectionConductivityEffect_Default));
            ConvectionConductivityEffect = Mathf.Round(content.Slider(ConvectionConductivityEffect, 1, 50));

            // Localization key: Celsius_Settings_EnvironmentDiffusion - Environment diffusion: {EnvironmentDiffusionFactor.ToStringPercent()}
            //                   Celsius_Settings_EnvironmentDiffusion_tooltip - How strongly environment (e.g. outdoor) temperature affects cell temperatures. Recommended value: {EnvironmentDiffusionFactor_Default.ToStringPercent()}.
            content.Label("Celsius_Settings_EnvironmentDiffusion".Translate(EnvironmentDiffusionFactor.ToStringPercent()), tooltip: "Celsius_Settings_EnvironmentDiffusion_tooltip".Translate(EnvironmentDiffusionFactor_Default.ToStringPercent()));
            EnvironmentDiffusionFactor = (float)Math.Round(content.Slider(EnvironmentDiffusionFactor, 0, 1), 1);

            // Localization key: Celsius_Settings_HeatPush - Heat push: {HeatPushMultiplier.ToStringPercent()}
            //                   Celsius_Settings_HeatPush_tooltip - Effect of things that produce or reduce heat (e.g. fires, heaters and coolers).
            content.Label("Celsius_Settings_HeatPush".Translate(HeatPushMultiplier.ToStringPercent()), tooltip: "Celsius_Settings_HeatPush_tooltip".Translate());
            HeatPushMultiplier = (float)Math.Round(content.Slider(HeatPushMultiplier, 0, 2), 1);
            HeatPushEffect = HeatPushEffect_Base * HeatPushMultiplier;

            // Localization key: Celsius_Settings_DebugMode - Debug logging mode
            //                   Celsius_Settings_DebugMode_tooltip - Verbose logging of Celsius' work. Necessary for bug reports.
            content.CheckboxLabeled("Celsius_Settings_DebugMode".Translate(), ref DebugMode, "Celsius_Settings_DebugMode_tooltip".Translate());

            // Localization key: Celsius_Settings_ResetDefault - Reset to default
            if (content.ButtonText("Celsius_Settings_ResetDefault".Translate()))
                Reset();

            viewRect.height = content.MaxColumnHeightSeen + 40;
            content.End();
            Widgets.EndScrollView();
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
