using System;
using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class CombatHealthTextPresenter : MonoBehaviour
    {
        [SerializeField] private CombatActorHost actorHost;
        [SerializeField] private CombatActorHost[] alternateActorHosts = Array.Empty<CombatActorHost>();
        [SerializeField] private Text healthText;
        [SerializeField] private string label = "체력";
        [SerializeField] private string defeatedText = "처치됨";
        [SerializeField] private bool hideWhenNoActiveActor = true;
        [SerializeField] private bool useActorKindLabel;
        [SerializeField] private bool hideBossActors;
        [SerializeField] private string enemyLabel = "적";
        [SerializeField] private string bossLabel = "헬테";

        public bool HasValidSetup => actorHost != null && healthText != null;
        public int AlternateActorCount => alternateActorHosts?.Length ?? 0;
        public bool HidesBossActors => hideBossActors;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("CombatHealthTextPresenter requires pre-placed CombatActorHost and UI Text references.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (actorHost == null || healthText == null) return;

            var visibleActor = FindVisibleActor();
            if (visibleActor == null)
            {
                if (hideWhenNoActiveActor) healthText.enabled = false;
                return;
            }

            healthText.enabled = true;
            var visibleLabel = ResolveLabel(visibleActor);
            healthText.text = visibleActor.Runtime.IsAlive
                ? $"{visibleLabel} {visibleActor.Runtime.CurrentHealth}/{visibleActor.Runtime.MaxHealth}"
                : defeatedText;
        }

        private CombatActorHost FindVisibleActor()
        {
            if (IsVisible(actorHost)) return actorHost;
            if (alternateActorHosts == null) return null;

            foreach (var alternateActorHost in alternateActorHosts)
            {
                if (IsVisible(alternateActorHost)) return alternateActorHost;
            }

            return null;
        }

        private bool IsVisible(CombatActorHost candidate) =>
            candidate != null && candidate.gameObject.activeInHierarchy && candidate.Runtime != null &&
            !(hideBossActors && candidate.Kind == CombatActorKind.Boss);

        private string ResolveLabel(CombatActorHost visibleActor)
        {
            if (!useActorKindLabel || visibleActor.Runtime == null) return label;

            return visibleActor.Kind switch
            {
                CombatActorKind.Boss => bossLabel,
                CombatActorKind.Enemy => enemyLabel,
                _ => label
            };
        }
    }
}
