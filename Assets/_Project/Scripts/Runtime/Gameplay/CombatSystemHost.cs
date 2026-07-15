using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class CombatSystemHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        public CombatSystem System { get; private set; }
        public GameEventBus Events => serviceRoot != null ? serviceRoot.Events : null;

        private void Awake() => Initialize();

        public bool Initialize()
        {
            if (System != null) return true;
            if (serviceRoot == null)
            {
                Debug.LogError("CombatSystemHost requires a pre-placed ServiceRoot reference.", this);
                return false;
            }

            serviceRoot.Initialize();
            System = new CombatSystem(serviceRoot.Events);
            return true;
        }
    }
}
