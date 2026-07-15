using Narthex.Save;

namespace Narthex.SceneFlow
{
    public static class Chapter01TransitionPolicy
    {
        public static bool CanEnter(PermanentSaveData permanentData, RunSaveData runData, string requiredStageId)
        {
            return permanentData != null && runData != null && permanentData.TutorialCompleted &&
                   runData.CurrentStageId == requiredStageId;
        }
    }
}
