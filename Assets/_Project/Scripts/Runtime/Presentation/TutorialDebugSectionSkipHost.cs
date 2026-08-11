using System;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Narthex.Presentation
{
    [Serializable]
    public sealed class TutorialDebugSectionDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField] private string questId;
        [SerializeField] private string locationName;
        [SerializeField] private GameObject zoneRoot;
        [SerializeField] private GameObject technicalRoot;
        [SerializeField] private GameObject[] activateOnJump = Array.Empty<GameObject>();
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float cameraMinX;
        [SerializeField] private float cameraMaxX;
        [SerializeField] private bool cameraTracksVertical;
        [SerializeField] private float cameraFixedY;
        [SerializeField] private float cameraMinY;
        [SerializeField] private float cameraMaxY;

        public string DisplayName => displayName;
        public string QuestId => questId;
        public string LocationName => locationName;
        public GameObject ZoneRoot => zoneRoot;
        public GameObject TechnicalRoot => technicalRoot;
        public GameObject[] ActivateOnJump => activateOnJump;
        public Transform SpawnPoint => spawnPoint;
        public float CameraMinX => cameraMinX;
        public float CameraMaxX => cameraMaxX;
        public bool CameraTracksVertical => cameraTracksVertical;
        public float CameraFixedY => cameraFixedY;
        public float CameraMinY => cameraMinY;
        public float CameraMaxY => cameraMaxY;

        public bool IsValid => !string.IsNullOrWhiteSpace(displayName) &&
                               !string.IsNullOrWhiteSpace(questId) &&
                               !string.IsNullOrWhiteSpace(locationName) &&
                               zoneRoot != null && technicalRoot != null && spawnPoint != null &&
                               cameraMinX <= cameraMaxX &&
                               (!cameraTracksVertical || cameraMinY <= cameraMaxY);
    }

    /// <summary>
    /// Keyboard shortcut for jumping directly to the imported combat sections.
    /// F8/F9 intentionally remain available in release demo builds for QA and demonstrations.
    /// It deliberately changes runtime state only and never records skipped quests in the save file.
    /// </summary>
    public sealed class TutorialDebugSectionSkipHost : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private TutorialRestartHost restartHost;
        [SerializeField] private TutorialDialoguePresenter dialoguePresenter;
        [SerializeField] private TutorialChapter0IntroFlowHost introFlowHost;
        [SerializeField] private TutorialGuideCompanionHost guideCompanion;
        [SerializeField] private CameraFollowHost cameraFollowHost;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private PlayerMotorHost playerMotorHost;
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody2D playerBody;
        [SerializeField] private CanvasGroup fadeCanvasGroup;

        [Header("Sections")]
        [SerializeField] private GameObject[] zoneRoots = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] technicalRoots = Array.Empty<GameObject>();
        [SerializeField] private TutorialDebugSectionDefinition[] sections =
            Array.Empty<TutorialDebugSectionDefinition>();

        [Header("Controls")]
        [SerializeField] private bool showOverlay;

        private int activeSectionIndex = -1;

        public int ActiveSectionIndex => activeSectionIndex;
        public int SectionCount => sections?.Length ?? 0;
        public bool HasValidSetup =>
            serviceRoot != null && questSequenceHost != null && restartHost != null &&
            dialoguePresenter != null && cameraFollowHost != null && playerInputHost != null &&
            playerMotorHost != null && player != null && playerBody != null && fadeCanvasGroup != null &&
            sections != null && sections.Length >= 3 && AllSectionsValid();

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialDebugSectionSkipHost requires F, G, and Helte debug section references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f8Key.wasPressedThisFrame) JumpToFSection();
            else if (keyboard.f9Key.wasPressedThisFrame) JumpToNextSection();
        }

        private void OnGUI()
        {
            // Intentionally no development overlay. F8/F9 keyboard shortcuts remain available.
        }

        public bool JumpToFSection() => JumpToSection(0);

        public bool JumpToNextSection()
        {
            var currentIndex = FindCurrentSectionIndex();
            return JumpToSection(currentIndex < 0 ? 0 : Mathf.Min(currentIndex + 1, sections.Length - 1));
        }

        public bool JumpToSection(int sectionIndex)
        {
            if (!HasValidSetup || sectionIndex < 0 || sectionIndex >= sections.Length) return false;
            var section = sections[sectionIndex];

            dialoguePresenter.CancelForDebugSkip();
            if (introFlowHost != null) introFlowHost.enabled = false;
            guideCompanion?.CancelGuide();
            playerInputHost.enabled = true;

            SetAllActive(zoneRoots, false);
            SetAllActive(technicalRoots, false);
            section.ZoneRoot.SetActive(true);
            section.TechnicalRoot.SetActive(true);
            SetAllActive(section.ActivateOnJump, true);
            playerMotorHost.UnlockDoubleJump();

            playerBody.linearVelocity = Vector2.zero;
            playerBody.position = section.SpawnPoint.position;
            player.position = section.SpawnPoint.position;
            Physics2D.SyncTransforms();

            if (section.CameraTracksVertical)
                cameraFollowHost.SetTrackingBounds(
                    section.CameraMinX,
                    section.CameraMaxX,
                    section.CameraMinY,
                    section.CameraMaxY,
                    true);
            else
                cameraFollowHost.SetBounds(
                    section.CameraMinX,
                    section.CameraMaxX,
                    section.CameraFixedY,
                    true);

            restartHost.SetRuntimeCheckpoint(section.QuestId, section.SpawnPoint);
            if (!questSequenceHost.TryDebugJumpToQuest(section.QuestId))
            {
                Debug.LogError($"개발자 스킵 퀘스트 전환에 실패했습니다: {section.QuestId}", this);
                return false;
            }

            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
            serviceRoot.Events.Publish(new TutorialLocationChanged(section.LocationName));
            RefreshSceneStatusPresenters();
            activeSectionIndex = sectionIndex;
            Debug.Log($"[sragon000][구간 스킵] {section.DisplayName}부터 테스트를 시작합니다.", this);
            return true;
        }

        private int FindCurrentSectionIndex()
        {
            if (activeSectionIndex >= 0 && activeSectionIndex < sections.Length)
                return activeSectionIndex;

            for (var index = 0; index < sections.Length; index++)
                if (sections[index].QuestId == questSequenceHost.CurrentQuestId)
                    return index;
            return -1;
        }

        private bool AllSectionsValid()
        {
            foreach (var section in sections)
                if (section == null || !section.IsValid) return false;
            return true;
        }

        private static void SetAllActive(GameObject[] roots, bool active)
        {
            if (roots == null) return;
            foreach (var root in roots)
                if (root != null && root.activeSelf != active)
                    root.SetActive(active);
        }

        private void RefreshSceneStatusPresenters()
        {
            foreach (var presenter in Resources.FindObjectsOfTypeAll<TutorialStatusPresenter>())
            {
                if (presenter == null || presenter.gameObject.scene != gameObject.scene) continue;
                presenter.RefreshFromCurrentQuest();
            }
        }
    }
}
