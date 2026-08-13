using UnityEngine;

namespace Narthex.Gameplay
{
    [System.Serializable]
    public sealed class TutorialRetryObjectState
    {
        [SerializeField] private GameObject target;
        [SerializeField] private bool active;

        public GameObject Target => target;
        public bool Active => active;

#if UNITY_EDITOR
        public TutorialRetryObjectState(GameObject authoredTarget, bool authoredActive)
        {
            target = authoredTarget;
            active = authoredActive;
        }
#endif
    }

    /// <summary>Authored retry contract shown in the hierarchy for each tutorial section.</summary>
    public sealed class TutorialRetrySection : MonoBehaviour
    {
        [SerializeField] private string questId;
        [SerializeField] private Transform checkpoint;
        [SerializeField] private GameObject[] sectionObjects = System.Array.Empty<GameObject>();
        [SerializeField] private MonoBehaviour[] retryParticipants = System.Array.Empty<MonoBehaviour>();
        [SerializeField] private TutorialRetryObjectState[] initialObjectStates =
            System.Array.Empty<TutorialRetryObjectState>();

        public string QuestId => questId;
        public Transform Checkpoint => checkpoint;
        public GameObject[] SectionObjects => sectionObjects;
        public MonoBehaviour[] RetryParticipants => retryParticipants;
        public TutorialRetryObjectState[] InitialObjectStates => initialObjectStates;
        public bool HasValidSetup => !string.IsNullOrWhiteSpace(questId) && checkpoint != null &&
                                     retryParticipants != null && initialObjectStates != null;

        public void RestoreAuthoredState()
        {
            foreach (var state in initialObjectStates)
                if (state?.Target != null)
                    state.Target.SetActive(state.Active);

            foreach (var participant in retryParticipants)
                if (participant is ITutorialRetryParticipant retryParticipant)
                    retryParticipant.ResetForTutorialRetry();
        }

#if UNITY_EDITOR
        public void Configure(
            string authoredQuestId,
            Transform authoredCheckpoint,
            MonoBehaviour[] authoredParticipants = null,
            TutorialRetryObjectState[] authoredStates = null)
        {
            questId = authoredQuestId;
            checkpoint = authoredCheckpoint;
            retryParticipants = authoredParticipants ?? System.Array.Empty<MonoBehaviour>();
            initialObjectStates = authoredStates ?? System.Array.Empty<TutorialRetryObjectState>();
            sectionObjects = System.Array.ConvertAll(initialObjectStates, state => state?.Target);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
