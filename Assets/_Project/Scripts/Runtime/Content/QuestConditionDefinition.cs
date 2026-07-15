using UnityEngine;

namespace Narthex.Content
{
    [CreateAssetMenu(menuName = "Narthex/Content/Quest Condition Definition")]
    public sealed class QuestConditionDefinition : DefinitionBase
    {
        public QuestSignalType SignalType;
        public string TargetId;
        public int RequiredAmount = 1;
    }
}
