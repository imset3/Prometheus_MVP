using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.SceneFlow
{
    public sealed class Chapter01TransitionHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private GameObject resultOverlay;
        [SerializeField] private Button nextStageButton;
        [SerializeField] private string requiredStageId = "CHAPTER_01";
        [SerializeField] private string chapterSceneName = "Chapter01";

        private bool loading;

        public bool HasValidSetup => serviceRoot != null && saveSystemHost != null && playerInputHost != null && resultOverlay != null && nextStageButton != null &&
                                     !string.IsNullOrWhiteSpace(requiredStageId) && !string.IsNullOrWhiteSpace(chapterSceneName);

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("Chapter01TransitionHost requires pre-placed service, save, button, and scene references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (!saveSystemHost.Initialize()) enabled = false;
        }

        private void OnEnable()
        {
            if (nextStageButton != null) nextStageButton.onClick.AddListener(TryEnterChapter01);
            if (playerInputHost != null) playerInputHost.InteractRequested += HandleInteractRequested;
        }

        private void OnDisable()
        {
            if (nextStageButton != null) nextStageButton.onClick.RemoveListener(TryEnterChapter01);
            if (playerInputHost != null) playerInputHost.InteractRequested -= HandleInteractRequested;
        }

        private void HandleInteractRequested()
        {
            if (resultOverlay != null && resultOverlay.activeInHierarchy)
                TryEnterChapter01();
        }

        public void TryEnterChapter01()
        {
            if (loading || !CanEnterChapter01()) return;

            loading = true;
            serviceRoot.StateMachine.TryTransition(GameState.Loading);
            SceneManager.LoadScene(chapterSceneName, LoadSceneMode.Single);
        }

        private bool CanEnterChapter01()
        {
            if (!HasValidSetup || saveSystemHost.System == null) return false;

            var canEnter = Chapter01TransitionPolicy.CanEnter(
                saveSystemHost.System.Current.Permanent,
                saveSystemHost.System.Current.Run,
                requiredStageId);
            if (!canEnter) Debug.LogWarning("CHAPTER_01 transition is unavailable until the tutorial completion is saved.", this);
            return canEnter;
        }
    }
}
