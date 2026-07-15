using System;
using Narthex.Core;

namespace Narthex.Save
{
    public sealed class SaveSystem : IDisposable
    {
        private readonly GameEventBus events;
        private readonly SaveFileStore store;
        public SaveData Current { get; private set; }

        public SaveSystem(GameEventBus events, SaveFileStore store)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Current = new SaveData();
            events.Subscribe<SaveRequested>(OnSaveRequested);
        }

        public void Load() { Current = store.Load(); }

        public void Save(string reason)
        {
            try
            {
                store.Save(Current);
                events.Publish(new SaveCompleted(reason));
            }
            catch (Exception exception)
            {
                events.Publish(new SaveFailed(reason, exception.Message));
            }
        }

        private void OnSaveRequested(SaveRequested request) { Save(request.Reason); }
        public void Dispose() { events.Unsubscribe<SaveRequested>(OnSaveRequested); }
    }
}
