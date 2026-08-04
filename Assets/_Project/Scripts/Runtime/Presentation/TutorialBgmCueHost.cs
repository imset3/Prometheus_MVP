using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Audio-only hook for the tutorial. Clips are optional until the music export
    /// arrives; dropping them into Resources/TutorialBgm requires no flow changes.
    /// </summary>
    public sealed class TutorialBgmCueHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip introClip;
        [SerializeField] private AudioClip hiddenRoomClip;
        [SerializeField] private AudioClip trainingClip;
        [SerializeField] private AudioClip exteriorCombatClip;
        [SerializeField] private AudioClip bossClip;
        [SerializeField, Min(0f)] private float crossfadeSeconds = 0.75f;

        private AudioClip currentClip;

        private void Awake()
        {
            if (serviceRoot == null) serviceRoot = FindFirstObjectByType<ServiceRoot>();
            if (musicSource == null) musicSource = GetComponent<AudioSource>();
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            TryLoadMissingClips();
        }

        private void OnEnable()
        {
            serviceRoot?.Initialize();
            serviceRoot?.Events?.Subscribe<TutorialLocationChanged>(HandleLocationChanged);
            serviceRoot?.Events?.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialLocationChanged>(HandleLocationChanged);
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
        }

        public void SetCue(string cueId)
        {
            var clip = cueId switch
            {
                "INTRO" => introClip,
                "HIDDEN_ROOM" => hiddenRoomClip,
                "TRAINING" => trainingClip,
                "EXTERIOR_COMBAT" => exteriorCombatClip,
                "BOSS" => bossClip,
                _ => null
            };
            if (clip == currentClip) return;
            currentClip = clip;
            if (musicSource == null || clip == null) return;
            musicSource.clip = clip;
            musicSource.volume = 1f;
            musicSource.Play();
        }

        private void HandleLocationChanged(TutorialLocationChanged message)
        {
            SetCue(message.LocationName switch
            {
                "숨겨진 방" => "HIDDEN_ROOM",
                "훈련장" => "TRAINING",
                "본부 외곽" or "본부 외곽 통로" or "나디르 선착장 진입로" => "EXTERIOR_COMBAT",
                "나디르 선착장" => "BOSS",
                _ => "INTRO"
            });
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            if (message.QuestId == "QST-TUTO-008") SetCue("BOSS");
            else if (message.QuestId == "QST-TUTO-007-A" || message.QuestId == "QST-TUTO-007-B")
                SetCue("EXTERIOR_COMBAT");
            else if (!string.IsNullOrEmpty(message.QuestId) && message.QuestId.StartsWith("QST-TUTO-00"))
                SetCue("TRAINING");
        }

        private void TryLoadMissingClips()
        {
            introClip ??= Resources.Load<AudioClip>("TutorialBgm/Intro");
            hiddenRoomClip ??= Resources.Load<AudioClip>("TutorialBgm/HiddenRoom");
            trainingClip ??= Resources.Load<AudioClip>("TutorialBgm/Training");
            exteriorCombatClip ??= Resources.Load<AudioClip>("TutorialBgm/ExteriorCombat");
            bossClip ??= Resources.Load<AudioClip>("TutorialBgm/Boss");
        }
    }
}
