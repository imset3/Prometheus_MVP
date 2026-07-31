using System;
using System.Collections.Generic;
using Narthex.Core;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Keeps low-contrast tutorial backplates behind the existing hand-authored
    /// geometry. Visual children are created in Edit mode by the AI Scene Toolkit.
    /// </summary>
    [ExecuteAlways]
    public sealed class TutorialBackgroundPresenter : MonoBehaviour
    {
        [Serializable]
        public sealed class BackgroundEntry
        {
            [SerializeField] private string key;
            [SerializeField] private GameObject visual;

            public string Key => key;
            public GameObject Visual => visual;

            public BackgroundEntry(string key, GameObject visual)
            {
                this.key = key;
                this.visual = visual;
            }
        }

        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private string initialKey = "A";
        [SerializeField] private float cameraSpaceDepth = 20f;
        [SerializeField] private BackgroundEntry[] entries = Array.Empty<BackgroundEntry>();

        private string currentKey;

        public string CurrentKey => currentKey;
        public IReadOnlyList<BackgroundEntry> Entries => entries;

        private void OnEnable()
        {
            if (Application.isPlaying && serviceRoot != null)
            {
                serviceRoot.Initialize();
                serviceRoot.Events.Subscribe<TutorialLocationChanged>(HandleLocationChanged);
            }

            SetCurrentKey(string.IsNullOrWhiteSpace(currentKey) ? initialKey : currentKey);
            FitToCamera();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                serviceRoot?.Events?.Unsubscribe<TutorialLocationChanged>(HandleLocationChanged);
        }

        private void LateUpdate()
        {
            FitToCamera();
        }

        public void Configure(ServiceRoot service, Camera camera, string defaultKey, float depth)
        {
            serviceRoot = service;
            targetCamera = camera;
            initialKey = string.IsNullOrWhiteSpace(defaultKey) ? "A" : defaultKey.Trim().ToUpperInvariant();
            cameraSpaceDepth = Mathf.Max(0.1f, depth);
            SetCurrentKey(initialKey);
            FitToCamera();
        }

        public void UpsertEntry(string key, GameObject visual)
        {
            var normalizedKey = NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
                throw new ArgumentException("Background key is required.", nameof(key));
            if (visual == null)
                throw new ArgumentNullException(nameof(visual));

            for (var index = 0; index < entries.Length; index++)
            {
                if (!string.Equals(entries[index].Key, normalizedKey, StringComparison.OrdinalIgnoreCase)) continue;
                entries[index] = new BackgroundEntry(normalizedKey, visual);
                SetCurrentKey(string.IsNullOrWhiteSpace(currentKey) ? initialKey : currentKey);
                return;
            }

            Array.Resize(ref entries, entries.Length + 1);
            entries[entries.Length - 1] = new BackgroundEntry(normalizedKey, visual);
            SetCurrentKey(string.IsNullOrWhiteSpace(currentKey) ? initialKey : currentKey);
        }

        public void SetCurrentKey(string key)
        {
            currentKey = NormalizeKey(key);
            foreach (var entry in entries)
            {
                if (entry?.Visual == null) continue;
                entry.Visual.SetActive(string.Equals(
                    entry.Key,
                    currentKey,
                    StringComparison.OrdinalIgnoreCase));
            }
        }

        private void HandleLocationChanged(TutorialLocationChanged message)
        {
            SetCurrentKey(ResolveLocationKey(message.LocationName));
        }

        private void FitToCamera()
        {
            if (targetCamera == null) return;

            transform.position = targetCamera.transform.position +
                                 targetCamera.transform.forward * cameraSpaceDepth;
            transform.rotation = targetCamera.transform.rotation;

            var cameraHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 2f * cameraSpaceDepth *
                  Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var cameraWidth = cameraHeight * Mathf.Max(0.1f, targetCamera.aspect);

            foreach (var entry in entries)
            {
                if (entry?.Visual == null) continue;
                var renderer = entry.Visual.GetComponent<SpriteRenderer>();
                if (renderer == null || renderer.sprite == null) continue;
                var spriteSize = renderer.sprite.bounds.size;
                if (spriteSize.x <= 0f || spriteSize.y <= 0f) continue;
                var coverScale = Mathf.Max(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y);
                entry.Visual.transform.localPosition = Vector3.zero;
                entry.Visual.transform.localRotation = Quaternion.identity;
                entry.Visual.transform.localScale = new Vector3(coverScale, coverScale, 1f);
            }
        }

        private static string ResolveLocationKey(string rawLocation)
        {
            if (string.IsNullOrWhiteSpace(rawLocation)) return "A";
            var normalized = rawLocation.Trim().ToUpperInvariant();
            if (normalized.Contains("숨겨진")) return "B";
            if (normalized.Contains("회의장") || normalized.Contains("Z01_HQ")) return "A";
            if (normalized.Contains("복도") || normalized.Contains("Z03_CRYON")) return "C";
            if (normalized.Contains("훈련장") || normalized.Contains("Z02_TRAINING")) return "D";
            if (normalized.Contains("F스테이지") ||
                (normalized.Contains("전투") && normalized.Contains("1")) ||
                normalized.Contains("Z04_EXTERIOR"))
                return "F";
            if (normalized.Contains("G스테이지") ||
                (normalized.Contains("전투") && normalized.Contains("2")) ||
                normalized.Contains("진입로") ||
                normalized.Contains("Z05_EXTERIOR"))
                return "G";
            if (normalized.Contains("선착장") || normalized.Contains("Z06_ORESTORAGE")) return "H";
            if (normalized.Contains("외부")) return "E";
            return NormalizeKey(normalized);
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            return key.Trim().ToUpperInvariant();
        }
    }
}
