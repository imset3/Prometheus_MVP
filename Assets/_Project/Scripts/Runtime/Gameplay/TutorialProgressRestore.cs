using System.Collections.Generic;
using Narthex.Save;

namespace Narthex.Gameplay
{
    public static class TutorialProgressRestore
    {
        public static bool IsRelayProgressRestored(RunSaveData runData, string relayId, string completionQuestId)
        {
            return runData != null &&
                   Contains(runData.ActivatedTowerIds, relayId) &&
                   Contains(runData.QuestIds, completionQuestId);
        }

        public static int FindFirstIncompleteQuestIndex(RunSaveData runData, IReadOnlyList<string> questIds)
        {
            if (questIds == null || questIds.Count == 0) return 0;

            for (var index = 0; index < questIds.Count; index++)
                if (!Contains(runData?.QuestIds, questIds[index])) return index;

            return questIds.Count - 1;
        }

        private static bool Contains(ICollection<string> ids, string value)
        {
            return ids != null && !string.IsNullOrWhiteSpace(value) && ids.Contains(value);
        }
    }
}
