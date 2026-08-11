using UnityEngine;

namespace Narthex.Presentation
{
    [DisallowMultipleComponent]
    public sealed class SpriteFrameBlendHost : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private SpriteRenderer blendRenderer;
        [SerializeField, Range(0.01f, 0.1f)] private float blendSeconds = 0.04f;

        private Sprite previousSprite;
        private bool previousFlipX;
        private float blendStartedAt;
        private bool isBlending;

        public void Configure(SpriteRenderer source, SpriteRenderer overlay, float duration)
        {
            sourceRenderer = source;
            blendRenderer = overlay;
            blendSeconds = Mathf.Clamp(duration, 0.01f, 0.1f);
            Synchronize(true);
        }

        private void Awake() => Synchronize(true);

        private void OnEnable() => Synchronize(true);

        private void LateUpdate()
        {
            if (sourceRenderer == null || blendRenderer == null) return;

            var current = sourceRenderer.sprite;
            if (current != previousSprite)
            {
                if (previousSprite != null)
                {
                    CopyRendererSettings();
                    blendRenderer.sprite = previousSprite;
                    blendRenderer.flipX = previousFlipX;
                    blendStartedAt = Time.unscaledTime;
                    isBlending = true;
                }
                previousSprite = current;
            }
            previousFlipX = sourceRenderer.flipX;

            if (!isBlending) return;
            var progress = Mathf.Clamp01((Time.unscaledTime - blendStartedAt) / blendSeconds);
            var color = sourceRenderer.color;
            color.a *= 1f - progress;
            blendRenderer.color = color;
            if (progress < 1f) return;
            blendRenderer.sprite = null;
            isBlending = false;
        }

        private void Synchronize(bool clearOverlay)
        {
            if (sourceRenderer == null) sourceRenderer = GetComponent<SpriteRenderer>();
            previousSprite = sourceRenderer == null ? null : sourceRenderer.sprite;
            previousFlipX = sourceRenderer != null && sourceRenderer.flipX;
            if (clearOverlay && blendRenderer != null)
            {
                blendRenderer.sprite = null;
                blendRenderer.color = Color.clear;
            }
            isBlending = false;
        }

        private void CopyRendererSettings()
        {
            blendRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            blendRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            blendRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            blendRenderer.maskInteraction = sourceRenderer.maskInteraction;
            blendRenderer.flipY = sourceRenderer.flipY;
        }
    }
}
