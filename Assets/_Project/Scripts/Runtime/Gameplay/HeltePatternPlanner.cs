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
        private readonly System.Func<int> basicCountSelector;

        private int basicPatternsRemaining;
        private bool blinkAfterSummon;
        private bool? previousPhaseTwo;

        public HeltePatternPlanner(System.Func<int> basicCountSelector = null)
        {
            this.basicCountSelector = basicCountSelector ?? (() => UnityEngine.Random.Range(1, 3));
        }

        public HeltePattern Next(bool phaseTwo)
        {
            if (!previousPhaseTwo.HasValue || previousPhaseTwo.Value != phaseTwo)
            {
                previousPhaseTwo = phaseTwo;
                basicPatternsRemaining = SelectBasicPatternCount();
                blinkAfterSummon = false;
            }

            if (phaseTwo && blinkAfterSummon)
            {
                blinkAfterSummon = false;
                basicPatternsRemaining = SelectBasicPatternCount();
                return HeltePattern.BlinkDash;
            }

            if (basicPatternsRemaining > 0)
            {
                basicPatternsRemaining--;
                return HeltePattern.BasicCombo;
            }

            if (!phaseTwo)
            {
                basicPatternsRemaining = SelectBasicPatternCount();
                return HeltePattern.BlinkDash;
            }

            blinkAfterSummon = true;
            return HeltePattern.SummonSwords;
        }

        private int SelectBasicPatternCount()
        {
            return UnityEngine.Mathf.Clamp(basicCountSelector(), 1, 2);
        }
    }
}
