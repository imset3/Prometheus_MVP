using Narthex.Core;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class QuestManagerHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        public QuestManager System { get; private set; }

        private void Awake() => Initialize();

        public bool Initialize()
        {
            if (System != null) return true;
            if (serviceRoot == null)
            {
                Debug.LogError("QuestManagerHost requires a pre-placed ServiceRoot reference.", this);
                return false;
            }

            serviceRoot.Initialize();
            System = new QuestManager(serviceRoot.Events);
            return true;
        }

        private void OnDestroy() => System?.Dispose();
    }
}
