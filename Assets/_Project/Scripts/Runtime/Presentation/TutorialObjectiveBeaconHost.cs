using System;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    [Serializable]
    public sealed class TutorialObjectiveBeaconTarget
    {
        [SerializeField] private string questId;
        [SerializeField] private Transform target;

        public string QuestId => questId;
        public Transform Target => target;
    }

    /// <summary>
    /// Drives a pre-placed world-space objective beacon. The visual can be replaced later
    /// without changing quest progression or target bindings.
    /// </summary>
    public sealed class TutorialObjectiveBeaconHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private Transform player;
        [SerializeField] private GameObject beaconVisual;
        [SerializeField] private TutorialObjectiveBeaconTarget[] targets = Array.Empty<TutorialObjectiveBeaconTarget>();
        [SerializeField] private Vector3 visualOffset = new(0f, 1.6f, 0f);
        [SerializeField, Min(0f)] private float hideDistance = 2.25f;

        private Transform currentTarget;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && player != null && beaconVisual != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialObjectiveBeaconHost requires pre-placed service, quest, player, and visual references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void Start()
        {
            SetTarget(questSequenceHost.CurrentQuestId);
        }

        private void LateUpdate()
        {
            if (currentTarget == null)
            {
                SetVisualActive(false);
                return;
            }

            transform.position = currentTarget.position + visualOffset;
            SetVisualActive(Vector3.Distance(player.position, currentTarget.position) > hideDistance);
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            SetTarget(message.QuestId);
        }

        private void SetTarget(string questId)
        {
            currentTarget = null;
            foreach (var candidate in targets)
            {
                if (candidate != null && candidate.Target != null && candidate.QuestId == questId)
                {
                    currentTarget = candidate.Target;
                    break;
                }
            }

            SetVisualActive(currentTarget != null && Vector3.Distance(player.position, currentTarget.position) > hideDistance);
        }

        private void SetVisualActive(bool active)
        {
            if (beaconVisual != null && beaconVisual.activeSelf != active)
                beaconVisual.SetActive(active);
        }
    }
}
