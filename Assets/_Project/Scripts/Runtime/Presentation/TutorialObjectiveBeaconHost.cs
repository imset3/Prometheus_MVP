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
        [SerializeField] private QuestManagerHost questManagerHost;
        [SerializeField] private Transform player;
        [SerializeField] private GameObject beaconVisual;
        [SerializeField] private TutorialObjectiveBeaconTarget[] targets = Array.Empty<TutorialObjectiveBeaconTarget>();
        [Header("Equipment Quest Guidance")]
        [SerializeField] private TutorialBootsPickupHost equipmentPickupHost;
        [SerializeField] private Transform equipmentPickupTarget;
        [SerializeField] private Transform equipmentDoubleJumpTarget;
        [SerializeField] private string equipmentQuestId = "QST-TUTO-006";
        [SerializeField] private string doubleJumpConditionId = "COND-TUTO-006-DOUBLE-JUMP";
        [SerializeField] private Vector3 visualOffset = new(0f, 1.6f, 0f);
        [SerializeField, Min(0f)] private float hideDistance = 2.25f;

        private Transform currentTarget;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && questManagerHost != null &&
                                     player != null && beaconVisual != null && equipmentPickupHost != null &&
                                     equipmentPickupTarget != null && equipmentDoubleJumpTarget != null &&
                                     !string.IsNullOrWhiteSpace(equipmentQuestId) &&
                                     !string.IsNullOrWhiteSpace(doubleJumpConditionId);

        public bool HasTarget(string questId)
        {
            if (string.IsNullOrWhiteSpace(questId) || targets == null) return false;
            foreach (var target in targets)
                if (target != null && target.QuestId == questId && target.Target != null) return true;
            return false;
        }

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
            RefreshEquipmentQuestTarget();
            if (currentTarget == null)
            {
                SetVisualActive(false);
                return;
            }

            transform.position = player.position + visualOffset;
            var distance = Vector3.Distance(player.position, currentTarget.position);
            if (beaconVisual != null)
            {
                var direction = currentTarget.position.x < player.position.x ? -1f : 1f;
                beaconVisual.transform.localRotation = Quaternion.Euler(0f, 0f, direction < 0f ? 180f : 0f);
            }
            SetVisualActive(distance > hideDistance);
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

            RefreshEquipmentQuestTarget();

            SetVisualActive(currentTarget != null && Vector3.Distance(player.position, currentTarget.position) > hideDistance);
        }

        private void RefreshEquipmentQuestTarget()
        {
            if (questSequenceHost.CurrentQuestId != equipmentQuestId) return;
            if (!equipmentPickupHost.IsCollected)
            {
                currentTarget = equipmentPickupTarget;
                return;
            }

            if (questManagerHost.Initialize() &&
                questManagerHost.System.GetConditionProgress(equipmentQuestId, doubleJumpConditionId) <= 0)
            {
                currentTarget = equipmentDoubleJumpTarget;
                return;
            }

            currentTarget = null;
        }

        private void SetVisualActive(bool active)
        {
            if (beaconVisual != null && beaconVisual.activeSelf != active)
                beaconVisual.SetActive(active);
        }
    }
}
