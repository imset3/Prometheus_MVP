namespace Narthex.Save
{
    public sealed class RunContext
    {
        public RunSaveData Data { get; private set; } = new RunSaveData();

        public void Reset(string startStageId)
        {
            Data = new RunSaveData
            {
                RunNumber = Data.RunNumber + 1,
                CurrentStageId = startStageId,
                Level = 1
            };
        }
    }
}
