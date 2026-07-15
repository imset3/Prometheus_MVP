using System;
using System.Collections.Generic;

namespace Narthex.Core
{
    public sealed class GameEventBus : IDisposable
    {
        private readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            handlers.TryGetValue(typeof(T), out var current);
            handlers[typeof(T)] = Delegate.Combine(current, handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            if (!handlers.TryGetValue(typeof(T), out var current)) return;
            var next = Delegate.Remove(current, handler);
            if (next == null) handlers.Remove(typeof(T));
            else handlers[typeof(T)] = next;
        }

        public void Publish<T>(T message)
        {
            if (handlers.TryGetValue(typeof(T), out var current))
                (current as Action<T>)?.Invoke(message);
        }

        public void Dispose() => handlers.Clear();
    }
}
