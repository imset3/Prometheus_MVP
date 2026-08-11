using Narthex.Gameplay;
using Narthex.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Narthex.Tests
{
    public sealed class TutorialMusicDirectorTests
    {
        [TestCase("회의장", TutorialMusicFamily.Adamas)]
        [TestCase("숨겨진 방", TutorialMusicFamily.Adamas)]
        [TestCase("F스테이지", TutorialMusicFamily.OuterCombat)]
        [TestCase("G스테이지", TutorialMusicFamily.OuterCombat)]
        [TestCase("Z05_ExteriorCombat_B", TutorialMusicFamily.OuterCombat)]
        [TestCase("나디르 선착장", TutorialMusicFamily.NadirApproach)]
        public void ResolveLocationFamily_MapsTutorialLocations(string location, TutorialMusicFamily expected)
        {
            Assert.That(TutorialMusicDirector.ResolveLocationFamily(location), Is.EqualTo(expected));
        }

        [Test]
        public void OuterCombatIntensity_OnlyEnablesForGStageDensity()
        {
            Assert.That(TutorialMusicDirector.IsOuterCombatHighIntensity("F스테이지"), Is.False);
            Assert.That(TutorialMusicDirector.IsOuterCombatHighIntensity("G스테이지"), Is.True);
            Assert.That(TutorialMusicDirector.IsOuterCombatHighIntensity("Z05_ExteriorCombat_B"), Is.True);
        }

        [TestCase(HelteCombatState.PhaseTransition, TutorialBossMusicLayer.PhaseTwo)]
        [TestCase(HelteCombatState.FinalRushTransition, TutorialBossMusicLayer.FinalRush)]
        [TestCase(HelteCombatState.MercyRetreat, TutorialBossMusicLayer.Mercy)]
        [TestCase(HelteCombatState.BasicWindup, TutorialBossMusicLayer.None)]
        public void ResolveBossLayer_OnlyChangesMusicAtMeaningfulStates(
            HelteCombatState state,
            TutorialBossMusicLayer expected)
        {
            Assert.That(TutorialMusicDirector.ResolveBossLayer(state), Is.EqualTo(expected));
        }

        [Test]
        public void AreAligned_RequiresMatchingSamplesAndFrequency()
        {
            var first = AudioClip.Create("first", 48000, 2, 48000, false);
            var aligned = AudioClip.Create("aligned", 48000, 2, 48000, false);
            var different = AudioClip.Create("different", 47000, 2, 48000, false);
            try
            {
                Assert.That(TutorialMusicDirector.AreAligned(first, aligned), Is.True);
                Assert.That(TutorialMusicDirector.AreAligned(first, different), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(aligned);
                Object.DestroyImmediate(different);
            }
        }
    }
}
