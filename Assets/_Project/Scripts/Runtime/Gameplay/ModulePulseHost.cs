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
        [SerializeField, Min(0.01f)] private float activeSeconds = 0.12f;

        private readonly Collider2D[] results = new Collider2D[8];
        private readonly HashSet<string> hitActorIds = new HashSet<string>();
        private float deactivateAt;
        private bool subscribed;

        public bool HasValidSetup => sourceActor != null && ability != null && pulseHitbox != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("ModulePulseHost requires pre-placed actor, ability, and hitbox references.", this);
                enabled = false;
                return;
            }

            pulseHitbox.enabled = false;
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
        }

        private void Update()
        {
            TrySubscribe();
            if (pulseHitbox != null && pulseHitbox.enabled && Time.time >= deactivateAt)
                pulseHitbox.enabled = false;
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

            deactivateAt = Time.time + activeSeconds;
            pulseHitbox.enabled = true;

            var filter = ContactFilter2D.noFilter;
            filter.SetLayerMask(targetLayers);
            filter.useTriggers = true;
            var count = pulseHitbox.Overlap(filter, results);
            hitActorIds.Clear();
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
