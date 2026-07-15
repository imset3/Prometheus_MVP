using System;
using Narthex.Content;
using Narthex.Core;
using UnityEngine;

namespace Narthex.SceneFlow
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private ContentRegistry contentRegistry;
        public ServiceRoot Services { get; private set; }

        private void Awake()
        {
            Services = GetComponent<ServiceRoot>();
            if (Services == null)
            {
                Debug.LogError("GameBootstrap requires a pre-placed ServiceRoot component.", this);
                enabled = false;
                return;
            }

            Services.Initialize();
            try
            {
                ContentRegistryValidator.Validate(contentRegistry);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Services.StateMachine.TryTransition(GameState.Error);
            }
        }

        private void Start()
        {
            if (Services == null || Services.StateMachine.Current == GameState.Error) return;
            Services.StateMachine.TryTransition(GameState.Loading);
            Services.StateMachine.TryTransition(GameState.Title);
        }
    }
}
