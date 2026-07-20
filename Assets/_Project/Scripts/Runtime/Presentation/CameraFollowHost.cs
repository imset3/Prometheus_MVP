using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    public static class TutorialCameraPolicy
    {
        public static float ResolveLookAhead(float horizontalVelocity, float distance, float minimumSpeed)
        {
            if (Mathf.Abs(horizontalVelocity) < minimumSpeed) return 0f;
            return Mathf.Sign(horizontalVelocity) * Mathf.Max(0f, distance);
        }

        public static float ResolveBossCenter(float playerX, float bossX, float bossWeight)
        {
            return Mathf.Lerp(playerX, bossX, Mathf.Clamp01(bossWeight));
        }
    }

    /// <summary>
    /// Camera controller for the single-scene tutorial. It keeps the player movement direction readable,
    /// frames both actors during the Helte encounter, and owns a bounded accessibility-scaled shake channel.
    /// </summary>
    public sealed class CameraFollowHost : MonoBehaviour
    {
        [Header("Core references")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private Transform target;
        [SerializeField] private Rigidbody2D targetBody;
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private TutorialBossArenaHost bossArenaHost;
        [SerializeField] private Transform bossFocus;

        [Header("Follow")]
        [SerializeField, Min(0f)] private float followSpeed = 14f;
        [SerializeField, Min(0f)] private float verticalFollowSpeed = 8f;
        [SerializeField, Min(0f)] private float verticalDeadZone = 0.65f;
        [SerializeField, Min(0f)] private float lookAheadDistance = 2f;
        [SerializeField, Min(0f)] private float lookAheadResponse = 8f;
        [SerializeField, Min(0f)] private float lookAheadMinimumSpeed = 0.2f;
        [SerializeField, Range(0f, 1f)] private float bossFramingWeight = 0.45f;

        [Header("Bounds")]
        [SerializeField] private float minX = -41f;
        [SerializeField] private float maxX = 41f;
        [SerializeField] private float fixedY;
        [SerializeField] private bool followVertical;
        [SerializeField] private float minY;
        [SerializeField] private float maxY;
        [SerializeField] private float fixedZ = -10f;

        [Header("Lens")]
        [SerializeField, Min(0.1f)] private float normalOrthographicSize = 5f;
        [SerializeField, Min(0.1f)] private float bossOrthographicSize = 6.25f;
        [SerializeField, Min(0.1f)] private float lensChangeSpeed = 4f;

        [Header("Motion feedback")]
        [SerializeField, Range(0f, 1f)] private float motionIntensity = 0.65f;
        [SerializeField, Min(0f)] private float maximumShakeDistance = 0.2f;

        private Vector2 currentCenter;
        private float currentLookAhead;
        private float shakeAmplitude;
        private float shakeDuration;
        private float shakeTimeRemaining;

        public bool HasValidSetup => serviceRoot != null && target != null && minX <= maxX &&
                                     (!followVertical || minY <= maxY);
        public bool HasReviewSetup => HasValidSetup && targetBody != null && controlledCamera != null &&
                                      bossArenaHost != null && bossFocus != null &&
                                      normalOrthographicSize < bossOrthographicSize;
        public float NormalOrthographicSize => normalOrthographicSize;
        public float BossOrthographicSize => bossOrthographicSize;
        public float MotionIntensity => motionIntensity;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("CameraFollowHost requires a target and valid horizontal bounds.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (controlledCamera == null) controlledCamera = GetComponent<Camera>();
            SnapToTarget();
        }

        private void OnEnable()
        {
            serviceRoot?.Initialize();
            serviceRoot?.Events?.Subscribe<PlayerRespawned>(HandlePlayerRespawned);
            serviceRoot?.Events?.Subscribe<PlayerHit>(HandlePlayerHit);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<PlayerRespawned>(HandlePlayerRespawned);
            serviceRoot?.Events?.Unsubscribe<PlayerHit>(HandlePlayerHit);
        }

        private void Start() => SnapToTarget();

        private void LateUpdate()
        {
            if (target == null) return;

            var velocityX = targetBody != null ? targetBody.linearVelocity.x : 0f;
            var lookAheadTarget = TutorialCameraPolicy.ResolveLookAhead(
                velocityX,
                lookAheadDistance,
                lookAheadMinimumSpeed);
            currentLookAhead = Mathf.MoveTowards(
                currentLookAhead,
                lookAheadTarget,
                lookAheadResponse * Time.unscaledDeltaTime);

            var focusX = target.position.x;
            if (bossArenaHost != null && bossArenaHost.CombatActive && bossFocus != null)
                focusX = TutorialCameraPolicy.ResolveBossCenter(target.position.x, bossFocus.position.x, bossFramingWeight);

            var desiredX = Mathf.Clamp(focusX + currentLookAhead, minX, maxX);
            var desiredY = GetDesiredY(false);
            currentCenter.x = Mathf.MoveTowards(currentCenter.x, desiredX, followSpeed * Time.unscaledDeltaTime);
            currentCenter.y = Mathf.MoveTowards(currentCenter.y, desiredY, verticalFollowSpeed * Time.unscaledDeltaTime);

            UpdateLens();
            var shakeOffset = ResolveShakeOffset();
            transform.position = new Vector3(
                currentCenter.x + shakeOffset.x,
                currentCenter.y + shakeOffset.y,
                fixedZ);
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            currentLookAhead = 0f;
            currentCenter = new Vector2(Mathf.Clamp(target.position.x, minX, maxX), GetDesiredY(true));
            transform.position = new Vector3(currentCenter.x, currentCenter.y, fixedZ);
        }

        public void RequestShake(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f || motionIntensity <= 0f) return;
            shakeAmplitude = Mathf.Min(maximumShakeDistance, Mathf.Max(shakeAmplitude, amplitude));
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
        }

        public void SetMotionIntensity(float intensity) => motionIntensity = Mathf.Clamp01(intensity);

        public void SetBounds(float nextMinX, float nextMaxX, float nextFixedY, bool snap = true)
        {
            if (nextMinX > nextMaxX)
            {
                Debug.LogError("CameraFollowHost received an invalid horizontal range.", this);
                return;
            }

            minX = nextMinX;
            maxX = nextMaxX;
            fixedY = nextFixedY;
            followVertical = false;
            if (snap) SnapToTarget();
        }

        public void SetTrackingBounds(
            float nextMinX,
            float nextMaxX,
            float nextMinY,
            float nextMaxY,
            bool snap = true)
        {
            if (nextMinX > nextMaxX || nextMinY > nextMaxY)
            {
                Debug.LogError("CameraFollowHost received an invalid tracking range.", this);
                return;
            }

            minX = nextMinX;
            maxX = nextMaxX;
            minY = nextMinY;
            maxY = nextMaxY;
            followVertical = true;
            if (snap) SnapToTarget();
        }

        private float GetDesiredY(bool ignoreDeadZone)
        {
            if (!followVertical) return fixedY;
            var rawTarget = Mathf.Clamp(target.position.y, minY, maxY);
            if (!ignoreDeadZone && Mathf.Abs(rawTarget - currentCenter.y) <= verticalDeadZone)
                return currentCenter.y;
            return rawTarget;
        }

        private void UpdateLens()
        {
            if (controlledCamera == null || !controlledCamera.orthographic) return;
            var targetSize = bossArenaHost != null && bossArenaHost.CombatActive
                ? bossOrthographicSize
                : normalOrthographicSize;
            controlledCamera.orthographicSize = Mathf.MoveTowards(
                controlledCamera.orthographicSize,
                targetSize,
                lensChangeSpeed * Time.unscaledDeltaTime);
        }

        private Vector2 ResolveShakeOffset()
        {
            if (shakeTimeRemaining <= 0f) return Vector2.zero;
            shakeTimeRemaining = Mathf.Max(0f, shakeTimeRemaining - Time.unscaledDeltaTime);
            var progress = shakeDuration <= 0f ? 0f : shakeTimeRemaining / shakeDuration;
            if (shakeTimeRemaining <= 0f)
            {
                shakeAmplitude = 0f;
                shakeDuration = 0f;
                return Vector2.zero;
            }
            return Random.insideUnitCircle * (shakeAmplitude * progress * motionIntensity);
        }

        private void HandlePlayerRespawned(PlayerRespawned message) => SnapToTarget();
        private void HandlePlayerHit(PlayerHit message) => RequestShake(0.14f, 0.14f);
    }
}
