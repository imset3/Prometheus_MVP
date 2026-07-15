using UnityEngine;

namespace Narthex.Content
{
    [CreateAssetMenu(menuName = "Narthex/Content/Action Definition")]
    public sealed class ActionDefinition : DefinitionBase
    {
        public ActionType ActionType;
        public float Delay;
        public string TargetId;
        public string[] EffectIds;
    }
}
