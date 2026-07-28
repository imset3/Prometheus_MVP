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
    /// Editor/development-build-only shortcut for jumping directly to the imported combat sections.
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
        [SerializeField] private bool showOverlay = true;

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
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            enabled = false;
            return;
#else
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialDebugSectionSkipHost requires F, G, and Helte debug section references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f8Key.wasPressedThisFrame) JumpToFSection();
            else if (keyboard.f9Key.wasPressedThisFrame) JumpToNextSection();
#endif
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!showOverlay || !HasValidSetup) return;

            const float width = 330f;
            const float height = 76f;
            var area = new Rect(16f, Screen.height - height - 16f, width, height);
            GUI.Box(area, "개발자 구간 스킵 · 저장 영향 없음");
            if (GUI.Button(new Rect(area.x + 10f, area.y + 28f, 145f, 36f), "F8  F 구역 바로가기"))
                JumpToFSection();
            if (GUI.Button(new Rect(area.x + 165f, area.y + 28f, 155f, 36f), "F9  다음 구역"))
                JumpToNextSection();
#endif
        }

        public bool JumpToFSection() => JumpToSection(0);

        public bool JumpToNextSection()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return false;
#else
            var currentIndex = FindCurrentSectionIndex();
            return JumpToSection(currentIndex < 0 ? 0 : Mathf.Min(currentIndex + 1, sections.Length - 1));
#endif
        }

        public bool JumpToSection(int sectionIndex)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return false;
#else
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
            activeSectionIndex = sectionIndex;
            Debug.Log($"[sragon000][구간 스킵] {section.DisplayName}부터 테스트를 시작합니다.", this);
            return true;
#endif
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
    }
}
