using System;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Marker-relative vertical wind presentation. Moving a wind marker moves both
    /// gameplay lift and its visual machine/streaks without a code change.
    /// </summary>
    public sealed class TutorialUpdraftVisualHost : MonoBehaviour
    {
        [SerializeField] private Transform[] streaks = Array.Empty<Transform>();
        [SerializeField] private SpriteRenderer[] streakRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField] private float bottomY = -5f;
        [SerializeField] private float topY = 5f;
        [SerializeField, Min(0.1f)] private float riseSpeed = 4.5f;
        [SerializeField, Min(0f)] private float swayAmount = 0.16f;
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.22f;

        private Vector3[] bases = Array.Empty<Vector3>();
        private float[] offsets = Array.Empty<float>();

        public bool HasValidSetup => streaks != null && streakRenderers != null &&
                                     streaks.Length >= 5 && streaks.Length == streakRenderers.Length &&
                                     Array.TrueForAll(streaks, item => item != null) &&
                                     Array.TrueForAll(streakRenderers, item => item != null) && topY > bottomY;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialUpdraftVisualHost requires marker-authored wind streaks.", this);
                enabled = false;
                return;
            }

            bases = new Vector3[streaks.Length];
            offsets = new float[streaks.Length];
            var span = topY - bottomY;
            for (var index = 0; index < streaks.Length; index++)
            {
                bases[index] = streaks[index].localPosition;
                offsets[index] = span * index / streaks.Length;
            }
        }

        private void Update()
        {
            var span = topY - bottomY;
            for (var index = 0; index < streaks.Length; index++)
            {
                var phase = Mathf.Repeat(Time.time * riseSpeed + offsets[index], span);
                var normalized = phase / span;
                var basePosition = bases[index];
                var sway = Mathf.Sin(Time.time * 2.4f + index * 1.7f) * swayAmount;
                streaks[index].localPosition = new Vector3(basePosition.x + sway, bottomY + phase, basePosition.z);
                var alpha = Mathf.Sin(normalized * Mathf.PI) * peakAlpha;
                var color = streakRenderers[index].color;
                color.a = alpha;
                streakRenderers[index].color = color;
            }
        }
    }
}
