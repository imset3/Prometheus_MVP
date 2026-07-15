namespace Narthex.Core
{
    public readonly struct PlayerDead
    {
        public readonly string PlayerId;
        public readonly string DeathReason;
        public readonly string CurrentStageId;

        public PlayerDead(string playerId, string deathReason, string currentStageId)
        {
            PlayerId = playerId;
            DeathReason = deathReason;
            CurrentStageId = currentStageId;
        }
    }

    public readonly struct RunRestarted
    {
        public readonly int RunNumber;
        public readonly string StartStageId;

        public RunRestarted(int runNumber, string startStageId)
        {
            RunNumber = runNumber;
            StartStageId = startStageId;
        }
    }

    public readonly struct PlayerRespawned
    {
        public readonly string PlayerId;
        public PlayerRespawned(string playerId) { PlayerId = playerId; }
    }
}
