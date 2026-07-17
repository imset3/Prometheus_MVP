using NUnit.Framework;
using Narthex.Core;
using Narthex.Save;
using Narthex.SceneFlow;
using System;
using System.IO;

namespace Narthex.Tests
{
    public sealed class CoreAndSaveTests
    {
        [Test]
        public void Chapter01Transition_RequiresTutorialCompletionAndMatchingStage()
        {
            var permanent = new PermanentSaveData();
            var run = new RunSaveData { CurrentStageId = "CHAPTER_01" };

            Assert.That(Chapter01TransitionPolicy.CanEnter(permanent, run, "CHAPTER_01"), Is.False);
            permanent.TutorialCompleted = true;
            Assert.That(Chapter01TransitionPolicy.CanEnter(permanent, run, "CHAPTER_01"), Is.True);
            Assert.That(Chapter01TransitionPolicy.CanEnter(permanent, run, "CHAPTER_02"), Is.False);
        }

        [Test]
        public void StateMachine_RejectsInvalidTransition()
        {
            var events = new GameEventBus();
            var machine = new GameStateMachine(events);

            Assert.That(machine.TryTransition(GameState.InRun), Is.False);
            Assert.That(machine.Current, Is.EqualTo(GameState.Booting));
            events.Dispose();
        }

        [Test]
        public void SaveSerializer_RoundTripsPermanentAndRunData()
        {
            var save = new SaveData();
            save.Permanent.TutorialCompleted = true;
            save.Permanent.UnlockedTreeIds.Add("TREE-BASIC-001");
            save.Run.CurrentStageId = "STG-001";
            save.Run.ModulePoints = 3;

            var restored = SaveSerializer.FromJson(SaveSerializer.ToJson(save));

            Assert.That(restored.Permanent.TutorialCompleted, Is.True);
            Assert.That(restored.Permanent.UnlockedTreeIds[0], Is.EqualTo("TREE-BASIC-001"));
            Assert.That(restored.Run.CurrentStageId, Is.EqualTo("STG-001"));
            Assert.That(restored.Run.ModulePoints, Is.EqualTo(3));
        }

        [Test]
        public void MigrationRunner_AdvancesSaveVersion()
        {
            var save = new SaveData();
            var runner = new MigrationRunner();
            runner.Register(new TestMigration(1, 2));

            runner.Run(save, 2);

            Assert.That(save.Permanent.SaveVersion, Is.EqualTo(2));
        }

        [Test]
        public void SaveFileStore_ReturnsFreshDataForCorruptSave()
        {
            var path = Path.Combine(Path.GetTempPath(), $"narthex-corrupt-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, "{not-valid-json");

            try
            {
                var restored = new SaveFileStore(path).Load();

                Assert.That(restored, Is.Not.Null);
                Assert.That(restored.Permanent.TutorialCompleted, Is.False);
                Assert.That(restored.Run.RunNumber, Is.EqualTo(0));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void SaveSystem_ResetProgressForSceneStart_ClearsProgressAndKeepsSettings()
        {
            var path = Path.Combine(Path.GetTempPath(), $"narthex-scene-reset-{Guid.NewGuid():N}.json");
            var events = new GameEventBus();
            var system = new SaveSystem(events, new SaveFileStore(path));

            try
            {
                system.Current.Permanent.TutorialCompleted = true;
                system.Current.Permanent.DoubleJumpUnlocked = true;
                system.Current.Run.QuestIds.Add("QST-TUTO-008");
                system.Current.Run.ModulePoints = 4;
                system.Current.Settings.MasterVolume = 0.45f;
                system.Save("TestSeed");

                system.ResetProgressForSceneStart();
                var restored = new SaveFileStore(path).Load();

                Assert.That(system.Current.Permanent.TutorialCompleted, Is.False);
                Assert.That(system.Current.Permanent.DoubleJumpUnlocked, Is.False);
                Assert.That(system.Current.Run.QuestIds, Is.Empty);
                Assert.That(system.Current.Run.ModulePoints, Is.EqualTo(0));
                Assert.That(system.Current.Settings.MasterVolume, Is.EqualTo(0.45f));
                Assert.That(restored.Permanent.TutorialCompleted, Is.False);
                Assert.That(restored.Run.QuestIds, Is.Empty);
                Assert.That(restored.Settings.MasterVolume, Is.EqualTo(0.45f));
            }
            finally
            {
                system.Dispose();
                events.Dispose();
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private sealed class TestMigration : ISaveMigration
        {
            public int FromVersion { get; }
            public int ToVersion { get; }

            public TestMigration(int fromVersion, int toVersion)
            {
                FromVersion = fromVersion;
                ToVersion = toVersion;
            }

            public void Apply(SaveData data) { }
        }
    }
}
