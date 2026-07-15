using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class ModuleSystemHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        public AbilityExecutor AbilityExecutor { get; private set; }
        public ModuleSystem System { get; private set; }

        private void Awake() => Initialize();

        public bool Initialize()
        {
            if (System != null) return true;
            if (serviceRoot == null)
            {
                Debug.LogError("ModuleSystemHost requires a pre-placed ServiceRoot reference.", this);
                return false;
            }

            serviceRoot.Initialize();
            AbilityExecutor = new AbilityExecutor(serviceRoot.Events);
            System = new ModuleSystem(serviceRoot.Events, AbilityExecutor);
            return true;
        }
    }
}
