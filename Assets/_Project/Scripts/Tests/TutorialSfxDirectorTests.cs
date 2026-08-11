using Narthex.Gameplay;
using Narthex.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Narthex.Tests
{
    public sealed class TutorialSfxDirectorTests
    {
        [TestCase(HelteCombatState.PhaseTransition, TutorialSfxCue.BossPhaseTwo)]
        [TestCase(HelteCombatState.FinalRushTransition, TutorialSfxCue.BossFinalRush)]
        [TestCase(HelteCombatState.MercyRetreat, TutorialSfxCue.BossMercy)]
        [TestCase(HelteCombatState.BasicWindup, TutorialSfxCue.BossBasicWindup)]
        [TestCase(HelteCombatState.BasicLeftSlash, TutorialSfxCue.BossSlash)]
        [TestCase(HelteCombatState.BasicRightSlash, TutorialSfxCue.BossSlash)]
        [TestCase(HelteCombatState.BlinkVanish, TutorialSfxCue.BossBlinkOut)]
        [TestCase(HelteCombatState.BlinkReappear, TutorialSfxCue.BossBlinkIn)]
        [TestCase(HelteCombatState.DashTelegraph, TutorialSfxCue.BossDashTelegraph)]
        [TestCase(HelteCombatState.DashApproach, TutorialSfxCue.BossDash)]
        [TestCase(HelteCombatState.CrossSlashTelegraph, TutorialSfxCue.BossCrossTelegraph)]
        [TestCase(HelteCombatState.CrossSlash, TutorialSfxCue.BossCrossSlash)]
        [TestCase(HelteCombatState.SwordFocus, TutorialSfxCue.BossSwordFocus)]
        [TestCase(HelteCombatState.SwordVolley, TutorialSfxCue.BossSwordFire)]
        [TestCase(HelteCombatState.CounterTelegraph, TutorialSfxCue.BossCounterTelegraph)]
        [TestCase(HelteCombatState.CounterStance, TutorialSfxCue.None)]
        [TestCase(HelteCombatState.CounterSucceeded, TutorialSfxCue.BossCounter)]
        [TestCase(HelteCombatState.Recover, TutorialSfxCue.None)]
        public void BossStateMapsToExpectedCue(HelteCombatState state, TutorialSfxCue expected)
        {
            Assert.That(TutorialSfxDirector.ResolveBossCue(state), Is.EqualTo(expected));
        }

        [TestCase(0.82f, 1f, 0.82f)]
        [TestCase(0.82f, 0.5f, 0.41f)]
        [TestCase(2f, 2f, 1f)]
        [TestCase(-1f, 0.5f, 0f)]
        public void EffectiveVolumeIsClampedAndMultiplied(float output, float settings, float expected)
        {
            Assert.That(TutorialSfxDirector.ResolveEffectiveVolume(output, settings), Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(0.5f, 0.5f, 1f, true)]
        [TestCase(0f, 0f, 1f, true)]
        [TestCase(1f, 1f, 1f, true)]
        [TestCase(-0.001f, 0.5f, 1f, false)]
        [TestCase(1.001f, 0.5f, 1f, false)]
        [TestCase(0.5f, -0.001f, 1f, false)]
        [TestCase(0.5f, 1.001f, 1f, false)]
        [TestCase(0.5f, 0.5f, -1f, false)]
        public void ViewportAudibilityRequiresEmitterOnScreen(float x, float y, float z, bool expected)
        {
            Assert.That(TutorialSfxDirector.IsViewportAudible(new Vector3(x, y, z)), Is.EqualTo(expected));
        }
    }
}
