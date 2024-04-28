using System.Collections.Generic;
using Verse;

namespace Celsius
{
    public enum PhaseTransitionType
    {
        None = 0,
        Freeze,
        Melt
    }

    public class TerrainThermalProperties : ThingThermalProperties
    {
        public PhaseTransitionType phaseTransition;

        public float transitionTemperature = float.NaN;

        public TerrainDef replaceWith;

        public override IEnumerable<string> ConfigErrors()
        {
            if (phaseTransition == PhaseTransitionType.None)
                yield return "phaseTransition not set.";
            if (float.IsNaN(transitionTemperature))
                yield return "transitionTemperature not set.";
        }

        public bool FreezesAt(float temperature) => phaseTransition == PhaseTransitionType.Freeze && temperature < transitionTemperature;

        public bool MeltsAt(float temperature) => phaseTransition == PhaseTransitionType.Melt && temperature > transitionTemperature;

        public override string ToString() =>
            $"Heat capacity: {heatCapacity:F1}. Insulation: {insulation:F1}. Conductivity: {GetThermalProps().conductivity:P1}. {phaseTransition}s at {transitionTemperature:F1}.{(replaceWith != null ? $" Replaced with {replaceWith.defName}." : "")}";
    }
}
