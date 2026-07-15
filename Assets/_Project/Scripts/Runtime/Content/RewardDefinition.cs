using UnityEngine;

namespace Narthex.Content
{
    [CreateAssetMenu(menuName = "Narthex/Content/Reward Definition")]
    public sealed class RewardDefinition : DefinitionBase
    {
        public RewardType RewardType;
        public string TargetId;
        public int Amount;
    }
}
