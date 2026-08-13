namespace Narthex.Gameplay
{
    /// <summary>
    /// Implemented by authored tutorial systems that own transient section state.
    /// Instances are serialized on TutorialRetrySection; no runtime discovery is used.
    /// </summary>
    public interface ITutorialRetryParticipant
    {
        void ResetForTutorialRetry();
    }
}
