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
        }

        [Test]
        public void TutorialSubtitleTimingPolicy_ShortensOnlyWhenBacklogged()
        {
            Assert.That(TutorialSubtitleTimingPolicy.ResolveVisibleDuration(4.2f, 2.8f, 0), Is.EqualTo(4.2f));
            Assert.That(TutorialSubtitleTimingPolicy.ResolveVisibleDuration(4.2f, 2.8f, 1), Is.EqualTo(2.8f));
            Assert.That(TutorialSubtitleTimingPolicy.ResolveVisibleDuration(4.2f, 2.8f, 4), Is.EqualTo(2.8f));
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
            const float fixedDeltaTime = 0.02f;
            const float gravityMagnitude = 29.43f;
            var firstStep = TutorialUpdraftPolicy.ResolveVerticalVelocity(
                -3f, 5.5f, 3.5f, gravityMagnitude, fixedDeltaTime);
            Assert.That(firstStep - gravityMagnitude * fixedDeltaTime, Is.GreaterThan(0f));

            var cappedStep = TutorialUpdraftPolicy.ResolveVerticalVelocity(
                3.4f, 5.5f, 3.5f, gravityMagnitude, fixedDeltaTime);
            Assert.That(cappedStep, Is.EqualTo(3.5f).Within(0.001f));
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
                "QST-TUTO-001", "QST-TUTO-004", "QST-TUTO-002", "QST-TUTO-003", "QST-TUTO-005",
                "QST-TUTO-006", "QST-TUTO-007", "QST-TUTO-007-A", "QST-TUTO-007-B", "QST-TUTO-008"
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
            events.Publish(new GameplaySignal(QuestSignalType.DashPerformed, "PLAYER-001"));

            Assert.That(quests.TryGetState(quest.StableId, out var state), Is.True);
            Assert.That(state.Status, Is.EqualTo(QuestRuntimeStatus.Completed));

            quests.Dispose();
            events.Dispose();
            Object.DestroyImmediate(quest);
            Object.DestroyImmediate(condition);
        }

        [Test]
        public void EquipmentQuest_RequiresActualDoubleJumpSignal()
        {
            var events = new GameEventBus();
            var package = CreateCondition("COND-PACKAGE", QuestSignalType.PortalUsed, "CRYON-EQUIPMENT-PACKAGE");
            var moduleTree = CreateCondition("COND-TREE", QuestSignalType.ModuleTreeOpened, "TREE-BASIC-001");
            var doubleJump = CreateCondition("COND-DOUBLE-JUMP", QuestSignalType.DoubleJumpPerformed, "PLAYER-001");
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureIdentity("QST-TUTO-006");
            quest.Conditions = new[] { package, moduleTree, doubleJump };
            var quests = new QuestManager(events);
            quests.Register(quest);

            Assert.That(quests.Start(quest.StableId), Is.True);
            events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, "CRYON-EQUIPMENT-PACKAGE"));
            events.Publish(new GameplaySignal(QuestSignalType.ModuleTreeOpened, "TREE-BASIC-001"));
            Assert.That(quests.TryGetState(quest.StableId, out var beforeJump), Is.True);
            Assert.That(beforeJump.Status, Is.EqualTo(QuestRuntimeStatus.InProgress));
            Assert.That(quests.GetConditionProgress(quest.StableId, doubleJump.StableId), Is.Zero);

            events.Publish(new GameplaySignal(QuestSignalType.DoubleJumpPerformed, "PLAYER-001"));
            Assert.That(quests.TryGetState(quest.StableId, out var afterJump), Is.True);
            Assert.That(afterJump.Status, Is.EqualTo(QuestRuntimeStatus.Completed));
            Assert.That(quests.GetConditionProgress(quest.StableId, doubleJump.StableId), Is.EqualTo(1));

            quests.Dispose();
            events.Dispose();
            Object.DestroyImmediate(quest);
            Object.DestroyImmediate(package);
            Object.DestroyImmediate(moduleTree);
            Object.DestroyImmediate(doubleJump);
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
        public void TutorialHudModeResolver_UsesResultDialogueBossPriority()
        {
            Assert.That(TutorialHudModeResolver.Resolve(false, false, false, false), Is.EqualTo(TutorialHudMode.Normal));
            Assert.That(TutorialHudModeResolver.Resolve(false, false, false, true), Is.EqualTo(TutorialHudMode.BossCombat));
            Assert.That(TutorialHudModeResolver.Resolve(false, true, false, true), Is.EqualTo(TutorialHudMode.Dialogue));
            Assert.That(TutorialHudModeResolver.Resolve(false, false, true, true), Is.EqualTo(TutorialHudMode.Dialogue));
            Assert.That(TutorialHudModeResolver.Resolve(true, true, true, true), Is.EqualTo(TutorialHudMode.Result));
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
