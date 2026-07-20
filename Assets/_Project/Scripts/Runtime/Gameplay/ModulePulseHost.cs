using Narthex.Content;
using Narthex.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Narthex.Gameplay
{
    public sealed class ModulePulseHost : MonoBehaviour
    {
        [SerializeField] private CombatActorHost sourceActor;
        [SerializeField] private AbilityDefinition ability;
        [SerializeField] private Collider2D pulseHitbox;
        [SerializeField] private LayerMask targetLayers = -1;
        [SerializeField, Min(1)] private int damage = 35;
        [SerializeField] private GameObject projectileVisual;
        [SerializeField, Min(0.1f)] private float travelDistance = 8f;
        [SerializeField, Min(0.05f)] private float travelSeconds = 0.45f;

        private readonly Collider2D[] results = new Collider2D[8];
        private readonly HashSet<string> hitActorIds = new HashSet<string>();
        private Vector3 originLocalPosition;
        private float launchedAt;
        private bool projectileActive;
        private bool subscribed;

        public bool HasValidSetup => sourceActor != null && ability != null && pulseHitbox != null && projectileVisual != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("ModulePulseHost requires pre-placed actor, ability, and hitbox references.", this);
                enabled = false;
                return;
            }

            pulseHitbox.enabled = false;
            originLocalPosition = transform.localPosition;
            projectileVisual.SetActive(false);
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void Start()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (subscribed) sourceActor.Events.Unsubscribe<AbilityRequested>(HandleAbilityRequested);
            subscribed = false;
            if (pulseHitbox != null) pulseHitbox.enabled = false;
            if (projectileVisual != null) projectileVisual.SetActive(false);
            projectileActive = false;
        }

        private void Update()
        {
            TrySubscribe();
            if (!projectileActive) return;

            var progress = Mathf.Clamp01((Time.time - launchedAt) / travelSeconds);
            transform.localPosition = originLocalPosition + Vector3.right * (travelDistance * progress);
            ApplyProjectileHits();
            if (progress < 1f) return;

            projectileActive = false;
            pulseHitbox.enabled = false;
            projectileVisual.SetActive(false);
            transform.localPosition = originLocalPosition;
        }

        private void TrySubscribe()
        {
            if (subscribed || sourceActor == null || sourceActor.Events == null) return;
            sourceActor.Events.Subscribe<AbilityRequested>(HandleAbilityRequested);
            subscribed = true;
        }

        private void HandleAbilityRequested(AbilityRequested message)
        {
            if (message.CasterId != sourceActor.ActorId || message.AbilityId != ability.StableId) return;
            if (sourceActor.Runtime == null || sourceActor.CombatSystem == null || !sourceActor.Runtime.IsAlive) return;

            transform.localPosition = originLocalPosition;
            launchedAt = Time.time;
            projectileActive = true;
            pulseHitbox.enabled = true;
            projectileVisual.SetActive(true);
            hitActorIds.Clear();
            ApplyProjectileHits();
        }

        private void ApplyProjectileHits()
        {
            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = pulseHitbox.Overlap(filter, results);
            for (var index = 0; index < count; index++)
            {
                var target = results[index].GetComponentInParent<CombatActorHost>();
                if (target == null || target.Kind == sourceActor.Kind || !hitActorIds.Add(target.ActorId)) continue;
                sourceActor.CombatSystem.TryApplyDamage(
                    target.ActorId,
                    new DamagePacket(sourceActor.ActorId, ability.StableId, damage));
            }
        }
    }
}
