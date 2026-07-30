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
        SummonSwords,
        FakeBlink,
        CounterStance
    }

    public sealed class HeltePatternPlanner
    {
        private readonly System.Func<int> basicCountSelector;

        private int basicPatternsRemaining;
        private bool blinkAfterSummon;
        private int finalRushPatternIndex;
        private bool fakeBlinkPending;
        private bool counterStancePending;
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
            fakeBlinkPending = false;
            counterStancePending = false;
            previousTempo = null;
        }

        public HeltePattern Next(bool phaseTwo)
        {
            return Next(phaseTwo ? HelteCombatTempo.PhaseTwo : HelteCombatTempo.Opening);
        }

        public HeltePattern Next(HelteCombatTempo tempo)
        {
            return Next(tempo, false);
        }

        public HeltePattern Next(HelteCombatTempo tempo, bool useFriendlyPatterns)
        {
            if (!previousTempo.HasValue || previousTempo.Value != tempo)
            {
                previousTempo = tempo;
                basicPatternsRemaining = SelectBasicPatternCount();
                blinkAfterSummon = false;
                finalRushPatternIndex = 0;
                fakeBlinkPending = false;
                counterStancePending = false;
            }

            if (tempo == HelteCombatTempo.FinalRush)
                return NextFinalRushPattern(useFriendlyPatterns);

            if (useFriendlyPatterns && fakeBlinkPending)
            {
                fakeBlinkPending = false;
                return HeltePattern.FakeBlink;
            }

            if (useFriendlyPatterns && counterStancePending)
            {
                counterStancePending = false;
                return HeltePattern.CounterStance;
            }

            if (tempo == HelteCombatTempo.PhaseTwo && blinkAfterSummon)
            {
                blinkAfterSummon = false;
                basicPatternsRemaining = SelectBasicPatternCount();
                counterStancePending = useFriendlyPatterns;
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
                fakeBlinkPending = useFriendlyPatterns;
                return HeltePattern.BlinkDash;
            }

            blinkAfterSummon = true;
            return HeltePattern.SummonSwords;
        }

        private HeltePattern NextFinalRushPattern(bool useFriendlyPatterns)
        {
            if (useFriendlyPatterns)
            {
                var friendlyPattern = finalRushPatternIndex switch
                {
                    0 => HeltePattern.BlinkDash,
                    1 => HeltePattern.BasicCombo,
                    2 => HeltePattern.CounterStance,
                    3 => HeltePattern.SummonSwords,
                    4 => HeltePattern.BasicCombo,
                    _ => HeltePattern.FakeBlink
                };
                finalRushPatternIndex = (finalRushPatternIndex + 1) % 6;
                return friendlyPattern;
            }

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

    public static class HelteFriendlyCombatPolicy
    {
        public static int LimitDamageBeforeMercy(
            int currentHealth,
            int maximumHealth,
            int requestedDamage,
            float mercyHealthRatio,
            bool mercyAvailable)
        {
            if (!mercyAvailable) return requestedDamage;
            var mercyFloor = UnityEngine.Mathf.CeilToInt(maximumHealth * mercyHealthRatio);
            return UnityEngine.Mathf.Clamp(
                requestedDamage,
                0,
                UnityEngine.Mathf.Max(0, currentHealth - mercyFloor));
        }
    }
}
