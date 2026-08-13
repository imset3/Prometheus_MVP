using Narthex.Content;
using Narthex.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Gameplay
{
    public static class TutorialRuntimeObjectiveTargetRegistry
    {
        private static readonly Dictionary<string, Transform> Targets = new(System.StringComparer.Ordinal);

        public static void Register(string questId, Transform target)
        {
            if (!string.IsNullOrWhiteSpace(questId) && target != null) Targets[questId] = target;
        }

        public static bool TryGet(string questId, out Transform target)
        {
            target = null;
            if (string.IsNullOrWhiteSpace(questId) || !Targets.TryGetValue(questId, out var candidate) || candidate == null)
                return false;
            target = candidate;
            return true;
        }
    }

    /// <summary>
    /// Completes a marker-driven traversal objective when the player reaches this
    /// trigger. The marker transform and collider are the complete authoring surface.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialTrainingArrivalMarkerHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private Transform player;
        [SerializeField] private string questId = "QST-TUTO-004";
        [SerializeField] private string signalTargetId = "TRAINING-DASH-FINISH";

        private bool published;

        public bool HasValidSetup => serviceRoot != null && questSequenceHost != null && player != null &&
                                     !string.IsNullOrWhiteSpace(questId) &&
                                     !string.IsNullOrWhiteSpace(signalTargetId);
        public string SignalTargetId => signalTargetId;

        public void Configure(
            ServiceRoot runtimeServiceRoot,
            TutorialQuestSequenceHost runtimeQuestSequence,
            Transform runtimePlayer,
            string runtimeQuestId,
            string runtimeSignalTargetId)
        {
            serviceRoot = runtimeServiceRoot;
            questSequenceHost = runtimeQuestSequence;
            player = runtimePlayer;
            questId = runtimeQuestId;
            signalTargetId = runtimeSignalTargetId;
            enabled = HasValidSetup;
        }

        private void Awake()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
            if (HasValidSetup) return;
            Debug.LogError("TutorialTrainingArrivalMarkerHost requires quest, player, and signal references.", this);
            enabled = false;
        }

        private void OnEnable() => published = false;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (published || questSequenceHost.CurrentQuestId != questId || !IsPlayer(other)) return;
            published = true;
            serviceRoot.Initialize();
            serviceRoot.Events.Publish(new GameplaySignal(QuestSignalType.PortalUsed, signalTargetId));
        }

        private bool IsPlayer(Collider2D other)
        {
            return other != null && (other.transform == player || other.transform.IsChildOf(player));
        }
    }

    /// <summary>Registers the pre-authored summit marker. It never creates a runtime replacement.</summary>
    public static class TutorialTrainingRuntimeMarkerInstaller
    {
        private const string DoubleJumpQuestId = "QST-TUTO-006";
        private const string DoubleJumpTargetId = "TRAINING-DOUBLE-JUMP-SUMMIT";

        public static Transform EnsureDoubleJumpSummit(Scene scene)
        {
            var existing = FindSceneComponents<TutorialTrainingArrivalMarkerHost>(scene)
                .FirstOrDefault(marker => marker.SignalTargetId == DoubleJumpTargetId &&
                                          !marker.CompareTag("EditorOnly"));
            if (existing == null)
            {
                Debug.LogError("Authored TRAINING-DOUBLE-JUMP-SUMMIT marker is missing or tagged EditorOnly.");
                return null;
            }
            var target = existing.transform;

            TutorialRuntimeObjectiveTargetRegistry.Register(DoubleJumpQuestId, target);
            return target;
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }
}
