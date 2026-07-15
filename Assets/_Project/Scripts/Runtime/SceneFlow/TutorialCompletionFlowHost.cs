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

        private void Awake()
        {
            if (serviceRoot == null || saveSystemHost == null || resultOverlay == null)
            {
                Debug.LogError("TutorialCompletionFlowHost requires pre-placed ServiceRoot, SaveSystemHost, and result overlay references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            resultOverlay.SetActive(false);
            if (!saveSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            if (saveSystemHost.System.Current.Permanent.TutorialCompleted)
            {
                EnterResultState();
                resultOverlay.SetActive(true);
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
            resultOverlay.SetActive(true);
        }

        private void EnterResultState()
        {
            EnterTutorialState();
            if (serviceRoot.StateMachine.Current == GameState.Tutorial)
                serviceRoot.StateMachine.TryTransition(GameState.Result);
        }
    }
}
