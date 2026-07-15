using Narthex.Core;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class RewardExecutorHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        public RewardExecutor System { get; private set; }

        private void Awake() => Initialize();

        public bool Initialize()
        {
            if (System != null) return true;
            if (serviceRoot == null || saveSystemHost == null || !saveSystemHost.Initialize())
            {
                Debug.LogError("RewardExecutorHost requires pre-placed ServiceRoot and SaveSystemHost references.", this);
                return false;
            }

            serviceRoot.Initialize();
            System = new RewardExecutor(serviceRoot.Events, saveSystemHost.System.Current.Permanent, saveSystemHost.System.Current.Run);
            return true;
        }

        private void OnDestroy() => System?.Dispose();
    }
}
