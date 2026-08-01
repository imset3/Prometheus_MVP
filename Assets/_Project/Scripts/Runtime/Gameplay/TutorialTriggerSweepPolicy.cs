using UnityEngine;

namespace Narthex.Gameplay
{
    public static class TutorialTriggerSweepPolicy
    {
        public static bool Intersects(Bounds bounds, Vector2 start, Vector2 end)
        {
            var minimum = (Vector2)bounds.min;
            var maximum = (Vector2)bounds.max;
            var delta = end - start;
            var minimumTime = 0f;
            var maximumTime = 1f;
            return IntersectsAxis(start.x, delta.x, minimum.x, maximum.x, ref minimumTime, ref maximumTime) &&
                   IntersectsAxis(start.y, delta.y, minimum.y, maximum.y, ref minimumTime, ref maximumTime);
        }

        private static bool IntersectsAxis(
            float start, float delta, float minimum, float maximum, ref float minimumTime, ref float maximumTime)
        {
            if (Mathf.Approximately(delta, 0f)) return start >= minimum && start <= maximum;
            var inverse = 1f / delta;
            var first = (minimum - start) * inverse;
            var second = (maximum - start) * inverse;
            if (first > second) (first, second) = (second, first);
            minimumTime = Mathf.Max(minimumTime, first);
            maximumTime = Mathf.Min(maximumTime, second);
            return minimumTime <= maximumTime;
        }
    }
}
