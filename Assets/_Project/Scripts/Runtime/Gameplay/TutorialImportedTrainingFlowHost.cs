using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Adapts the imported blockout training room to the existing tutorial systems.
    /// It grants the already-equipped boots on room entry and only exposes ranged
    /// targets while the ranged lesson is active.
    /// </summary>
    public sealed class TutorialImportedTrainingFlowHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private PlayerMotorHost playerMotor;
        [SerializeField] private Transform player;
        [SerializeField] private Collider2D trainingArea;
        [SerializeField] private GameObject[] rangedTargets = System.Array.Empty<GameObject>();
        [SerializeField] private string rangedQuestId = "QST-TUTO-005";

        private bool bootsGranted;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && playerMotor != null &&
                                     player != null && trainingArea != null && trainingArea.isTrigger &&
                                     rangedTargets != null && rangedTargets.Length == 3 &&
                                     !string.IsNullOrWhiteSpace(rangedQuestId);
        public bool BootsGranted => bootsGranted;
        public int RangedTargetCount => rangedTargets?.Length ?? 0;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialImportedTrainingFlowHost requires training bounds, player, motor, and three ranged targets.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            SetRangedTargetsActive(false);
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void Start()
        {
            RefreshTargets(questSequenceHost.CurrentQuestId);
        }

        private void Update()
        {
            if (bootsGranted || player == null || trainingArea == null ||
                !trainingArea.OverlapPoint(player.position))
                return;

            playerMotor.UnlockDoubleJump();
            bootsGranted = true;
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            SetRangedTargetsActive(false);
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            RefreshTargets(message.QuestId);
        }

        private void RefreshTargets(string questId)
        {
            SetRangedTargetsActive(questId == rangedQuestId);
            if (questId != rangedQuestId) return;
            foreach (var target in rangedTargets)
            {
                var actor = target != null ? target.GetComponent<CombatActorHost>() : null;
                actor?.ResetRuntime();
            }
        }

        private void SetRangedTargetsActive(bool active)
        {
            if (rangedTargets == null) return;
            foreach (var target in rangedTargets)
                if (target != null && target.activeSelf != active)
                    target.SetActive(active);
        }
    }
}
