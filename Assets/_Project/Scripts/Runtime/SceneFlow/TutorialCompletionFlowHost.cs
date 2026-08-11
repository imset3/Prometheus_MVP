using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Save;
using UnityEngine;

namespace Narthex.SceneFlow
{
    public sealed class TutorialCompletionFlowHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private GameObject resultOverlay;
        [SerializeField] private GameObject[] gameplayHudObjects = System.Array.Empty<GameObject>();
        [SerializeField] private TutorialDemoEndingSequenceHost demoEndingSequence;

        public bool HasValidSetup => serviceRoot != null && saveSystemHost != null && resultOverlay != null &&
                                     gameplayHudObjects != null && gameplayHudObjects.Length > 0 &&
                                     HasCompleteGameplayHudReferences();
        public int GameplayHudObjectCount => gameplayHudObjects?.Length ?? 0;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialCompletionFlowHost requires pre-placed service, result overlay, and gameplay HUD references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            SetResultPresentation(false);
            if (!saveSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            if (saveSystemHost.System.Current.Permanent.TutorialCompleted)
            {
                EnterResultState();
                SetResultPresentation(true);
                demoEndingSequence?.PrepareImmediateResult();
                return;
            }

            EnterTutorialState();
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events?.Subscribe<TutorialCompleted>(HandleTutorialCompleted);
            serviceRoot.Events?.Subscribe<BossKilled>(HandleBossKilled);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialCompleted>(HandleTutorialCompleted);
            serviceRoot?.Events?.Unsubscribe<BossKilled>(HandleBossKilled);
        }

        private void HandleBossKilled(BossKilled message)
        {
            // TutorialCompleted can be published after quest/reward processing. Hide the
            // live tutorial HUD on the actual defeat frame so the epilogue never starts
            // with a stale objective, prompt, or boss bar still visible.
            SetGameplayHudVisible(false);
        }

        private void EnterTutorialState()
        {
            var stateMachine = serviceRoot.StateMachine;
            if (stateMachine.Current == GameState.Booting) stateMachine.TryTransition(GameState.Loading);
            if (stateMachine.Current == GameState.Loading || stateMachine.Current == GameState.Title)
                stateMachine.TryTransition(GameState.Tutorial);
        }

        private void HandleTutorialCompleted(TutorialCompleted message)
        {
            if (demoEndingSequence != null && demoEndingSequence.TryBeginEnding()) return;
            PresentResultNow();
        }

        public void PresentResultNow()
        {
            EnterResultState();
            SetResultPresentation(true);
        }

        public void SetGameplayHudVisible(bool visible)
        {
            foreach (var gameplayHudObject in gameplayHudObjects)
                gameplayHudObject.SetActive(visible);
        }

        private bool HasCompleteGameplayHudReferences()
        {
            foreach (var gameplayHudObject in gameplayHudObjects)
            {
                if (gameplayHudObject == null) return false;
            }

            return true;
        }

        private void SetResultPresentation(bool showingResult)
        {
            resultOverlay.SetActive(showingResult);
            SetGameplayHudVisible(!showingResult);
        }

        private void EnterResultState()
        {
            EnterTutorialState();
            if (serviceRoot.StateMachine.Current == GameState.Tutorial)
                serviceRoot.StateMachine.TryTransition(GameState.Result);
        }
    }
}
