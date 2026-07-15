using UnityEngine;

namespace Narthex.Core
{
    public sealed class GameClock
    {
        public float ScaledDeltaTime => Time.deltaTime;
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;
        public float FixedDeltaTime => Time.fixedDeltaTime;
    }
}
