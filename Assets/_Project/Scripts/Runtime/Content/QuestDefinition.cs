using UnityEngine;

namespace Narthex.Content
{
    [CreateAssetMenu(menuName = "Narthex/Content/Quest Definition")]
    public sealed class QuestDefinition : DefinitionBase
    {
        public QuestConditionDefinition[] Conditions = new QuestConditionDefinition[0];
        public RewardDefinition[] Rewards = new RewardDefinition[0];
        public string[] NextQuestIds = new string[0];
    }
}
