using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Restricts action-training quest progress to the authored lesson lane while leaving
    /// the player's controls fully available everywhere else.
    /// </summary>
    public sealed class TutorialTrainingActionScopeHost : MonoBehaviour
    {
        [SerializeField] private QuestManagerHost questManagerHost;
        [SerializeField] private Transform player;
        [SerializeField] private string[] scopedQuestIds = System.Array.Empty<string>();
        [SerializeField] private Collider2D[] scopeAreas = System.Array.Empty<Collider2D>();

        public bool HasValidSetup => questManagerHost != null && player != null &&
                                     scopedQuestIds != null && scopeAreas != null &&
                                     scopedQuestIds.Length > 0 && scopedQuestIds.Length == scopeAreas.Length &&
                                     HasCompleteScopes();

        private void Awake()
        {
            if (!HasValidSetup || !questManagerHost.Initialize())
            {
                Debug.LogError("TutorialTrainingActionScopeHost requires matching quest IDs, trigger areas, and player references.", this);
                enabled = false;
                return;
            }

            questManagerHost.System.SetProgressSignalFilter(ShouldAcceptSignal);
        }

        private void OnDestroy()
        {
            questManagerHost?.System?.ClearProgressSignalFilter(ShouldAcceptSignal);
        }

        public bool IsPlayerInsideScope(string questId)
        {
            for (var index = 0; index < scopedQuestIds.Length; index++)
            {
                if (scopedQuestIds[index] == questId)
                    return scopeAreas[index].OverlapPoint(player.position);
            }

            return true;
        }

        private bool ShouldAcceptSignal(string questId, GameplaySignal signal)
        {
            return IsPlayerInsideScope(questId);
        }

        private bool HasCompleteScopes()
        {
            for (var index = 0; index < scopedQuestIds.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(scopedQuestIds[index]) || scopeAreas[index] == null ||
                    !scopeAreas[index].isTrigger)
                    return false;
            }

            return true;
        }
    }
}
