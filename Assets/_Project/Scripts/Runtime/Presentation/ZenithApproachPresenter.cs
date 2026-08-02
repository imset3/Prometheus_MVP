using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Keeps Zenith in camera space while its apparent distance changes continuously
    /// from the player's world X position. The sky/backplate remains independent so
    /// location changes cannot introduce a colour or scale jump.
    /// </summary>
    [ExecuteAlways]
    public sealed class ZenithApproachPresenter : MonoBehaviour
    {
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private SpriteRenderer zenithRenderer;
        [SerializeField] private float startWorldX = 239f;
        [SerializeField] private float endWorldX = 867.87f;
        [SerializeField] private Vector2 farViewportAnchor = new(0.80f, 0.70f);
        [SerializeField] private Vector2 nearViewportAnchor = new(0.70f, 0.58f);
        [SerializeField, Range(0.01f, 1f)] private float farScreenWidth = 0.14f;
        [SerializeField, Range(0.01f, 1f)] private float nearScreenWidth = 0.56f;
        [SerializeField, Range(0f, 1f)] private float farOpacity = 0.72f;
        [SerializeField, Range(0f, 1f)] private float nearOpacity = 1f;
        [SerializeField] private bool previewInEditMode;
        [SerializeField, Range(0f, 1f)] private float previewProgress;

        public Transform PlayerTarget => playerTarget;
        public Camera TargetCamera => targetCamera;
        public SpriteRenderer ZenithRenderer => zenithRenderer;
        public float StartWorldX => startWorldX;
        public float EndWorldX => endWorldX;
        public float CurrentProgress { get; private set; }

        private void OnEnable()
        {
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        public void Configure(
            Transform player,
            Camera camera,
            SpriteRenderer renderer,
            float startX,
            float endX,
            Vector2 farAnchor,
            Vector2 nearAnchor,
            float farWidth,
            float nearWidth,
            float startOpacity,
            float endOpacity)
        {
            playerTarget = player;
            targetCamera = camera;
            zenithRenderer = renderer;
            startWorldX = startX;
            endWorldX = Mathf.Max(startX + 0.01f, endX);
            farViewportAnchor = farAnchor;
            nearViewportAnchor = nearAnchor;
            farScreenWidth = Mathf.Clamp(farWidth, 0.01f, 1f);
            nearScreenWidth = Mathf.Clamp(nearWidth, 0.01f, 1f);
            farOpacity = Mathf.Clamp01(startOpacity);
            nearOpacity = Mathf.Clamp01(endOpacity);
            Refresh();
        }

        public static float CalculateProgress(float worldX, float startX, float endX)
        {
            if (endX <= startX) return worldX >= endX ? 1f : 0f;
            return Mathf.Clamp01(Mathf.InverseLerp(startX, endX, worldX));
        }

        public void RefreshForWorldX(float worldX)
        {
            if (targetCamera == null || zenithRenderer == null || zenithRenderer.sprite == null)
                return;

            var rawProgress = CalculateProgress(worldX, startWorldX, endWorldX);
            var easedProgress = Mathf.SmoothStep(0f, 1f, rawProgress);
            CurrentProgress = rawProgress;

            zenithRenderer.enabled = worldX >= startWorldX;
            if (!zenithRenderer.enabled) return;

            var cameraHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 2f * Mathf.Abs(transform.position.z - targetCamera.transform.position.z) *
                  Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var cameraWidth = cameraHeight * Mathf.Max(0.1f, targetCamera.aspect);
            var viewportAnchor = Vector2.Lerp(farViewportAnchor, nearViewportAnchor, easedProgress);
            var localPosition = new Vector3(
                (viewportAnchor.x - 0.5f) * cameraWidth,
                (viewportAnchor.y - 0.5f) * cameraHeight,
                0f);
            zenithRenderer.transform.localPosition = localPosition;
            zenithRenderer.transform.localRotation = Quaternion.identity;

            var targetWidth = cameraWidth * Mathf.Lerp(farScreenWidth, nearScreenWidth, easedProgress);
            var spriteWidth = Mathf.Max(0.0001f, zenithRenderer.sprite.bounds.size.x);
            var uniformScale = targetWidth / spriteWidth;
            zenithRenderer.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);

            var color = zenithRenderer.color;
            color.a = Mathf.Lerp(farOpacity, nearOpacity, easedProgress);
            zenithRenderer.color = color;
        }

        private void Refresh()
        {
            if (!Application.isPlaying && previewInEditMode)
            {
                RefreshForWorldX(Mathf.Lerp(startWorldX, endWorldX, previewProgress));
                return;
            }

            if (playerTarget == null)
            {
                var player = GameObject.Find("/TutorialRuntimeRoot/StageRoot/PlayerRoot");
                if (player != null) playerTarget = player.transform;
            }

            if (playerTarget == null)
            {
                if (zenithRenderer != null) zenithRenderer.enabled = false;
                return;
            }

            RefreshForWorldX(playerTarget.position.x);
        }
    }
}
