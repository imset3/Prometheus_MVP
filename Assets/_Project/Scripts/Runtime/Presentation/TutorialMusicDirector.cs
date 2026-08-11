using System.Collections;
using System.Collections.Generic;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Save;
using UnityEngine;

namespace Narthex.Presentation
{
    public enum TutorialMusicFamily
    {
        Adamas,
        OuterCombat,
        NadirApproach,
        Silent
    }

    public enum TutorialBossMusicLayer
    {
        None,
        PhaseTwo,
        FinalRush,
        Mercy
    }

    /// <summary>
    /// Drives pre-placed, sample-aligned tutorial music sources. It never creates
    /// runtime GameObjects and only reacts to existing location, boss, and completion state.
    /// </summary>
    public sealed class TutorialMusicDirector : MonoBehaviour
    {
        [Header("Runtime")]
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private TutorialBossArenaHost bossArenaHost;
        [SerializeField] private HelteBossPatternHost heltePatternHost;

        [Header("Crossfade sources")]
        [SerializeField] private AudioSource musicSourceA;
        [SerializeField] private AudioSource musicSourceB;
        [SerializeField] private AudioSource outerIntensitySource;

        [Header("Helte synchronized sources")]
        [SerializeField] private AudioSource helteBaseSource;
        [SerializeField] private AudioSource heltePhaseTwoSource;
        [SerializeField] private AudioSource helteFinalSource;
        [SerializeField] private AudioSource victorySource;

        [Header("Clips")]
        [SerializeField] private AudioClip adamasLoop;
        [SerializeField] private AudioClip outerCombatBaseLoop;
        [SerializeField] private AudioClip outerCombatIntensityLoop;
        [SerializeField] private AudioClip helteBaseLoop;
        [SerializeField] private AudioClip heltePhaseTwoLoop;
        [SerializeField] private AudioClip helteFinalLoop;
        [SerializeField] private AudioClip victoryLoop;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float outputLevel = 0.72f;
        [SerializeField, Range(0f, 1f)] private float outerIntensityLevel = 0.28f;
        [SerializeField, Range(0f, 1f)] private float phaseTwoLevel = 0.42f;
        [SerializeField, Range(0f, 1f)] private float finalRushLevel = 0.48f;
        [SerializeField, Range(0f, 1f)] private float mercyDuckLevel = 0.45f;
        [SerializeField, Range(0f, 1f)] private float victoryLevel = 0.62f;
        [SerializeField, Min(0.01f)] private float crossfadeSeconds = 1.2f;
        [SerializeField, Min(0.01f)] private float bossLayerFadeSeconds = 1.875f;
        [SerializeField, Min(0.01f)] private float mercyDuckSeconds = 1.2f;
        [SerializeField] private string initialLocationName = "회의장";

        private readonly Dictionary<AudioSource, Coroutine> fades = new();
        private AudioSource currentRegularSource;
        private TutorialMusicFamily currentFamily = TutorialMusicFamily.Silent;
        private string currentLocationName;
        private bool wasBossActive;
        private bool tutorialCompleted;
        private Coroutine mercyRoutine;

        public bool HasValidSetup =>
            serviceRoot != null && bossArenaHost != null && heltePatternHost != null &&
            musicSourceA != null && musicSourceB != null && outerIntensitySource != null &&
            helteBaseSource != null && heltePhaseTwoSource != null && helteFinalSource != null &&
            victorySource != null &&
            adamasLoop != null && outerCombatBaseLoop != null && outerCombatIntensityLoop != null &&
            helteBaseLoop != null && heltePhaseTwoLoop != null && helteFinalLoop != null &&
            victoryLoop != null;

        public TutorialMusicFamily CurrentFamily => currentFamily;
        public bool BossMusicActive => wasBossActive;
        public float EffectiveMusicVolume => ResolveMusicVolume();
        public AudioClip VictoryClip => victoryLoop;
        public bool VictoryMusicPlaying => victorySource != null && victorySource.isPlaying;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialMusicDirector requires pre-placed sources, music clips, and boss references.", this);
                enabled = false;
                return;
            }

            ConfigureSource(musicSourceA);
            ConfigureSource(musicSourceB);
            ConfigureSource(outerIntensitySource);
            ConfigureSource(helteBaseSource);
            ConfigureSource(heltePhaseTwoSource);
            ConfigureSource(helteFinalSource);
            if (victorySource != null) ConfigureSource(victorySource);
            ValidateAlignedLayers();
        }

        private void OnEnable()
        {
            if (!HasValidSetup) return;
            serviceRoot.Initialize();
            serviceRoot.Events.Subscribe<TutorialLocationChanged>(HandleLocationChanged);
            serviceRoot.Events.Subscribe<TutorialCompleted>(HandleTutorialCompleted);
            heltePatternHost.StateChanged += HandleHelteStateChanged;
        }

        private void Start()
        {
            if (!HasValidSetup) return;
            currentLocationName = string.IsNullOrWhiteSpace(initialLocationName)
                ? "회의장"
                : initialLocationName;
            wasBossActive = bossArenaHost.CombatActive;
            if (wasBossActive) StartHelteFamily();
            else ApplyLocationMusic(currentLocationName);
        }

        private void Update()
        {
            if (!HasValidSetup || tutorialCompleted) return;
            var bossActive = bossArenaHost.CombatActive;
            if (bossActive == wasBossActive) return;
            wasBossActive = bossActive;
            if (bossActive) StartHelteFamily();
            else ApplyLocationMusic(currentLocationName);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialLocationChanged>(HandleLocationChanged);
            serviceRoot?.Events?.Unsubscribe<TutorialCompleted>(HandleTutorialCompleted);
            if (heltePatternHost != null) heltePatternHost.StateChanged -= HandleHelteStateChanged;
            if (mercyRoutine != null) StopCoroutine(mercyRoutine);
            mercyRoutine = null;
            StopAllFades();
            StopAllSources();
        }

        private void HandleLocationChanged(TutorialLocationChanged message)
        {
            currentLocationName = message.LocationName;
            if (!wasBossActive && !tutorialCompleted) ApplyLocationMusic(currentLocationName);
        }

        private void HandleTutorialCompleted(TutorialCompleted message)
        {
            tutorialCompleted = true;
            FadeAllToSilence(crossfadeSeconds);
            if (victorySource != null && victoryLoop != null)
            {
                PrepareScheduled(victorySource, victoryLoop, AudioSettings.dspTime + 0.08d);
                Fade(victorySource, ResolveMusicVolume() * victoryLevel, Mathf.Min(0.8f, crossfadeSeconds));
            }
            currentFamily = TutorialMusicFamily.Silent;
        }

        private void HandleHelteStateChanged(HelteCombatState state)
        {
            if (!wasBossActive || tutorialCompleted) return;
            switch (ResolveBossLayer(state))
            {
                case TutorialBossMusicLayer.PhaseTwo:
                    Fade(heltePhaseTwoSource, ResolveMusicVolume() * phaseTwoLevel, bossLayerFadeSeconds);
                    break;
                case TutorialBossMusicLayer.FinalRush:
                    Fade(heltePhaseTwoSource, ResolveMusicVolume() * phaseTwoLevel, bossLayerFadeSeconds);
                    Fade(helteFinalSource, ResolveMusicVolume() * finalRushLevel, bossLayerFadeSeconds);
                    break;
                case TutorialBossMusicLayer.Mercy:
                    if (mercyRoutine != null) StopCoroutine(mercyRoutine);
                    mercyRoutine = StartCoroutine(DuckForMercy());
                    break;
            }
        }

        private void ApplyLocationMusic(string locationName)
        {
            var family = ResolveLocationFamily(locationName);
            switch (family)
            {
                case TutorialMusicFamily.OuterCombat:
                    StartOuterCombat(IsOuterCombatHighIntensity(locationName));
                    break;
                case TutorialMusicFamily.NadirApproach:
                case TutorialMusicFamily.Adamas:
                default:
                    StartRegular(adamasLoop, family == TutorialMusicFamily.Silent
                        ? TutorialMusicFamily.Adamas
                        : family);
                    break;
            }
        }

        private void StartRegular(AudioClip clip, TutorialMusicFamily family)
        {
            if (currentFamily == family && currentRegularSource != null && currentRegularSource.isPlaying)
                return;

            var next = currentRegularSource == musicSourceA ? musicSourceB : musicSourceA;
            var previous = currentRegularSource;
            var dspStart = AudioSettings.dspTime + 0.08d;
            PrepareScheduled(next, clip, dspStart);
            Fade(next, ResolveMusicVolume(), crossfadeSeconds);
            if (previous != null) Fade(previous, 0f, crossfadeSeconds, true);
            Fade(outerIntensitySource, 0f, crossfadeSeconds, true);
            FadeHelteSourcesToZero(crossfadeSeconds, true);
            currentRegularSource = next;
            currentFamily = family;
        }

        private void StartOuterCombat(bool highIntensity)
        {
            if (currentFamily == TutorialMusicFamily.OuterCombat && currentRegularSource != null &&
                currentRegularSource.isPlaying)
            {
                Fade(outerIntensitySource,
                    highIntensity ? ResolveMusicVolume() * outerIntensityLevel : 0f,
                    bossLayerFadeSeconds);
                return;
            }

            var next = currentRegularSource == musicSourceA ? musicSourceB : musicSourceA;
            var previous = currentRegularSource;
            var dspStart = AudioSettings.dspTime + 0.08d;
            PrepareScheduled(next, outerCombatBaseLoop, dspStart);
            PrepareScheduled(outerIntensitySource, outerCombatIntensityLoop, dspStart);
            Fade(next, ResolveMusicVolume(), crossfadeSeconds);
            Fade(outerIntensitySource,
                highIntensity ? ResolveMusicVolume() * outerIntensityLevel : 0f,
                crossfadeSeconds);
            if (previous != null) Fade(previous, 0f, crossfadeSeconds, true);
            FadeHelteSourcesToZero(crossfadeSeconds, true);
            currentRegularSource = next;
            currentFamily = TutorialMusicFamily.OuterCombat;
        }

        private void StartHelteFamily()
        {
            var dspStart = AudioSettings.dspTime + 0.08d;
            PrepareScheduled(helteBaseSource, helteBaseLoop, dspStart);
            PrepareScheduled(heltePhaseTwoSource, heltePhaseTwoLoop, dspStart);
            PrepareScheduled(helteFinalSource, helteFinalLoop, dspStart);
            Fade(helteBaseSource, ResolveMusicVolume(), crossfadeSeconds);
            Fade(heltePhaseTwoSource, 0f, 0.01f);
            Fade(helteFinalSource, 0f, 0.01f);
            if (currentRegularSource != null) Fade(currentRegularSource, 0f, crossfadeSeconds, true);
            Fade(outerIntensitySource, 0f, crossfadeSeconds, true);
            currentFamily = TutorialMusicFamily.NadirApproach;
        }

        private IEnumerator DuckForMercy()
        {
            Fade(helteBaseSource, ResolveMusicVolume() * mercyDuckLevel, 0.2f);
            yield return new WaitForSeconds(mercyDuckSeconds);
            if (wasBossActive && !tutorialCompleted)
                Fade(helteBaseSource, ResolveMusicVolume(), 0.35f);
            mercyRoutine = null;
        }

        private void FadeAllToSilence(float seconds)
        {
            foreach (var source in AllSources()) Fade(source, 0f, seconds, true);
        }

        private void FadeHelteSourcesToZero(float seconds, bool stopAtZero)
        {
            Fade(helteBaseSource, 0f, seconds, stopAtZero);
            Fade(heltePhaseTwoSource, 0f, seconds, stopAtZero);
            Fade(helteFinalSource, 0f, seconds, stopAtZero);
        }

        private void Fade(AudioSource source, float target, float seconds, bool stopAtZero = false)
        {
            if (source == null) return;
            if (fades.TryGetValue(source, out var active) && active != null) StopCoroutine(active);
            fades[source] = StartCoroutine(FadeRoutine(source, Mathf.Clamp01(target), seconds, stopAtZero));
        }

        private IEnumerator FadeRoutine(AudioSource source, float target, float seconds, bool stopAtZero)
        {
            var start = source.volume;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            source.volume = target;
            if (stopAtZero && target <= 0.0001f) source.Stop();
            fades.Remove(source);
        }

        private void StopAllFades()
        {
            foreach (var fade in fades.Values)
                if (fade != null) StopCoroutine(fade);
            fades.Clear();
        }

        private void StopAllSources()
        {
            foreach (var source in AllSources())
            {
                if (source == null) continue;
                source.Stop();
                source.volume = 0f;
            }
        }

        private IEnumerable<AudioSource> AllSources()
        {
            yield return musicSourceA;
            yield return musicSourceB;
            yield return outerIntensitySource;
            yield return helteBaseSource;
            yield return heltePhaseTwoSource;
            yield return helteFinalSource;
            yield return victorySource;
        }

        private static void PrepareScheduled(AudioSource source, AudioClip clip, double dspStart)
        {
            source.Stop();
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.PlayScheduled(dspStart);
        }

        private static void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
        }

        private float ResolveMusicVolume()
        {
            var settingsMultiplier = 1f;
            if (saveSystemHost != null && saveSystemHost.Initialize() && saveSystemHost.System?.Current?.Settings != null)
            {
                var settings = saveSystemHost.System.Current.Settings;
                settingsMultiplier = Mathf.Clamp01(settings.MasterVolume) * Mathf.Clamp01(settings.MusicVolume);
            }
            return Mathf.Clamp01(outputLevel * settingsMultiplier);
        }

        private void ValidateAlignedLayers()
        {
            if (!AreAligned(outerCombatBaseLoop, outerCombatIntensityLoop))
                Debug.LogWarning("Outer-combat music layers do not have matching sample lengths.", this);
            if (!AreAligned(helteBaseLoop, heltePhaseTwoLoop) || !AreAligned(helteBaseLoop, helteFinalLoop))
                Debug.LogWarning("Helte music layers do not have matching sample lengths.", this);
        }

        public static bool AreAligned(AudioClip first, AudioClip second) =>
            first != null && second != null && first.samples == second.samples && first.frequency == second.frequency;

        public static TutorialMusicFamily ResolveLocationFamily(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName)) return TutorialMusicFamily.Adamas;
            var normalized = locationName.Trim().ToUpperInvariant();
            if (normalized.Contains("F스테이지") || normalized.Contains("G스테이지") ||
                normalized.Contains("Z04_EXTERIOR") || normalized.Contains("Z05_EXTERIOR") ||
                normalized.Contains("전투 1") || normalized.Contains("전투 2") ||
                normalized.Contains("진입로"))
                return TutorialMusicFamily.OuterCombat;
            if (normalized.Contains("선착장") || normalized.Contains("Z06_ORESTORAGE"))
                return TutorialMusicFamily.NadirApproach;
            return TutorialMusicFamily.Adamas;
        }

        public static bool IsOuterCombatHighIntensity(string locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName)) return false;
            var normalized = locationName.Trim().ToUpperInvariant();
            return normalized.Contains("G스테이지") || normalized.Contains("Z05_EXTERIOR") ||
                   normalized.Contains("전투 2") || normalized.Contains("진입로");
        }

        public static TutorialBossMusicLayer ResolveBossLayer(HelteCombatState state) => state switch
        {
            HelteCombatState.PhaseTransition => TutorialBossMusicLayer.PhaseTwo,
            HelteCombatState.FinalRushTransition => TutorialBossMusicLayer.FinalRush,
            HelteCombatState.MercyRetreat => TutorialBossMusicLayer.Mercy,
            _ => TutorialBossMusicLayer.None
        };
    }
}
