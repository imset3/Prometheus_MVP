using System;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    [Serializable]
    public sealed class HelteStateVfxBinding
    {
        [SerializeField] private HelteCombatState state;
        [SerializeField] private GameObject effectRoot;
        [SerializeField] private Transform anchor;
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private bool followAnchor = true;
        [SerializeField] private bool applyAnchorRotation = true;
        [SerializeField] private bool restartParticleSystems = true;
        [SerializeField] private bool deactivateOnStateExit = true;

        public HelteCombatState State => state;
        public GameObject EffectRoot => effectRoot;
        public Transform Anchor => anchor;
        public Vector3 LocalOffset => localOffset;
        public bool FollowAnchor => followAnchor;
        public bool ApplyAnchorRotation => applyAnchorRotation;
        public bool RestartParticleSystems => restartParticleSystems;
        public bool DeactivateOnStateExit => deactivateOnStateExit;
        public bool IsConfigured => effectRoot != null;
    }

    /// <summary>
    /// Connects replaceable, pre-placed VFX roots to Helte FSM states. Multiple bindings may target the same state,
    /// allowing art to layer particles, trails, flashes, and screen-space cues without changing combat code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeltePatternVfxHost : MonoBehaviour
    {
        [SerializeField] private HelteBossPatternHost patternHost;
        [SerializeField] private HelteStateVfxBinding[] bindings = Array.Empty<HelteStateVfxBinding>();

        public bool HasValidSetup => patternHost != null && bindings != null;
        public int ConfiguredBindingCount
        {
            get
            {
                if (bindings == null) return 0;
                var count = 0;
                foreach (var binding in bindings)
                    if (binding != null && binding.IsConfigured)
                        count++;
                return count;
            }
        }

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("HeltePatternVfxHost requires a HelteBossPatternHost and a binding array.", this);
                enabled = false;
                return;
            }

            DeactivateAll();
        }

        private void OnEnable()
        {
            if (patternHost == null) return;
            patternHost.StateChanged += HandleStateChanged;
            DeactivateAll();
        }

        private void OnDisable()
        {
            if (patternHost != null) patternHost.StateChanged -= HandleStateChanged;
            DeactivateAll();
        }

        private void LateUpdate()
        {
            if (bindings == null) return;
            foreach (var binding in bindings)
            {
                if (binding == null || !binding.IsConfigured || !binding.FollowAnchor ||
                    binding.Anchor == null || !binding.EffectRoot.activeSelf)
                    continue;
                ApplyAnchor(binding);
            }
        }

        private void HandleStateChanged(HelteCombatState state)
        {
            if (bindings == null) return;

            foreach (var binding in bindings)
            {
                if (binding == null || !binding.IsConfigured) continue;
                if (binding.State != state)
                {
                    if (binding.DeactivateOnStateExit) SetActive(binding, false);
                    continue;
                }

                SetActive(binding, true);
            }
        }

        private static void SetActive(HelteStateVfxBinding binding, bool active)
        {
            var root = binding.EffectRoot;
            if (root == null) return;

            if (!active)
            {
                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                root.SetActive(false);
                return;
            }

            ApplyAnchor(binding);
            root.SetActive(true);
            if (!binding.RestartParticleSystems) return;

            foreach (var particles in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Play(true);
            }
        }

        private static void ApplyAnchor(HelteStateVfxBinding binding)
        {
            if (binding.Anchor == null || binding.EffectRoot == null) return;
            binding.EffectRoot.transform.position =
                binding.Anchor.TransformPoint(binding.LocalOffset);
            if (binding.ApplyAnchorRotation)
                binding.EffectRoot.transform.rotation = binding.Anchor.rotation;
        }

        private void DeactivateAll()
        {
            if (bindings == null) return;
            foreach (var binding in bindings)
                if (binding != null && binding.IsConfigured)
                    SetActive(binding, false);
        }
    }
}
