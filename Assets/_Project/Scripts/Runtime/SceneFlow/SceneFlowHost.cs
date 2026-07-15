using Narthex.Core;
using UnityEngine;

namespace Narthex.SceneFlow
{
    public sealed class SceneFlowHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;

        public SceneFlowManager Manager { get; private set; }
        public SceneFlowCoordinator Coordinator { get; private set; }

        private void Awake() => Initialize();

        public bool Initialize()
        {
            if (Manager != null) return true;
            if (serviceRoot == null)
            {
                Debug.LogError("SceneFlowHost requires a pre-placed ServiceRoot reference.", this);
                return false;
            }

            serviceRoot.Initialize();
            Manager = new SceneFlowManager(serviceRoot.StateMachine);
            Coordinator = new SceneFlowCoordinator(serviceRoot.Events, Manager);
            return true;
        }

        private void OnDestroy() => Coordinator?.Dispose();
    }
}
