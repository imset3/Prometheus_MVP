using System;
using System.Collections;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Briefly holds the camera on the exterior invasion view while player movement remains enabled.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialExteriorInvasionViewHost : MonoBehaviour
    {
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialLoreSubtitlePresenter subtitlePresenter;
        [SerializeField] private CameraFollowHost cameraFollowHost;
        [SerializeField] private TutorialObjectiveBeaconHost objectiveBeacon;
        [SerializeField] private Transform player;
        [SerializeField] private Transform viewAnchor;
        [SerializeField] private Transform nextTarget;
        [SerializeField] private string requiredQuestId = "QST-TUTO-007";
        [SerializeField, TextArea(2, 4)] private string[] subtitles = Array.Empty<string>();
        [SerializeField, Min(0.2f)] private float cameraHoldDuration = 1.8f;
        [SerializeField] private float restoreCameraMinX = 246f;
        [SerializeField] private float restoreCameraMaxX = 274f;
        [SerializeField] private float restoreCameraY;
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.16f;
        [SerializeField, Min(0f)] private float shakeDuration = 0.38f;

        private Collider2D triggerCollider;
        private bool presented;
        private Vector2 previousPlayerPosition;
        private bool hasPreviousPlayerPosition;

        public bool HasValidSetup => questSequenceHost != null && subtitlePresenter != null &&
                                     cameraFollowHost != null && objectiveBeacon != null && player != null &&
                                     viewAnchor != null && nextTarget != null &&
                                     !string.IsNullOrWhiteSpace(requiredQuestId) &&
                                     subtitles != null && subtitles.Length > 0 &&
                                     restoreCameraMinX <= restoreCameraMaxX &&
                                     GetComponent<Collider2D>() is Collider2D candidate && candidate.isTrigger;
        public bool PreservesPlayerControl => true;
        public int SubtitleCount => subtitles?.Length ?? 0;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null) triggerCollider.isTrigger = true;
            previousPlayerPosition = player != null ? player.position : Vector2.zero;
            hasPreviousPlayerPosition = player != null;
            if (!HasValidSetup)
            {
                Debug.LogError(
                    "TutorialExteriorInvasionViewHost requires quest, subtitle, camera, player, view, objective, and trigger references.",
                    this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (presented || player == null || triggerCollider == null) return;
            var currentPosition = (Vector2)player.position;
            if (hasPreviousPlayerPosition && questSequenceHost.CurrentQuestId == requiredQuestId &&
                TutorialTriggerSweepPolicy.Intersects(
                    triggerCollider.bounds,
                    previousPlayerPosition,
                    currentPosition))
                BeginPresentation();
            previousPlayerPosition = currentPosition;
            hasPreviousPlayerPosition = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (presented || other == null || questSequenceHost.CurrentQuestId != requiredQuestId) return;
            var candidate = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
            if (candidate != player && !candidate.IsChildOf(player) && !player.IsChildOf(candidate)) return;
            BeginPresentation();
        }

        private void BeginPresentation()
        {
            if (presented) return;
            presented = true;
            triggerCollider.enabled = false;
            StartCoroutine(PresentationRoutine());
        }

        private IEnumerator PresentationRoutine()
        {
            cameraFollowHost.SetBounds(viewAnchor.position.x, viewAnchor.position.x, viewAnchor.position.y, true);
            if (shakeAmplitude > 0f && shakeDuration > 0f)
                cameraFollowHost.RequestShake(shakeAmplitude, shakeDuration);
            foreach (var subtitle in subtitles)
                subtitlePresenter.ShowSubtitle(subtitle);

            yield return new WaitForSecondsRealtime(cameraHoldDuration);
            cameraFollowHost.SetBounds(restoreCameraMinX, restoreCameraMaxX, restoreCameraY, false);
            objectiveBeacon.SetExternalTarget(nextTarget);
        }
    }
}
