using System.Collections;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    public sealed class TutorialHitImpactVfxHost : MonoBehaviour
    {
        [SerializeField] private CombatSystemHost combatSystemHost;
        [SerializeField] private SpriteRenderer impactRenderer;
        [SerializeField, Min(0.05f)] private float duration = 0.18f;
        [SerializeField, Min(0f)] private float verticalOffset = 0.75f;
        [SerializeField, Min(0.1f)] private float peakScale = 1.15f;

        private Coroutine playback;

        private void OnEnable()
        {
            if (combatSystemHost != null && combatSystemHost.Initialize())
                combatSystemHost.Events?.Subscribe<HitConfirmed>(HandleHitConfirmed);

            HideImpact();
        }

        private void OnDisable()
        {
            combatSystemHost?.Events?.Unsubscribe<HitConfirmed>(HandleHitConfirmed);
            if (playback != null) StopCoroutine(playback);
            playback = null;
            HideImpact();
        }

        private void HandleHitConfirmed(HitConfirmed message)
        {
            if (impactRenderer == null) return;

            var actors = FindObjectsByType<CombatActorHost>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            CombatActorHost target = null;
            for (var index = 0; index < actors.Length; index++)
            {
                if (actors[index].ActorId != message.TargetId) continue;
                target = actors[index];
                break;
            }

            if (target == null) return;
            impactRenderer.transform.position = target.transform.position + Vector3.up * verticalOffset;

            if (playback != null) StopCoroutine(playback);
            playback = StartCoroutine(PlayImpact());
        }

        private IEnumerator PlayImpact()
        {
            impactRenderer.enabled = true;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var pulse = Mathf.Sin(progress * Mathf.PI);
                impactRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, peakScale, pulse);
                var color = impactRenderer.color;
                color.a = 1f - progress;
                impactRenderer.color = color;
                yield return null;
            }

            playback = null;
            HideImpact();
        }

        private void HideImpact()
        {
            if (impactRenderer == null) return;
            impactRenderer.enabled = false;
            impactRenderer.transform.localScale = Vector3.one;
            var color = impactRenderer.color;
            color.a = 1f;
            impactRenderer.color = color;
        }
    }
}
