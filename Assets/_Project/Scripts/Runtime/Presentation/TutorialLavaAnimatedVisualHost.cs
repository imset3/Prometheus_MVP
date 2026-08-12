using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Lightweight replaceable art layer for lava hazards. The hazard collider and
    /// damage logic remain on the parent; this host only loops authored sprites.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TutorialLavaAnimatedVisualHost : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Sprite[] frames = System.Array.Empty<Sprite>();
        [SerializeField, Min(1f)] private float framesPerSecond = 6f;

        private void Awake()
        {
            targetRenderer ??= GetComponent<SpriteRenderer>();
            ApplyFrame();
        }

        private void OnEnable() => ApplyFrame();

        private void Update() => ApplyFrame();

        private void ApplyFrame()
        {
            if (targetRenderer == null || frames == null || frames.Length == 0) return;
            var index = Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length;
            targetRenderer.sprite = frames[index];
        }
    }
}
