using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Routes tutorial-wide UI and progression cues through pre-placed audio sources.
    /// Leave clips empty during production setup; adding clips later needs no code change.
    /// </summary>
    public sealed class TutorialSfxCueHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private AudioSource worldSource;
        [Header("Tutorial UI / Flow")]
        [SerializeField] private AudioClip narrativeCue;
        [SerializeField] private AudioClip objectiveUpdatedCue;
        [SerializeField] private AudioClip tutorialCompletedCue;

        public bool HasValidSetup => serviceRoot != null && uiSource != null && worldSource != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialSfxCueHost requires pre-placed ServiceRoot and audio sources.", this);
                enabled = false;
                return;
            }

        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialNarrativeChanged>(HandleNarrativeChanged);
            serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot.Events.Subscribe<TutorialCompleted>(HandleTutorialCompleted);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialNarrativeChanged>(HandleNarrativeChanged);
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            serviceRoot?.Events?.Unsubscribe<TutorialCompleted>(HandleTutorialCompleted);
        }

        private void HandleNarrativeChanged(TutorialNarrativeChanged message) => PlayUi(narrativeCue);
        private void HandleObjectiveChanged(TutorialObjectiveChanged message) => PlayUi(objectiveUpdatedCue);
        private void HandleTutorialCompleted(TutorialCompleted message) => PlayUi(tutorialCompletedCue);

        private void PlayUi(AudioClip clip)
        {
            if (clip != null) uiSource.PlayOneShot(clip);
        }
    }
}
