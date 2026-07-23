using System;
using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public static class TutorialAccessibilityPolicy
    {
        public static int ResolveFontSize(int currentSize, int minimumSize) => Mathf.Max(currentSize, minimumSize);
        public static float ResolvePanelAlpha(float currentAlpha, float minimumAlpha) =>
            currentAlpha <= 0f ? 0f : Mathf.Max(currentAlpha, Mathf.Clamp01(minimumAlpha));
    }

    /// <summary>
    /// Applies the tutorial's default readability profile to pre-placed UI. It exposes one bounded motion
    /// channel and keeps subtitle contrast/font sizing consistent without creating runtime UI.
    /// </summary>
    public sealed class TutorialAccessibilityHost : MonoBehaviour
    {
        [SerializeField] private CameraFollowHost cameraFollowHost;
        [SerializeField] private Text[] readableTexts = Array.Empty<Text>();
        [SerializeField] private Image[] contrastPanels = Array.Empty<Image>();
        [SerializeField] private EnemyAttackHost[] enemyAttackHosts = Array.Empty<EnemyAttackHost>();
        [SerializeField, Range(0f, 1f)] private float motionIntensity = 0.65f;
        [SerializeField, Range(0f, 1f)] private float flashIntensity = 0.45f;
        [SerializeField, Min(12)] private int minimumSubtitleFontSize = 20;
        [SerializeField, Range(0f, 1f)] private float minimumPanelAlpha = 0.88f;

        public bool HasValidSetup => cameraFollowHost != null && readableTexts != null && readableTexts.Length >= 4 &&
                                     contrastPanels != null && contrastPanels.Length >= 3 && HasFlashTargets &&
                                     HasNoMissingReferences();
        public float MotionIntensity => motionIntensity;
        public float FlashIntensity => flashIntensity;
        public bool HasFlashTargets => enemyAttackHosts != null && enemyAttackHosts.Length > 0;
        public int MinimumSubtitleFontSize => minimumSubtitleFontSize;
        public bool UsesTextualCombatSemantics => true;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialAccessibilityHost requires camera, subtitle labels, and contrast panels.", this);
                enabled = false;
                return;
            }
            ApplyProfile();
        }

        public void SetMotionIntensity(float intensity)
        {
            motionIntensity = Mathf.Clamp01(intensity);
            if (cameraFollowHost != null) cameraFollowHost.SetMotionIntensity(motionIntensity);
        }

        public void SetFlashIntensity(float intensity)
        {
            flashIntensity = Mathf.Clamp01(intensity);
            foreach (var attackHost in enemyAttackHosts)
                if (attackHost != null) attackHost.SetWarningFlashIntensity(flashIntensity);
        }

        public void ApplyProfile()
        {
            cameraFollowHost.SetMotionIntensity(motionIntensity);
            foreach (var label in readableTexts)
                label.fontSize = TutorialAccessibilityPolicy.ResolveFontSize(label.fontSize, minimumSubtitleFontSize);
            foreach (var panel in contrastPanels)
            {
                var color = panel.color;
                color.a = TutorialAccessibilityPolicy.ResolvePanelAlpha(color.a, minimumPanelAlpha);
                panel.color = color;
            }
            foreach (var attackHost in enemyAttackHosts)
                if (attackHost != null) attackHost.SetWarningFlashIntensity(flashIntensity);
        }

        private bool HasNoMissingReferences()
        {
            foreach (var label in readableTexts)
                if (label == null) return false;
            foreach (var panel in contrastPanels)
                if (panel == null) return false;
            foreach (var attackHost in enemyAttackHosts)
                if (attackHost == null) return false;
            return true;
        }
    }
}
