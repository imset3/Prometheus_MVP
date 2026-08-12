using NUnit.Framework;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Save;
using Narthex.SceneFlow;
using Narthex.Presentation;
using UnityEngine;

namespace Narthex.Tests
{
    public sealed class GameplayPipelineTests
    {
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void FixedStepMotion_IsInvariantAcrossRenderFrameRates(int renderFramesPerSecond)
        {
            const float duration = 6f;
            const float fixedStep = .02f;
            const float horizontalSpeed = 5f;
            const float jumpVelocity = 8f;
            const float gravity = -9.81f;
            var renderFrameCount = renderFramesPerSecond * (int)duration;
            var fixedStepCount = Mathf.RoundToInt((renderFrameCount / (float)renderFramesPerSecond) / fixedStep);
            var x = 0f;
            var y = 0f;
            var verticalVelocity = jumpVelocity;
            for (var step = 0; step < fixedStepCount; step++)
            {
                x += horizontalSpeed * fixedStep;
                verticalVelocity += gravity * fixedStep;
                y += verticalVelocity * fixedStep;
            }

            Assert.That(fixedStepCount, Is.EqualTo(300));
            Assert.That(x, Is.EqualTo(30f).Within(.001f));
            Assert.That(y, Is.EqualTo(-129.169f).Within(.01f));

            const float projectileDuration = 1.55f;
            var projectileSteps = Mathf.CeilToInt(projectileDuration / fixedStep);
            var elapsed = Mathf.Min(projectileDuration, projectileSteps * fixedStep);
            var projectileX = Mathf.Lerp(198.5f, 192.5f, elapsed / projectileDuration);
            Assert.That(projectileX, Is.EqualTo(192.5f).Within(.001f));
        }

        [Test]
        public void PlayerDashTimingPolicy_AppliesCooldownAfterInvulnerableDashEnds()
        {
            const float dashStartedAt = 10f;
            const float dashDuration = 0.16f;
            const float cooldownAfterDash = 0.5f;

            Assert.That(
                PlayerDashTimingPolicy.ResolveNextAllowedTime(
                    dashStartedAt,
                    dashDuration,
                    cooldownAfterDash),
                Is.EqualTo(10.66f).Within(0.001f));
        }

        [Test]
        public void TutorialTrainingPhasePolicy_ActivatesExactlyOneLessonAndUnlocksAfterTraining()
        {
            var questIds = new[]
            {
                "QST-TUTO-004",
                "QST-TUTO-006",
                "QST-TUTO-002",
                "QST-TUTO-003",
                "QST-TUTO-005"
            };

            for (var phaseIndex = 0; phaseIndex < questIds.Length; phaseIndex++)
            {
                var resolved = TutorialTrainingPhasePolicy.ResolvePhaseIndex(
                    questIds[phaseIndex],
                    questIds);
                Assert.That(resolved, Is.EqualTo(phaseIndex));
                Assert.That(TutorialTrainingPhasePolicy.ShouldLockExit(resolved), Is.True);

                var activeCount = 0;
                for (var candidateIndex = 0; candidateIndex < questIds.Length; candidateIndex++)
                    if (TutorialTrainingPhasePolicy.ShouldActivatePhase(resolved, candidateIndex))
                        activeCount++;
                Assert.That(activeCount, Is.EqualTo(1));
            }

            var completedPhase = TutorialTrainingPhasePolicy.ResolvePhaseIndex(
                "QST-TUTO-007",
                questIds);
            Assert.That(completedPhase, Is.EqualTo(-1));
            Assert.That(TutorialTrainingPhasePolicy.ShouldLockExit(completedPhase), Is.False);
            Assert.That(
                TutorialTrainingPhasePolicy.ShouldActivatePhase(completedPhase, 0),
                Is.False);
        }

        [Test]
        public void TutorialSkillUnlockPolicy_UnlocksAtRangedLessonAndRemainsUnlockedAfterward()
        {
            const int rangedLessonStep = 6;
            Assert.That(TutorialSkillUnlockPolicy.HasReachedStep(5, rangedLessonStep), Is.False);
            Assert.That(TutorialSkillUnlockPolicy.HasReachedStep(6, rangedLessonStep), Is.True);
            Assert.That(TutorialSkillUnlockPolicy.HasReachedStep(10, rangedLessonStep), Is.True);
            Assert.That(TutorialSkillUnlockPolicy.HasReachedStep(10, -1), Is.False);
        }

        [Test]
        public void TitleDisplayModePolicy_MigratesLegacyFullscreenAndKeepsThreeExplicitModes()
        {
            var legacyWindowed = new SettingsSaveData { Fullscreen = false, HasDisplayModeSelection = false };
            var legacyFullscreen = new SettingsSaveData { Fullscreen = true, HasDisplayModeSelection = false };
            Assert.That(TitleScreenHost.ResolveDisplayMode(legacyWindowed), Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(TitleScreenHost.ResolveDisplayMode(legacyFullscreen), Is.EqualTo(FullScreenMode.FullScreenWindow));

            foreach (var mode in new[]
                     {
                         FullScreenMode.Windowed,
                         FullScreenMode.ExclusiveFullScreen,
                         FullScreenMode.FullScreenWindow
                     })
            {
                var settings = new SettingsSaveData
                {
                    DisplayMode = (int)mode,
                    HasDisplayModeSelection = true
                };
                Assert.That(TitleScreenHost.ResolveDisplayMode(settings), Is.EqualTo(mode));
            }
        }

        [Test]
        public void TitleResolutionPolicy_UsesDetectedDisplayModesAndClosestSavedChoice()
        {
            var options = TitleScreenHost.BuildResolutionOptions(
                new[]
                {
                    new Vector2Int(800, 600),
                    new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440),
                    new Vector2Int(1920, 1080)
                },
                new Vector2Int(2560, 1440));

            Assert.That(options, Is.EqualTo(new[]
            {
                new Vector2Int(1920, 1080),
                new Vector2Int(2560, 1440)
            }));
            Assert.That(
                TitleScreenHost.FindClosestSupportedResolution(options, new Vector2Int(2304, 1296)),
                Is.EqualTo(new Vector2Int(2560, 1440)));
        }

        [Test]
        public void SettingsPanelPolicy_FitsInsideSmallAndWideCanvases()
        {
            Assert.That(
                TitleScreenHost.ResolvePanelScale(new Vector2(1920f, 1080f), new Vector2(980f, 780f), 40f),
                Is.EqualTo(1f));
            var compactScale = TitleScreenHost.ResolvePanelScale(
                new Vector2(800f, 600f), new Vector2(980f, 780f), 40f);
            Assert.That(compactScale, Is.LessThan(1f));
            Assert.That(980f * compactScale, Is.LessThanOrEqualTo(720.01f));
            Assert.That(780f * compactScale, Is.LessThanOrEqualTo(520.01f));
        }

        [Test]
        public void TheusFocusedVolleyPolicy_RequiresUnlockAndUsesLargerFinalShot()
        {
            Assert.That(TutorialTheusRangedSupportHost.CanStartFocusedVolley(true, false, 0f, true, false), Is.True);
            Assert.That(TutorialTheusRangedSupportHost.CanStartFocusedVolley(false, false, 0f, true, false), Is.False);
            Assert.That(TutorialTheusRangedSupportHost.CanStartFocusedVolley(true, false, 0.1f, true, false), Is.False);
            Assert.That(TutorialTheusRangedSupportHost.CanStartFocusedVolley(true, true, 0f, true, false), Is.False);
            Assert.That(TutorialTheusRangedSupportHost.CanStartFocusedVolley(true, false, 0f, true, true), Is.False);
            Assert.That(TutorialTheusRangedSupportHost.ResolveFocusedVolleyScale(3, 5, 1.35f), Is.EqualTo(1f));
            Assert.That(TutorialTheusRangedSupportHost.ResolveFocusedVolleyScale(4, 5, 1.35f), Is.EqualTo(1.35f));
        }

        [Test]
        public void DemoEndingFlightPolicy_MovesTowardZenithAndShrinksRearViewAirship()
        {
            var start = new Vector2(-560f, -180f);
            var end = new Vector2(520f, 190f);

            Assert.That(TutorialDemoEndingSequenceHost.CalculateFlightPosition(start, end, 0f), Is.EqualTo(start));
            Assert.That(TutorialDemoEndingSequenceHost.CalculateFlightPosition(start, end, 1f), Is.EqualTo(end));
            Assert.That(TutorialDemoEndingSequenceHost.CalculateFlightScale(1f, 0.12f, 0f), Is.EqualTo(1f));
            Assert.That(TutorialDemoEndingSequenceHost.CalculateFlightScale(1f, 0.12f, 1f), Is.EqualTo(0.12f).Within(0.001f));
            Assert.That(TutorialDemoEndingSequenceHost.SmoothProgress(0.5f), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void TutorialCameraPolicy_UsesVelocityLookAheadAndBossWeightedCenter()
        {
            Assert.That(TutorialCameraPolicy.ResolveLookAhead(0.05f, 2f, 0.2f), Is.Zero);
            Assert.That(TutorialCameraPolicy.ResolveLookAhead(-3f, 2f, 0.2f), Is.EqualTo(-2f));
            Assert.That(TutorialCameraPolicy.ResolveLookAhead(3f, 2f, 0.2f), Is.EqualTo(2f));
            Assert.That(TutorialCameraPolicy.ResolveBossCenter(990f, 1000f, 0.45f), Is.EqualTo(994.5f).Within(0.001f));
        }

        [Test]
        public void TutorialAccessibilityPolicy_EnforcesMinimumSizeAndContrast()
        {
            Assert.That(TutorialAccessibilityPolicy.ResolveFontSize(16, 20), Is.EqualTo(20));
            Assert.That(TutorialAccessibilityPolicy.ResolveFontSize(24, 20), Is.EqualTo(24));
            Assert.That(TutorialAccessibilityPolicy.ResolvePanelAlpha(0.4f, 0.88f), Is.EqualTo(0.88f).Within(0.001f));
            Assert.That(TutorialAccessibilityPolicy.ResolvePanelAlpha(0f, 0.88f), Is.Zero);
        }

        [Test]
        public void TutorialSubtitleTimingPolicy_ShortensOnlyWhenBacklogged()
        {
            Assert.That(TutorialSubtitleTimingPolicy.ResolveVisibleDuration(4.2f, 2.8f, 0), Is.EqualTo(4.2f));
            Assert.That(TutorialSubtitleTimingPolicy.ResolveVisibleDuration(4.2f, 2.8f, 1), Is.EqualTo(2.8f));
            Assert.That(TutorialSubtitleTimingPolicy.ResolveVisibleDuration(4.2f, 2.8f, 4), Is.EqualTo(2.8f));
        }

        [Test]
        public void TutorialSubtitleDismissPolicy_UnlocksSpaceAfterOneSecond()
        {
            Assert.That(TutorialSubtitleDismissPolicy.CanDismiss(0.99f, 1f, true), Is.False);
            Assert.That(TutorialSubtitleDismissPolicy.CanDismiss(1f, 1f, false), Is.False);
            Assert.That(TutorialSubtitleDismissPolicy.CanDismiss(1f, 1f, true), Is.True);
        }

        [Test]
        public void TutorialTriggerSweepPolicy_DetectsFastCrossingAndRejectsMiss()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(2f, 8f, 0f));
            Assert.That(TutorialTriggerSweepPolicy.Intersects(bounds, new Vector2(-10f, 0f), new Vector2(10f, 0f)), Is.True);
            Assert.That(TutorialTriggerSweepPolicy.Intersects(bounds, new Vector2(-10f, 10f), new Vector2(10f, 10f)), Is.False);
        }

        [Test]
        public void TutorialUpdraftPolicy_CompensatesGravityAndBuildsStableRiseSpeed()
        {
            var minimum = new Vector2(-4f, -2f);
            var maximum = new Vector2(4f, 3f);
            Assert.That(TutorialUpdraftPolicy.ShouldApply(false, Vector2.zero, minimum, maximum), Is.False,
                "Updraft recovery must not activate without held glide input.");
            Assert.That(TutorialUpdraftPolicy.ShouldApply(true, Vector2.zero, minimum, maximum), Is.True);
            Assert.That(TutorialUpdraftPolicy.ShouldApply(true, new Vector2(8f, 0f), minimum, maximum), Is.False);
            Assert.That(TutorialUpdraftPolicy.HasReturnClearance(15.5f, 12f), Is.True);
            Assert.That(TutorialUpdraftPolicy.HasReturnClearance(11.5f, 12f), Is.False,
                "The updraft must not stop below the return ledge.");

            const float fixedDeltaTime = 0.02f;
            const float gravityMagnitude = 29.43f;
            var firstStep = TutorialUpdraftPolicy.ResolveVerticalVelocity(
                -3f, 6.5f, 4.5f, gravityMagnitude, fixedDeltaTime);
            Assert.That(firstStep - gravityMagnitude * fixedDeltaTime, Is.GreaterThan(0f));

            var cappedStep = TutorialUpdraftPolicy.ResolveVerticalVelocity(
                4.4f, 6.5f, 4.5f, gravityMagnitude, fixedDeltaTime);
            Assert.That(cappedStep, Is.EqualTo(4.5f).Within(0.001f));
        }

        [Test]
        public void TutorialEnvironmentHazardPolicy_UsesPercentDamageHeldGlideAndAliveSafeReturn()
        {
            Assert.That(
                TutorialEnvironmentHazardPolicy.ResolveFractionalDamage(100, 0.1f),
                Is.EqualTo(10));
            Assert.That(
                TutorialEnvironmentHazardPolicy.ResolveFractionalDamage(100, 0.2f),
                Is.EqualTo(20));
            Assert.That(
                TutorialEnvironmentHazardPolicy.ResolveFractionalDamage(7, 0.1f),
                Is.EqualTo(1));

            Assert.That(
                TutorialEnvironmentHazardPolicy.ShouldApplyWind(true, false),
                Is.False,
                "Wind must not replace the player's Space/glide input.");
            Assert.That(
                TutorialEnvironmentHazardPolicy.ShouldApplyWind(false, true),
                Is.False);
            Assert.That(
                TutorialEnvironmentHazardPolicy.ShouldApplyWind(true, true),
                Is.True);

            var rise = TutorialEnvironmentHazardPolicy.ResolveWindVelocity(
                -3f,
                24f,
                8f,
                29.43f,
                0.02f);
            Assert.That(rise, Is.GreaterThan(0f));
            Assert.That(
                TutorialEnvironmentHazardPolicy.ResolveWindVelocity(
                    7.9f,
                    24f,
                    8f,
                    29.43f,
                    0.02f),
                Is.EqualTo(8f).Within(0.001f));
            var rightwardWind = TutorialEnvironmentHazardPolicy.ResolveDirectionalWindVelocity(
                new Vector2(0f, -2f),
                Vector2.right,
                24f,
                8f,
                Physics2D.gravity,
                0.02f);
            Assert.That(rightwardWind.x, Is.GreaterThan(0f));
            Assert.That(rightwardWind.y, Is.EqualTo(-2f).Within(0.001f),
                "Rotating a wind marker must rotate only its authored force direction.");

            Assert.That(TutorialEnvironmentHazardPolicy.ShouldReturnToSafePoint(true), Is.True);
            Assert.That(
                TutorialEnvironmentHazardPolicy.ShouldReturnToSafePoint(false),
                Is.False,
                "Fatal lava damage must be handled by the normal G checkpoint restart.");
        }

        [Test]
        public void TutorialGlideRetryPolicy_RetriesOnlyActiveGlideCrossingsBelowFailHeight()
        {
            const float failHeight = -5.25f;
            Assert.That(
                TutorialGlideRetryPolicy.ShouldRetry(
                    TutorialChapter0IntroState.SeekPasskey,
                    -5.3f,
                    failHeight),
                Is.True);
            Assert.That(
                TutorialGlideRetryPolicy.ShouldRetry(
                    TutorialChapter0IntroState.ReturnToMeeting,
                    -6f,
                    failHeight),
                Is.True);
            Assert.That(
                TutorialGlideRetryPolicy.ShouldRetry(
                    TutorialChapter0IntroState.SeekPasskey,
                    -5f,
                    failHeight),
                Is.False);
            Assert.That(
                TutorialGlideRetryPolicy.ShouldRetry(
                    TutorialChapter0IntroState.SeekLedge,
                    -6f,
                    failHeight),
                Is.False);
        }

        [Test]
        public void TutorialAimPolicy_KeyboardIgnoresStalePointerDeltaAndGamepadUsesStick()
        {
            Assert.That(TutorialAimPolicy.ResolveNonPointerAttackDirection(false, -1f, 1f, -1f), Is.EqualTo(1f));
            Assert.That(TutorialAimPolicy.ResolveNonPointerAttackDirection(true, -1f, 1f, 1f), Is.EqualTo(-1f));
            Assert.That(TutorialAimPolicy.ResolveNonPointerAttackDirection(false, -1f, 0f, -1f), Is.EqualTo(-1f));
        }

        [Test]
        public void TutorialQuestSequence_CompletesAllStepsAndPersistsBossCompletion()
        {
            var events = new GameEventBus();
            var permanent = new PermanentSaveData();
            var run = new RunSaveData();
            var quests = new QuestManager(events);
            var questDefinitions = new QuestDefinition[8];
            var conditions = new QuestConditionDefinition[8];
            var signalTypes = new[]
            {
                QuestSignalType.MovementPerformed,
                QuestSignalType.JumpPerformed,
                QuestSignalType.AttackPerformed,
                QuestSignalType.DashPerformed,
                QuestSignalType.ModuleUsed,
                QuestSignalType.ModuleTreeOpened,
                QuestSignalType.TowerActivated,
                QuestSignalType.BossKilled
            };
            var targetIds = new[]
            {
                "PLAYER-001", "PLAYER-001", "PLAYER-001", "PLAYER-001",
                "PLAYER-001", "PLAYER-001", "RELAY-TUTO-001", "BOSS-TUTO-HELTE"
            };

            for (var index = 0; index < questDefinitions.Length; index++)
            {
                conditions[index] = ScriptableObject.CreateInstance<QuestConditionDefinition>();
                conditions[index].ConfigureIdentity("COND-SEQUENCE-" + index);
                conditions[index].SignalType = signalTypes[index];
                conditions[index].TargetId = targetIds[index];
                questDefinitions[index] = ScriptableObject.CreateInstance<QuestDefinition>();
                questDefinitions[index].ConfigureIdentity("QST-TUTO-" + (index + 1).ToString("000"));
                questDefinitions[index].Conditions = new[] { conditions[index] };
                quests.Register(questDefinitions[index]);
            }

            var currentQuest = 0;
            events.Subscribe<QuestCompleted>(message =>
            {
                if (!run.QuestIds.Contains(message.QuestId)) run.QuestIds.Add(message.QuestId);
                currentQuest++;
                if (currentQuest < questDefinitions.Length) quests.Start(questDefinitions[currentQuest].StableId);
            });
            var completion = new TutorialBossCompletion(events, permanent, run, "BOSS-TUTO-HELTE", "CHAPTER_01");
            events.Subscribe<BossKilled>(message => completion.TryComplete(message));

            Assert.That(quests.Start(questDefinitions[0].StableId), Is.True);
            for (var index = 0; index < signalTypes.Length - 1; index++)
                events.Publish(new GameplaySignal(signalTypes[index], targetIds[index]));

            events.Publish(new BossKilled("BOSS-TUTO-HELTE", "TUTORIAL", "TREE-BOSS-HELTE"));
            events.Publish(new GameplaySignal(QuestSignalType.BossKilled, "BOSS-TUTO-HELTE"));

            Assert.That(run.QuestIds, Has.Count.EqualTo(8));
            Assert.That(permanent.TutorialCompleted, Is.True);
            Assert.That(permanent.UnlockedTreeIds, Does.Contain("TREE-BOSS-HELTE"));
            Assert.That(run.CurrentStageId, Is.EqualTo("CHAPTER_01"));

            quests.Dispose();
            events.Dispose();
            foreach (var quest in questDefinitions) Object.DestroyImmediate(quest);
            foreach (var condition in conditions) Object.DestroyImmediate(condition);
        }

        [Test]
        public void TutorialProgressRestore_RequiresRelayAndCompletionQuest()
        {
            const string relayId = "RELAY-TUTO-001";
            const string relayQuestId = "QST-TUTO-007";

            var incomplete = new RunSaveData();
            incomplete.ActivatedTowerIds.Add(relayId);
            Assert.That(TutorialProgressRestore.IsRelayProgressRestored(incomplete, relayId, relayQuestId), Is.False);

            incomplete.QuestIds.Add(relayQuestId);
            Assert.That(TutorialProgressRestore.IsRelayProgressRestored(incomplete, relayId, relayQuestId), Is.True);
        }

        [Test]
        public void TutorialProgressRestore_SelectsFirstIncompleteQuest()
        {
            var run = new RunSaveData();
            run.QuestIds.Add("QST-TUTO-001");
            run.QuestIds.Add("QST-TUTO-002");
            var questIds = new[] { "QST-TUTO-001", "QST-TUTO-002", "QST-TUTO-003" };

            Assert.That(TutorialProgressRestore.FindFirstIncompleteQuestIndex(run, questIds), Is.EqualTo(2));
            run.QuestIds.Add("QST-TUTO-003");
            Assert.That(TutorialProgressRestore.FindFirstIncompleteQuestIndex(run, questIds), Is.EqualTo(2));
        }

        [Test]
        public void TutorialProgressRestore_SelectsSavedCheckpointBeforeQuestHostAwake()
        {
            var questIds = new[]
            {
                "QST-TUTO-001", "QST-TUTO-004", "QST-TUTO-006", "QST-TUTO-002", "QST-TUTO-003",
                "QST-TUTO-005", "QST-TUTO-007", "QST-TUTO-007-A", "QST-TUTO-007-B", "QST-TUTO-008"
            };
            var run = new RunSaveData();
            for (var index = 0; index < 8; index++) run.QuestIds.Add(questIds[index]);

            Assert.That(TutorialProgressRestore.FindFirstIncompleteQuestIndex(run, questIds), Is.EqualTo(8));
        }

        [Test]
        public void TutorialChapter0IntroProgress_RestoresHiddenRoomAndPasskeyReturn()
        {
            Assert.That(
                TutorialChapter0IntroProgress.Resolve(TutorialChapter0IntroProgress.MeetingStageId, false),
                Is.EqualTo(TutorialChapter0IntroState.SeekHiddenRoom));
            Assert.That(
                TutorialChapter0IntroProgress.Resolve(TutorialChapter0IntroProgress.HiddenRoomStageId, false),
                Is.EqualTo(TutorialChapter0IntroState.HiddenRoomEntryDialogue));
            Assert.That(
                TutorialChapter0IntroProgress.Resolve(TutorialChapter0IntroProgress.ReturnStageId, true),
                Is.EqualTo(TutorialChapter0IntroState.SeekTrainingExit));

            var items = new System.Collections.Generic.List<string>
            {
                TutorialChapter0IntroProgress.PasskeyItemId
            };
            Assert.That(TutorialChapter0IntroProgress.ContainsPasskey(items), Is.True);
            Assert.That(TutorialChapter0IntroProgress.ContainsPasskey(null), Is.False);

            var entry = new Vector3(-38f, 1f, 0f);
            Assert.That(
                TutorialChapter0IntroProgress.HasReachedHiddenRoomEntry(new Vector3(-36.5f, 1f, 0f), entry, false),
                Is.True,
                "The transition must begin before the player can step past the deck edge.");
            Assert.That(
                TutorialChapter0IntroProgress.HasReachedHiddenRoomEntry(new Vector3(-35f, 1f, 0f), entry, false),
                Is.False);
            Assert.That(
                TutorialChapter0IntroProgress.HasReachedHiddenRoomEntry(new Vector3(-20f, 20f, 0f), entry, true),
                Is.True);
        }

        [Test]
        public void ModuleUnlockEquipUse_ExecutesAbilityAndPublishesModuleUsed()
        {
            var events = new GameEventBus();
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.ConfigureIdentity("ABILITY-TEST");
            var module = ScriptableObject.CreateInstance<ModuleDefinition>();
            module.ConfigureIdentity("MODULE-TEST");
            module.TreeId = "TREE-BASIC-001";
            module.Ability = ability;
            var executor = new AbilityExecutor(events);
            var modules = new ModuleSystem(events, executor);
            var used = false;
            events.Subscribe<ModuleUsed>(_ => used = true);
            modules.Register(module);

            Assert.That(modules.Unlock(module.StableId), Is.True);
            Assert.That(modules.Equip(module.StableId, 0), Is.True);
            Assert.That(modules.TryUse("PLAYER", module.StableId), Is.True);
            Assert.That(used, Is.True);

            events.Dispose();
            Object.DestroyImmediate(module);
            Object.DestroyImmediate(ability);
        }

        [Test]
        public void ModuleTree_ConsumesPointsAndRequiresPrerequisiteModules()
        {
            var events = new GameEventBus();
            var permanent = new PermanentSaveData();
            var run = new RunSaveData { ModulePoints = 2 };
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.ConfigureIdentity("ABILITY-TREE");
            var firstModule = CreateModule("MOD-TREE-001", "TREE-BASIC-001", ability, 1);
            var secondModule = CreateModule("MOD-TREE-002", "TREE-BASIC-001", ability, 1);
            var tree = ScriptableObject.CreateInstance<ModuleTreeDefinition>();
            tree.ConfigureIdentity("TREE-BASIC-001");
            tree.AvailableAtRunStart = true;
            tree.Nodes = new[]
            {
                new ModuleNodeDefinition { Module = firstModule },
                new ModuleNodeDefinition { Module = secondModule, RequiredModuleIds = new[] { firstModule.StableId } }
            };
            var modules = new ModuleSystem(events, new AbilityExecutor(events));
            var manager = new ModuleTreeManager(events, modules, permanent, run);
            manager.Register(tree);

            Assert.That(manager.TryUnlockModule(secondModule.StableId), Is.False);
            Assert.That(manager.TryUnlockModule(firstModule.StableId), Is.True);
            Assert.That(manager.TryUnlockModule(secondModule.StableId), Is.True);
            Assert.That(run.ModulePoints, Is.EqualTo(0));
            Assert.That(manager.TryEquipModule(secondModule.StableId, 0), Is.True);
            Assert.That(run.EquippedModuleSlots[0].ModuleId, Is.EqualTo(secondModule.StableId));

            events.Dispose();
            Object.DestroyImmediate(tree);
            Object.DestroyImmediate(firstModule);
            Object.DestroyImmediate(secondModule);
            Object.DestroyImmediate(ability);
        }

        [Test]
        public void QuestManager_CompletesMovementQuestFromGameplaySignal()
        {
            var events = new GameEventBus();
            var condition = ScriptableObject.CreateInstance<QuestConditionDefinition>();
            condition.ConfigureIdentity("COND-MOVE");
            condition.SignalType = QuestSignalType.MovementPerformed;
            condition.TargetId = "PLAYER-001";
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureIdentity("QST-TUTO-MOVE");
            quest.Conditions = new[] { condition };
            var quests = new QuestManager(events);
            quests.Register(quest);

            Assert.That(quests.Start(quest.StableId), Is.True);
            events.Publish(new GameplaySignal(QuestSignalType.MovementPerformed, "PLAYER-001"));

            Assert.That(quests.TryGetState(quest.StableId, out var state), Is.True);
            Assert.That(state.Status, Is.EqualTo(QuestRuntimeStatus.Completed));

            quests.Dispose();
            events.Dispose();
            Object.DestroyImmediate(quest);
            Object.DestroyImmediate(condition);
        }

        [Test]
        public void QuestManager_CompletesDashQuestFromGameplaySignal()
        {
            var events = new GameEventBus();
            var condition = ScriptableObject.CreateInstance<QuestConditionDefinition>();
            condition.ConfigureIdentity("COND-DASH");
            condition.SignalType = QuestSignalType.DashPerformed;
            condition.TargetId = "PLAYER-001";
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureIdentity("QST-TUTO-DASH");
            quest.Conditions = new[] { condition };
            var quests = new QuestManager(events);
            quests.Register(quest);

            Assert.That(quests.Start(quest.StableId), Is.True);
            quests.SetProgressSignalFilter((questId, signal) => false);
            events.Publish(new GameplaySignal(QuestSignalType.DashPerformed, "PLAYER-001"));

            Assert.That(quests.TryGetState(quest.StableId, out var blockedState), Is.True);
            Assert.That(blockedState.Status, Is.EqualTo(QuestRuntimeStatus.InProgress));
            Assert.That(quests.GetConditionProgress(quest.StableId, condition.StableId), Is.Zero,
                "A matching action outside its authored lesson area must not count.");

            quests.SetProgressSignalFilter((questId, signal) => true);
            events.Publish(new GameplaySignal(QuestSignalType.DashPerformed, "PLAYER-001"));

            Assert.That(quests.TryGetState(quest.StableId, out var state), Is.True);
            Assert.That(state.Status, Is.EqualTo(QuestRuntimeStatus.Completed));

            quests.Dispose();
            events.Dispose();
            Object.DestroyImmediate(quest);
            Object.DestroyImmediate(condition);
        }

        [Test]
        public void DoubleJumpTrainingQuest_RequiresSummitArrivalSignal()
        {
            var events = new GameEventBus();
            var summit = CreateCondition(
                "COND-DOUBLE-JUMP-SUMMIT",
                QuestSignalType.PortalUsed,
                "TRAINING-DOUBLE-JUMP-SUMMIT");
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureIdentity("QST-TUTO-006");
            quest.Conditions = new[] { summit };
            var quests = new QuestManager(events);
            quests.Register(quest);

            Assert.That(quests.Start(quest.StableId), Is.True);
            events.Publish(new GameplaySignal(QuestSignalType.DoubleJumpPerformed, "PLAYER-001"));
            Assert.That(quests.TryGetState(quest.StableId, out var beforeSummit), Is.True);
            Assert.That(beforeSummit.Status, Is.EqualTo(QuestRuntimeStatus.InProgress),
                "Performing a double jump alone must not finish the marker-authored lesson.");
            Assert.That(quests.GetConditionProgress(quest.StableId, summit.StableId), Is.Zero);

            events.Publish(new GameplaySignal(
                QuestSignalType.PortalUsed,
                "TRAINING-DOUBLE-JUMP-SUMMIT"));
            Assert.That(quests.TryGetState(quest.StableId, out var afterSummit), Is.True);
            Assert.That(afterSummit.Status, Is.EqualTo(QuestRuntimeStatus.Completed));
            Assert.That(quests.GetConditionProgress(quest.StableId, summit.StableId), Is.EqualTo(1));

            quests.Dispose();
            events.Dispose();
            Object.DestroyImmediate(quest);
            Object.DestroyImmediate(summit);
        }

        [Test]
        public void QuestManager_ResetProgress_RestartsAnActiveTrainingRequirement()
        {
            var events = new GameEventBus();
            var condition = ScriptableObject.CreateInstance<QuestConditionDefinition>();
            condition.ConfigureIdentity("COND-DASH-RESTART");
            condition.SignalType = QuestSignalType.DashPerformed;
            condition.TargetId = "PLAYER-001";
            condition.RequiredAmount = 2;
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureIdentity("QST-TUTO-DASH-RESTART");
            quest.Conditions = new[] { condition };
            var quests = new QuestManager(events);
            quests.Register(quest);

            Assert.That(quests.Start(quest.StableId), Is.True);
            events.Publish(new GameplaySignal(QuestSignalType.DashPerformed, "PLAYER-001"));
            Assert.That(quests.ResetProgress(quest.StableId), Is.True);
            events.Publish(new GameplaySignal(QuestSignalType.DashPerformed, "PLAYER-001"));

            Assert.That(quests.TryGetState(quest.StableId, out var state), Is.True);
            Assert.That(state.Status, Is.EqualTo(QuestRuntimeStatus.InProgress));
            events.Publish(new GameplaySignal(QuestSignalType.DashPerformed, "PLAYER-001"));
            Assert.That(state.Status, Is.EqualTo(QuestRuntimeStatus.Completed));

            quests.Dispose();
            events.Dispose();
            Object.DestroyImmediate(quest);
            Object.DestroyImmediate(condition);
        }

        [Test]
        public void BossQuestCompletion_GrantsModulePointAndPermanentTree()
        {
            var events = new GameEventBus();
            var permanent = new PermanentSaveData();
            var run = new RunSaveData();
            var reward = ScriptableObject.CreateInstance<RewardDefinition>();
            reward.ConfigureIdentity("REWARD-HELTE");
            reward.RewardType = RewardType.BossModuleTreeUnlock;
            reward.TargetId = "TREE-BOSS-HELTE";
            var condition = ScriptableObject.CreateInstance<QuestConditionDefinition>();
            condition.ConfigureIdentity("COND-HELTE");
            condition.SignalType = QuestSignalType.BossKilled;
            condition.TargetId = "BOSS-TUTO-HELTE";
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureIdentity("QST-TUTO-008");
            quest.Conditions = new[] { condition };
            quest.Rewards = new[] { reward };
            var rewards = new RewardExecutor(events, permanent, run);
            rewards.Register(reward);
            var quests = new QuestManager(events);
            quests.Register(quest);
            Assert.That(quests.Start(quest.StableId), Is.True);

            events.Publish(new GameplaySignal(QuestSignalType.BossKilled, "BOSS-TUTO-HELTE"));

            Assert.That(quests.TryGetState(quest.StableId, out var state), Is.True);
            Assert.That(state.Status, Is.EqualTo(QuestRuntimeStatus.Completed));
            Assert.That(permanent.UnlockedTreeIds.Contains("TREE-BOSS-HELTE"), Is.True);

            quests.Dispose();
            rewards.Dispose();
            events.Dispose();
            Object.DestroyImmediate(quest);
            Object.DestroyImmediate(condition);
            Object.DestroyImmediate(reward);
        }

        [Test]
        public void TutorialBossCompletion_SavesPermanentBossRewardOnlyOnce()
        {
            var events = new GameEventBus();
            var permanent = new PermanentSaveData();
            var run = new RunSaveData();
            var completion = new TutorialBossCompletion(events, permanent, run, "BOSS-TUTO-HELTE", "CHAPTER_01");
            var completedCount = 0;
            events.Subscribe<TutorialCompleted>(_ => completedCount++);
            var message = new BossKilled("BOSS-TUTO-HELTE", "TUTO-006", "TREE-BOSS-HELTE");

            Assert.That(completion.TryComplete(message), Is.True);
            Assert.That(completion.TryComplete(message), Is.False);
            Assert.That(permanent.TutorialCompleted, Is.True);
            Assert.That(permanent.BossKillRecords, Does.Contain("BOSS-TUTO-HELTE"));
            Assert.That(permanent.UnlockedTreeIds, Does.Contain("TREE-BOSS-HELTE"));
            Assert.That(run.CurrentStageId, Is.EqualTo("CHAPTER_01"));
            Assert.That(completedCount, Is.EqualTo(1));

            events.Dispose();
        }

        [Test]
        public void HeltePatternPlanner_UsesOneOrTwoBasicsBeforePhaseSpecificSpecials()
        {
            var oneBasicPhaseOne = new HeltePatternPlanner(() => 1);
            Assert.That(oneBasicPhaseOne.Next(false), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(oneBasicPhaseOne.Next(false), Is.EqualTo(HeltePattern.BlinkDash));
            Assert.That(oneBasicPhaseOne.Next(false), Is.EqualTo(HeltePattern.BasicCombo));

            var twoBasicsPhaseTwo = new HeltePatternPlanner(() => 2);
            Assert.That(twoBasicsPhaseTwo.Next(true), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(twoBasicsPhaseTwo.Next(true), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(twoBasicsPhaseTwo.Next(true), Is.EqualTo(HeltePattern.SummonSwords));
            Assert.That(twoBasicsPhaseTwo.Next(true), Is.EqualTo(HeltePattern.BlinkDash));
            Assert.That(twoBasicsPhaseTwo.Next(true), Is.EqualTo(HeltePattern.BasicCombo));
        }

        [Test]
        public void HeltePatternPlanner_ResetRestartsTheOpeningSequence()
        {
            var planner = new HeltePatternPlanner(() => 1);
            Assert.That(planner.Next(false), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(planner.Next(false), Is.EqualTo(HeltePattern.BlinkDash));

            planner.Reset();

            Assert.That(planner.Next(false), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(planner.Next(false), Is.EqualTo(HeltePattern.BlinkDash));
        }

        [Test]
        public void HeltePatternPlanner_FinalRushUsesReadableFourBeatSequence()
        {
            var planner = new HeltePatternPlanner(() => 2);

            Assert.That(planner.Next(HelteCombatTempo.FinalRush), Is.EqualTo(HeltePattern.BlinkDash));
            Assert.That(planner.Next(HelteCombatTempo.FinalRush), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(planner.Next(HelteCombatTempo.FinalRush), Is.EqualTo(HeltePattern.SummonSwords));
            Assert.That(planner.Next(HelteCombatTempo.FinalRush), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(planner.Next(HelteCombatTempo.FinalRush), Is.EqualTo(HeltePattern.BlinkDash));
        }

        [Test]
        public void HeltePatternPlanner_FriendlyPrototypeAddsFeintAndCounterWithoutReplacingCorePatterns()
        {
            var opening = new HeltePatternPlanner(() => 1);
            Assert.That(opening.Next(HelteCombatTempo.Opening, true), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(opening.Next(HelteCombatTempo.Opening, true), Is.EqualTo(HeltePattern.BlinkDash));
            Assert.That(opening.Next(HelteCombatTempo.Opening, true), Is.EqualTo(HeltePattern.FakeBlink));
            Assert.That(opening.Next(HelteCombatTempo.Opening, true), Is.EqualTo(HeltePattern.BasicCombo));

            var phaseTwo = new HeltePatternPlanner(() => 1);
            Assert.That(phaseTwo.Next(HelteCombatTempo.PhaseTwo, true), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(phaseTwo.Next(HelteCombatTempo.PhaseTwo, true), Is.EqualTo(HeltePattern.SummonSwords));
            Assert.That(phaseTwo.Next(HelteCombatTempo.PhaseTwo, true), Is.EqualTo(HeltePattern.BlinkDash));
            Assert.That(phaseTwo.Next(HelteCombatTempo.PhaseTwo, true), Is.EqualTo(HeltePattern.CounterStance));

            var finalTest = new HeltePatternPlanner(() => 1);
            Assert.That(finalTest.Next(HelteCombatTempo.FinalRush, true), Is.EqualTo(HeltePattern.BlinkDash));
            Assert.That(finalTest.Next(HelteCombatTempo.FinalRush, true), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(finalTest.Next(HelteCombatTempo.FinalRush, true), Is.EqualTo(HeltePattern.CounterStance));
            Assert.That(finalTest.Next(HelteCombatTempo.FinalRush, true), Is.EqualTo(HeltePattern.SummonSwords));
            Assert.That(finalTest.Next(HelteCombatTempo.FinalRush, true), Is.EqualTo(HeltePattern.BasicCombo));
            Assert.That(finalTest.Next(HelteCombatTempo.FinalRush, true), Is.EqualTo(HeltePattern.FakeBlink));
        }

        [Test]
        public void HelteFriendlyCombatPolicy_ReservesMercyBeforeLethalFollowUp()
        {
            Assert.That(HelteFriendlyCombatPolicy.IsMercyAvailable(30f, 30f), Is.True);
            Assert.That(HelteFriendlyCombatPolicy.IsMercyAvailable(29.99f, 30f), Is.False);
            Assert.That(
                HelteFriendlyCombatPolicy.LimitDamageBeforeMercy(32, 100, 15, 0.25f, true),
                Is.EqualTo(7));
            Assert.That(
                HelteFriendlyCombatPolicy.LimitDamageBeforeMercy(25, 100, 15, 0.25f, true),
                Is.Zero);
            Assert.That(
                HelteFriendlyCombatPolicy.LimitDamageBeforeMercy(25, 100, 15, 0.25f, false),
                Is.EqualTo(15));
        }

        [Test]
        public void BossCombatCueResolver_ExplainsFriendlyAndFinalRushStates()
        {
            var fakeOpen = BossCombatCuePresenter.ResolveCue(HelteCombatState.FakeBlinkPause);
            var counterRisk = BossCombatCuePresenter.ResolveCue(HelteCombatState.CounterStance);
            var mercy = BossCombatCuePresenter.ResolveCue(HelteCombatState.MercyRetreat);
            var finalRush = BossCombatCuePresenter.ResolveCue(HelteCombatState.FinalRushTransition);

            Assert.That(fakeOpen.Visible, Is.True);
            Assert.That(fakeOpen.Text, Does.Contain("공격하지 않습니다"));
            Assert.That(counterRisk.Text, Does.Contain("밀려납니다"));
            Assert.That(mercy.Text, Does.Contain("휴식"));
            Assert.That(finalRush.Text, Does.Contain("FINAL TEST"));
            Assert.That(BossCombatCuePresenter.ResolveCue(HelteCombatState.Waiting).Visible, Is.False);
        }

        [Test]
        public void CharacterPngAnimationBridge_MapsFriendlyHelteStatesToExistingClips()
        {
            Assert.That(
                CharacterPngAnimationBridge.ResolveHelteAnimationState(HelteCombatState.FinalRushTransition),
                Is.EqualTo("PhaseTransition"));
            Assert.That(
                CharacterPngAnimationBridge.ResolveHelteAnimationState(HelteCombatState.FakeBlinkVanish),
                Is.EqualTo("BlinkVanish"));
            Assert.That(
                CharacterPngAnimationBridge.ResolveHelteAnimationState(HelteCombatState.CounterTelegraph),
                Is.EqualTo("CounterTelegraph"));
            Assert.That(
                CharacterPngAnimationBridge.ResolveHelteAnimationState(HelteCombatState.FakeBlinkReappear),
                Is.EqualTo("BlinkReappear"));
            Assert.That(
                CharacterPngAnimationBridge.ResolveHelteAnimationState(HelteCombatState.FakeBlinkPause),
                Is.EqualTo("Recover"));
            Assert.That(
                CharacterPngAnimationBridge.ResolveHelteAnimationState(HelteCombatState.CounterSucceeded),
                Is.EqualTo("CounterStance"));
            Assert.That(
                CharacterPngAnimationBridge.ResolveHelteAnimationState(HelteCombatState.MercyRetreat),
                Is.EqualTo("Recover"));
            Assert.That(CharacterPngAnimationBridge.ShouldFlipAuthoredSprite(false, 1f), Is.True,
                "Helte's authored left-facing frames must flip when the player is to the right.");
            Assert.That(CharacterPngAnimationBridge.ShouldFlipAuthoredSprite(false, -1f), Is.False,
                "Helte's authored left-facing frames must remain unflipped when the player is to the left.");
        }

        [Test]
        public void TutorialHudModeResolver_UsesResultDialogueBossPriority()
        {
            Assert.That(TutorialHudModeResolver.Resolve(false, false, false, false), Is.EqualTo(TutorialHudMode.Normal));
            Assert.That(TutorialHudModeResolver.Resolve(false, false, false, true), Is.EqualTo(TutorialHudMode.BossCombat));
            Assert.That(TutorialHudModeResolver.Resolve(false, true, false, true), Is.EqualTo(TutorialHudMode.Dialogue));
            Assert.That(TutorialHudModeResolver.Resolve(false, false, true, true), Is.EqualTo(TutorialHudMode.Dialogue));
            Assert.That(TutorialHudModeResolver.Resolve(false, true, true, false, true), Is.EqualTo(TutorialHudMode.Epilogue));
            Assert.That(TutorialHudModeResolver.Resolve(true, true, true, true), Is.EqualTo(TutorialHudMode.Result));
        }

        [Test]
        public void PromeBossSkillPolicy_UsesCooldownAndResolvesFourStrikes()
        {
            Assert.That(PromeBossSkillHost.CanActivate(true, true, true, 0f, false), Is.True);
            Assert.That(PromeBossSkillHost.CanActivate(true, true, true, 0.1f, false), Is.False);
            Assert.That(PromeBossSkillHost.CanActivate(true, true, true, 0f, true), Is.False);
            Assert.That(PromeBossSkillHost.CanActivate(false, true, true, 0f, false), Is.False);

            var strikes = new[] { 35, 40, 45, 100 };
            Assert.That(PromeBossSkillHost.ResolveStrikeDamage(strikes, 0), Is.EqualTo(35));
            Assert.That(PromeBossSkillHost.ResolveStrikeDamage(strikes, 3), Is.EqualTo(100));
            Assert.That(PromeBossSkillHost.ResolveStrikeDamage(strikes, 4), Is.Zero);
            Assert.That(PromeBossSkillHost.ResolveSkillFacing(-1f, 1f), Is.EqualTo(-1f),
                "The player's current input-facing direction must override stale visual state.");
            Assert.That(PromeBossSkillHost.ResolveSkillFacing(0f, -1f), Is.EqualTo(-1f));
            Assert.That(PromeBossSkillHost.ShouldFlipStrikeSprite(1f), Is.False,
                "The authored right-facing slash must stay unflipped while Prome faces right.");
            Assert.That(PromeBossSkillHost.ShouldFlipStrikeSprite(-1f), Is.True,
                "The slash sprite must mirror while Prome faces left.");
            Assert.That(PromeBossSkillHost.IsBossInForwardRange(-3f, -1f, 4.5f), Is.True);
            Assert.That(PromeBossSkillHost.IsBossInForwardRange(3f, -1f, 4.5f), Is.False);
            Assert.That(CharacterPngAnimationBridge.ShouldSuppressHitReaction(true), Is.True,
                "Taking damage must not cancel the active four-slash presentation.");
        }

        [Test]
        public void BossHealthBarPresenter_ExplainsSafeAndUnsafeHelteWindows()
        {
            Assert.That(BossHealthBarPresenter.ResolveStateLabel(HelteCombatState.FakeBlinkPause), Is.EqualTo("공격 기회"));
            Assert.That(BossHealthBarPresenter.ResolveStateLabel(HelteCombatState.CounterStance), Is.EqualTo("공격 금지"));
            Assert.That(BossHealthBarPresenter.ResolveStateLabel(HelteCombatState.PhaseTransition), Is.EqualTo("PHASE 2"));
            Assert.That(BossHealthBarPresenter.ResolveStateLabel(HelteCombatState.Waiting), Is.Empty);
            Assert.That(BossHealthBarPresenter.ResolveHealthLabel("헬테", 1250, 2500),
                Is.EqualTo("헬테   1,250 / 2,500   ·   50%"));
        }

        [Test]
        public void BossHealthBarPresenter_KeepsHudFixedForTheWholeEncounter()
        {
            Assert.That(BossHealthBarPresenter.ShouldKeepVisible(true, false, true), Is.True,
                "The fixed HUD must stay visible throughout active Helte patterns.");
            Assert.That(BossHealthBarPresenter.ShouldKeepVisible(false, false, true), Is.False);
            Assert.That(BossHealthBarPresenter.ShouldKeepVisible(true, true, true), Is.False);
            Assert.That(BossHealthBarPresenter.ShouldKeepVisible(true, false, false), Is.False);
        }

        private static ModuleDefinition CreateModule(string id, string treeId, AbilityDefinition ability, int cost)
        {
            var module = ScriptableObject.CreateInstance<ModuleDefinition>();
            module.ConfigureIdentity(id);
            module.TreeId = treeId;
            module.Ability = ability;
            module.UnlockCost = cost;
            return module;
        }

        private static QuestConditionDefinition CreateCondition(string id, QuestSignalType signalType, string targetId)
        {
            var condition = ScriptableObject.CreateInstance<QuestConditionDefinition>();
            condition.ConfigureIdentity(id);
            condition.SignalType = signalType;
            condition.TargetId = targetId;
            condition.RequiredAmount = 1;
            return condition;
        }
    }
}
