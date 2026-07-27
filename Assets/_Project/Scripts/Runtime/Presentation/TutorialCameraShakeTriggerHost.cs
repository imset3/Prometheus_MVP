using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialCameraShakeTriggerHost : MonoBehaviour
    {
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private CameraFollowHost cameraFollowHost;
        [SerializeField] private Transform player;
        [SerializeField] private string requiredQuestId = "QST-TUTO-007";
        [SerializeField, Min(0.01f)] private float amplitude = 0.12f;
        [SerializeField, Min(0.01f)] private float duration = 0.32f;

        private Collider2D triggerCollider;
        private bool triggered;
        private Vector2 previousPlayerPosition;
        private bool hasPreviousPlayerPosition;

        public bool HasValidSetup => questSequenceHost != null && cameraFollowHost != null && player != null &&
                                     !string.IsNullOrWhiteSpace(requiredQuestId) &&
                                     GetComponent<Collider2D>() is Collider2D candidate && candidate.isTrigger;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null) triggerCollider.isTrigger = true;
            previousPlayerPosition = player != null ? player.position : Vector2.zero;
            hasPreviousPlayerPosition = player != null;
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialCameraShakeTriggerHost requires quest, camera, player, and trigger references.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (triggered || player == null || triggerCollider == null) return;
            var currentPosition = (Vector2)player.position;
            if (hasPreviousPlayerPosition && questSequenceHost.CurrentQuestId == requiredQuestId &&
                TutorialTriggerSweepPolicy.Intersects(
                    triggerCollider.bounds,
                    previousPlayerPosition,
                    currentPosition))
                Trigger();
            previousPlayerPosition = currentPosition;
            hasPreviousPlayerPosition = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggered || other == null || questSequenceHost.CurrentQuestId != requiredQuestId) return;
            var candidate = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
            if (candidate != player && !candidate.IsChildOf(player) && !player.IsChildOf(candidate)) return;
            Trigger();
        }

        private void Trigger()
        {
            if (triggered) return;
            triggered = true;
            cameraFollowHost.RequestShake(amplitude, duration);
            triggerCollider.enabled = false;
        }
    }
}
