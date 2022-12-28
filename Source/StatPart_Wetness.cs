using RimWorld;
using Verse;

namespace Celsius
{
    public class StatPart_Wetness : StatPart
    {
        public float offset;

        bool AppliesTo(StatRequest req) =>
            Settings.PawnEnvironmentEffects && offset != 0 && (req.Thing as Pawn)?.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(ThoughtDefOf.SoakingWet) != null;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (AppliesTo(req))
                val += offset;
        }

        public override string ExplanationPart(StatRequest req) =>
            AppliesTo(req)
                ? "Celsius_StatPart_Wetness_Explanation".Translate(offset.ToStringTemperatureOffset())
                : null;
    }
}
