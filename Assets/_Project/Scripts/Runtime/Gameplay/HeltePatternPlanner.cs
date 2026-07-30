namespace Narthex.Gameplay
{
    public enum HelteCombatTempo
    {
        Opening,
        PhaseTwo,
        FinalRush
    }

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
        private int finalRushPatternIndex;
        private HelteCombatTempo? previousTempo;

        public HeltePatternPlanner(System.Func<int> basicCountSelector = null)
        {
            this.basicCountSelector = basicCountSelector ?? (() => UnityEngine.Random.Range(1, 3));
        }

        public void Reset()
        {
            basicPatternsRemaining = 0;
            blinkAfterSummon = false;
            finalRushPatternIndex = 0;
            previousTempo = null;
        }

        public HeltePattern Next(bool phaseTwo)
        {
            return Next(phaseTwo ? HelteCombatTempo.PhaseTwo : HelteCombatTempo.Opening);
        }

        public HeltePattern Next(HelteCombatTempo tempo)
        {
            if (!previousTempo.HasValue || previousTempo.Value != tempo)
            {
                previousTempo = tempo;
                basicPatternsRemaining = SelectBasicPatternCount();
                blinkAfterSummon = false;
                finalRushPatternIndex = 0;
            }

            if (tempo == HelteCombatTempo.FinalRush)
                return NextFinalRushPattern();

            if (tempo == HelteCombatTempo.PhaseTwo && blinkAfterSummon)
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

            if (tempo == HelteCombatTempo.Opening)
            {
                basicPatternsRemaining = SelectBasicPatternCount();
                return HeltePattern.BlinkDash;
            }

            blinkAfterSummon = true;
            return HeltePattern.SummonSwords;
        }

        private HeltePattern NextFinalRushPattern()
        {
            // A readable four-beat climax: reposition, punish window, projectile pressure, then another punish window.
            var pattern = finalRushPatternIndex switch
            {
                0 => HeltePattern.BlinkDash,
                1 => HeltePattern.BasicCombo,
                2 => HeltePattern.SummonSwords,
                _ => HeltePattern.BasicCombo
            };
            finalRushPatternIndex = (finalRushPatternIndex + 1) % 4;
            return pattern;
        }

        private int SelectBasicPatternCount()
        {
            return UnityEngine.Mathf.Clamp(basicCountSelector(), 1, 2);
        }
    }
}
