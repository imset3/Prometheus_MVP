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

    /// <summary>
    /// Player builds strip GameObjects tagged EditorOnly. The authored summit marker remains useful
    /// in Scene view, while this installer guarantees an equivalent runtime trigger and beacon target
    /// when the build has stripped that authoring marker.
    /// </summary>
    public static class TutorialTrainingRuntimeMarkerInstaller
    {
        private const string DoubleJumpQuestId = "QST-TUTO-006";
        private const string DoubleJumpTargetId = "TRAINING-DOUBLE-JUMP-SUMMIT";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TutorialScene" && scene.name != "BossDevelopmentScene") return;

            EnsureDoubleJumpSummit(scene);
        }

        public static Transform EnsureDoubleJumpSummit(Scene scene)
        {
            var existing = FindSceneComponents<TutorialTrainingArrivalMarkerHost>(scene)
                .FirstOrDefault(marker => marker.SignalTargetId == DoubleJumpTargetId &&
                                          !marker.CompareTag("EditorOnly"));
            var target = existing != null ? existing.transform : CreateDoubleJumpSummit(scene);
            if (target == null) return null;

            TutorialRuntimeObjectiveTargetRegistry.Register(DoubleJumpQuestId, target);
            return target;
        }

        private static Transform CreateDoubleJumpSummit(Scene scene)
        {
            var phase = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "02_더블점프" &&
                                        item.parent != null && item.parent.name == "TrainingPhaseContents");
            var serviceRoot = FindSceneComponents<ServiceRoot>(scene).FirstOrDefault();
            var questSequence = FindSceneComponents<TutorialQuestSequenceHost>(scene).FirstOrDefault();
            var playerMotor = FindSceneComponents<PlayerMotorHost>(scene).FirstOrDefault();
            if (phase == null || serviceRoot == null || questSequence == null || playerMotor == null) return null;

            var highestPlatform = phase.GetComponentsInChildren<BoxCollider2D>(true)
                .Where(collider => !collider.isTrigger)
                .OrderByDescending(collider => collider.bounds.max.y)
                .ThenByDescending(collider => collider.bounds.center.x)
                .FirstOrDefault();
            if (highestPlatform == null) return null;

            var markerObject = new GameObject("Runtime_훈련_더블점프_끝");
            markerObject.SetActive(false);
            markerObject.transform.SetParent(phase, true);
            markerObject.transform.position = new Vector3(
                highestPlatform.bounds.center.x,
                highestPlatform.bounds.max.y + 0.75f,
                phase.position.z);
            var trigger = markerObject.AddComponent<BoxCollider2D>();
            trigger.size = new Vector2(3f, 1.5f);
            trigger.isTrigger = true;
            var host = markerObject.AddComponent<TutorialTrainingArrivalMarkerHost>();
            host.Configure(serviceRoot, questSequence, playerMotor.transform, DoubleJumpQuestId, DoubleJumpTargetId);
            // Keep activeSelf enabled so the marker follows its phase parent's later activation.
            // Mirroring activeInHierarchy here permanently disabled it when the scene initially
            // loaded with the double-jump phase hidden.
            markerObject.SetActive(true);
            return markerObject.transform;
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }
}
