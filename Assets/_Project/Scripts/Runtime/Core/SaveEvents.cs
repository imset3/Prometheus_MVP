namespace Narthex.Core
{
    public readonly struct SaveRequested
    {
        public readonly string Reason;
        public SaveRequested(string reason) { Reason = reason; }
    }

    public readonly struct SaveCompleted
    {
        public readonly string Reason;
        public SaveCompleted(string reason) { Reason = reason; }
    }

    public readonly struct SaveFailed
    {
        public readonly string Reason;
        public readonly string Error;
        public SaveFailed(string reason, string error) { Reason = reason; Error = error; }
    }
}
