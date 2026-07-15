using Narthex.Content;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class TutorialModuleUseHost : MonoBehaviour
    {
        [SerializeField] private PlayerInputHost inputHost;
        [SerializeField] private CombatActorHost playerActor;
        [SerializeField] private ModuleSystemHost moduleSystemHost;
        [SerializeField] private ModuleDefinition tutorialModule;
        [SerializeField, Min(0)] private int slotIndex;

        public bool HasValidSetup => inputHost != null && playerActor != null && moduleSystemHost != null && tutorialModule != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialModuleUseHost requires pre-placed input, player, module system, and module references.", this);
                enabled = false;
                return;
            }

            if (!moduleSystemHost.Initialize())
            {
                enabled = false;
                return;
            }

            moduleSystemHost.System.Register(tutorialModule);
            moduleSystemHost.System.Unlock(tutorialModule.StableId);
            moduleSystemHost.System.Equip(tutorialModule.StableId, slotIndex);
        }

        private void OnEnable()
        {
            if (inputHost != null) inputHost.ModuleRequested += TryUseModule;
        }

        private void OnDisable()
        {
            if (inputHost != null) inputHost.ModuleRequested -= TryUseModule;
        }

        private void TryUseModule()
        {
            if (playerActor.Runtime == null || moduleSystemHost.System == null) return;
            moduleSystemHost.System.TryUse(playerActor.ActorId, tutorialModule.StableId);
        }
    }
}
