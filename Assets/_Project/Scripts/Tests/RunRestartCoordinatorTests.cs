using NUnit.Framework;
using Narthex.Core;
using Narthex.Save;
using Narthex.SceneFlow;

namespace Narthex.Tests
{
    public sealed class RunRestartCoordinatorTests
    {
        [Test]
        public void PlayerDeath_ResetsRunAndPreservesPermanentData()
        {
            var events = new GameEventBus();
            var context = new RunContext();
            var permanent = new PermanentSaveData();
            var coordinator = new RunRestartCoordinator(events, context, permanent, "STG-001");

            events.Publish(new PlayerDead("PLAYER", "Test", "STG-003"));

            Assert.That(permanent.TotalDeaths, Is.EqualTo(1));
            Assert.That(context.Data.CurrentStageId, Is.EqualTo("STG-001"));
            Assert.That(context.Data.Level, Is.EqualTo(1));
            Assert.That(context.Data.RunNumber, Is.EqualTo(1));

            coordinator.Dispose();
            events.Dispose();
        }

        [Test]
        public void PlayerDeath_IsIgnoredAfterCoordinatorDispose()
        {
            var events = new GameEventBus();
            var context = new RunContext();
            var permanent = new PermanentSaveData();
            var coordinator = new RunRestartCoordinator(events, context, permanent, "STG-001");
            coordinator.Dispose();

            events.Publish(new PlayerDead("PLAYER", "Test", "STG-003"));

            Assert.That(permanent.TotalDeaths, Is.EqualTo(0));
            Assert.That(context.Data.RunNumber, Is.EqualTo(0));
            events.Dispose();
        }
    }
}
