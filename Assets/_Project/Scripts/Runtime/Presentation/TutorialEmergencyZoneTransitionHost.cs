using System.Collections;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Handles the training-room evacuation as a hard blackout with running footsteps,
    /// then restores the reused corridor in its second-visit direction.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialEmergencyZoneTransitionHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialDialoguePresenter dialoguePresenter;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private PlayerMotorHost playerMotor;
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private TutorialGuideCompanionHost guideCompanion;
        [SerializeField] private CameraFollowHost cameraFollowHost;
        [SerializeField] private TutorialObjectiveBeaconHost objectiveBeacon;
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private AudioSource runningAudioSource;
        [SerializeField] private AudioClip runningFootstepClip;

        [Header("Zones")]
        [SerializeField] private GameObject currentZoneRoot;
        [SerializeField] private GameObject nextZoneRoot;
        [SerializeField] private Transform destinationSpawn;
        [SerializeField] private Transform corridorExitTarget;
        [SerializeField] private string requiredQuestId = "QST-TUTO-007";
        [SerializeField] private string portalSignalTargetId = "TUTORIAL-TRAINING-EMERGENCY-TO-C02";
        [SerializeField] private float destinationCameraMinX = 118.5f;
        [SerializeField] private float destinationCameraMaxX = 141.5f;
        [SerializeField] private float destinationCameraY;

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float fadeOutDuration = 0.22f;
        [SerializeField, Min(0.2f)] private float blackoutRunDuration = 1.45f;
        [SerializeField, Min(0.05f)] private float fadeInDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float fallbackFootstepInterval = 0.24f;

        private Collider2D trigger;
        private bool transitionRunning;
        private AudioClip generatedFootstep;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && dialoguePresenter != null &&
                                     playerInputHost != null && playerMotor != null && player != null &&
                                     playerBody != null && guideCompanion != null && cameraFollowHost != null &&
                                     objectiveBeacon != null && fadeCanvasGroup != null && runningAudioSource != null &&
                                     currentZoneRoot != null && nextZoneRoot != null && destinationSpawn != null &&
                                     corridorExitTarget != null && !string.IsNullOrWhiteSpace(requiredQuestId) &&
                                     destinationCameraMinX <= destinationCameraMaxX;
        public bool UsesBlackoutRun => true;

        private void Awake()
        {
            trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialEmergencyZoneTransitionHost requires player, dialogue, fade, audio, zone, and camera references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            generatedFootstep = CreateFallbackFootstep();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (transitionRunning || dialoguePresenter.IsShowing ||
                questSequenceHost.CurrentQuestId != requiredQuestId || !IsPlayer(other))
                return;
            StartCoroutine(TransitionRoutine());
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            OnTriggerEnter2D(other);
        }

        private bool IsPlayer(Collider2D other)
        {
            if (other == null) return false;
            var candidate = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
            return candidate == player || candidate.IsChildOf(player) || player.IsChildOf(candidate);
        }

        private IEnumerator TransitionRoutine()
        {
            transitionRunning = true;
            playerMotor.ResetTransientInput();
            playerInputHost.AcquireInputLock(PlayerInputLockReason.Transition);
            playerBody.linearVelocity = Vector2.zero;
            fadeCanvasGroup.blocksRaycasts = true;

            yield return FadeTo(1f, fadeOutDuration);
            yield return PlayRunningFootsteps();

            nextZoneRoot.SetActive(true);
            playerBody.position = destinationSpawn.position;
            player.position = destinationSpawn.position;
            playerBody.linearVelocity = Vector2.zero;
            guideCompanion.CancelGuide();
            guideCompanion.transform.position = destinationSpawn.position + new Vector3(1.1f, 1.1f, 0f);
            cameraFollowHost.SetBounds(
                destinationCameraMinX,
                destinationCameraMaxX,
                destinationCameraY,
                true);
            objectiveBeacon.SetExternalTarget(corridorExitTarget);
            Physics2D.SyncTransforms();
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, portalSignalTargetId));
            serviceRoot.Events.Publish(new TutorialLocationChanged(nextZoneRoot.name));

            yield return FadeTo(0f, fadeInDuration);
            currentZoneRoot.SetActive(false);
            fadeCanvasGroup.blocksRaycasts = false;
            playerInputHost.ReleaseInputLock(PlayerInputLockReason.Transition);
            transitionRunning = false;
        }

        private IEnumerator PlayRunningFootsteps()
        {
            var elapsed = 0f;
            var nextStepAt = 0f;
            while (elapsed < blackoutRunDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed >= nextStepAt)
                {
                    runningAudioSource.PlayOneShot(
                        runningFootstepClip != null ? runningFootstepClip : generatedFootstep);
                    nextStepAt += fallbackFootstepInterval;
                }
                yield return null;
            }
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            var start = fadeCanvasGroup.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            fadeCanvasGroup.alpha = targetAlpha;
        }

        private static AudioClip CreateFallbackFootstep()
        {
            const int sampleRate = 22050;
            const float duration = 0.09f;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            var random = new System.Random(2718);
            for (var index = 0; index < sampleCount; index++)
            {
                var progress = index / (float)sampleCount;
                var envelope = Mathf.Pow(1f - progress, 3f);
                var lowThump = Mathf.Sin(progress * Mathf.PI * 7f) * 0.28f;
                var noise = ((float)random.NextDouble() * 2f - 1f) * 0.12f;
                samples[index] = (lowThump + noise) * envelope;
            }
            var clip = AudioClip.Create("SFX_Footstep_Run_Placeholder", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
