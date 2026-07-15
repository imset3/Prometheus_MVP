using Narthex.Content;
using Narthex.Core;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class ModuleTreeManagerHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private ModuleSystemHost moduleSystemHost;
        [SerializeField] private ModuleTreeDefinition[] treeDefinitions;

        public ModuleTreeManager System { get; private set; }
        public bool HasValidSetup => serviceRoot != null && saveSystemHost != null && moduleSystemHost != null && treeDefinitions != null;

        private void Awake() => Initialize();

        public bool Initialize()
        {
            if (System != null) return true;
            if (!HasValidSetup)
            {
                Debug.LogError("ModuleTreeManagerHost requires pre-placed service, save, module, and tree references.", this);
                return false;
            }

            serviceRoot.Initialize();
            if (!saveSystemHost.Initialize() || !moduleSystemHost.Initialize()) return false;
            System = new ModuleTreeManager(serviceRoot.Events, moduleSystemHost.System, saveSystemHost.System.Current.Permanent, saveSystemHost.System.Current.Run);
            foreach (var tree in treeDefinitions)
            {
                if (tree == null)
                {
                    Debug.LogError("ModuleTreeManagerHost has an empty tree definition reference.", this);
                    System = null;
                    return false;
                }

                System.Register(tree);
            }

            return true;
        }
    }
}
