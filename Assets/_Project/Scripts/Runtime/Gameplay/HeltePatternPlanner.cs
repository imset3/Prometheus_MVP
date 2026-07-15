namespace Narthex.Gameplay
{
    public enum HeltePattern
    {
        None,
        BasicCombo,
        BlinkDash,
        SummonSwords
    }

    public sealed class HeltePatternPlanner
    {
        private const int BasicPatternsBeforeSpecial = 2;

        private int basicPatternCount;
        private bool blinkAfterSummon;

        public HeltePattern Next(bool phaseTwo)
        {
            if (phaseTwo && blinkAfterSummon)
            {
                blinkAfterSummon = false;
                return HeltePattern.BlinkDash;
            }

            if (basicPatternCount < BasicPatternsBeforeSpecial)
            {
                basicPatternCount++;
                return HeltePattern.BasicCombo;
            }

            basicPatternCount = 0;
            if (!phaseTwo) return HeltePattern.BlinkDash;

            blinkAfterSummon = true;
            return HeltePattern.SummonSwords;
        }
    }
}
