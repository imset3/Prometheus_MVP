using UnityEngine;

namespace Narthex.Content
{
    [CreateAssetMenu(menuName = "Narthex/Content/Module Tree Definition")]
    public sealed class ModuleTreeDefinition : DefinitionBase
    {
        public ModuleTreeType TreeType;
        public bool AvailableAtRunStart = true;
        public ModuleNodeDefinition[] Nodes = new ModuleNodeDefinition[0];
    }
}
