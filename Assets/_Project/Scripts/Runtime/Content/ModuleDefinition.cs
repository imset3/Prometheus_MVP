using UnityEngine;

namespace Narthex.Content
{
    [CreateAssetMenu(menuName = "Narthex/Content/Module Definition")]
    public sealed class ModuleDefinition : DefinitionBase
    {
        public string TreeId;
        public AbilityDefinition Ability;
        public int UnlockCost = 1;
        public int MaxUpgradeLevel = 3;
        public ModuleCastPolicyData CastPolicy;
    }
}
