using UnityEngine;
using Narthex.Gameplay;

namespace Narthex.Presentation
{
    /// <summary>
    /// Starts the second G-stage enemy group only after the player crosses the
    /// newly opened internal passage.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialEncounterPhaseTriggerHost : MonoBehaviour
    {
        [SerializeField] private TutorialWaveEncounterHost encounter;
        [SerializeField] private Transform player;
        [SerializeField] private TutorialObjectiveBeaconHost objectiveBeacon;
        [SerializeField] private Transform nextObjectiveTarget;

        public bool HasValidSetup => encounter != null && player != null &&
                                     objectiveBeacon != null && nextObjectiveTarget != null;
        public bool HasDynamicGuidance => objectiveBeacon != null && nextObjectiveTarget != null;

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup) return;

            Debug.LogError(
                "TutorialEncounterPhaseTriggerHost requires encounter and player references.",
                this);
            enabled = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            TryAdvance();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            TryAdvance();
        }

        private void TryAdvance()
        {
            if (!encounter.IsWaitingForTraversal) return;
            encounter.TryAdvanceFromTraversal();
            if (!encounter.IsWaitingForTraversal)
                objectiveBeacon.SetExternalTarget(nextObjectiveTarget);
        }

        private bool IsPlayer(Collider2D other)
        {
            return other != null && (other.transform == player || other.transform.IsChildOf(player));
        }
    }
}
