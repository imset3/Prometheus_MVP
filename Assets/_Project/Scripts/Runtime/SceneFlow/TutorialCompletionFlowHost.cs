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
                return;
            }

            EnterTutorialState();
        }

        private void OnEnable()
        {
            if (serviceRoot == null) return;
            serviceRoot.Initialize();
            serviceRoot.Events?.Subscribe<TutorialCompleted>(HandleTutorialCompleted);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialCompleted>(HandleTutorialCompleted);
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
            EnterResultState();
            SetResultPresentation(true);
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
            foreach (var gameplayHudObject in gameplayHudObjects)
                gameplayHudObject.SetActive(!showingResult);
        }

        private void EnterResultState()
        {
            EnterTutorialState();
            if (serviceRoot.StateMachine.Current == GameState.Tutorial)
                serviceRoot.StateMachine.TryTransition(GameState.Result);
        }
    }
}
