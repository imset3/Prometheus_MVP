using Narthex.Core;
using UnityEngine;

namespace Narthex.Save
{
    public sealed class SaveSystemHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private string saveFileName = "narthex_save.json";

        public SaveSystem System { get; private set; }

        private void Awake() => Initialize();

        public bool Initialize()
        {
            if (System != null) return true;
            if (serviceRoot == null)
            {
                Debug.LogError("SaveSystemHost requires a pre-placed ServiceRoot reference.", this);
                return false;
            }

            serviceRoot.Initialize();
            var path = GameLaunchSession.ResolveSavePath(saveFileName);
            System = new SaveSystem(serviceRoot.Events, new SaveFileStore(path));
            System.Load();
            return true;
        }

        private void OnDestroy() => System?.Dispose();
    }
}
