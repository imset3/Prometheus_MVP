using System;
using System.Collections;
using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Theus' automatic ranged support. It uses a pre-placed visual pool and applies
    /// damage through the shared combat system when a projectile reaches its target.
    /// </summary>
    public sealed class TutorialTheusRangedSupportHost : MonoBehaviour
    {
        [SerializeField] private CombatActorHost playerSourceActor;
        [SerializeField] private PlayerInputHost inputHost;
        [SerializeField] private TutorialTheusLightFormHost lightFormHost;
        [SerializeField] private TutorialBossArenaHost bossArenaHost;
        [SerializeField] private bool bossCombatOnly;
        [SerializeField] private GameObject[] projectilePool = Array.Empty<GameObject>();
        [SerializeField] private SpriteRenderer[] projectileRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField] private GameObject[] impactPool = Array.Empty<GameObject>();
        [SerializeField] private SpriteRenderer[] impactRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField, Min(1)] private int damage = 8;
        [SerializeField, Min(0.1f)] private float cooldown = 2.4f;
        [SerializeField, Min(0.1f)] private float range = 14f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 12f;
        [SerializeField] private Vector3 launchOffset = new(0.45f, 0.1f, 0f);
        [SerializeField, Min(0.05f)] private float impactDuration = 0.22f;
        [SerializeField, Min(0f)] private float impactExpansion = 0.45f;
        [Header("Focused Volley Skill")]
        [SerializeField] private bool startsFocusedVolleyUnlocked;
        [SerializeField, Min(1)] private int focusedVolleyShots = 5;
        [SerializeField, Min(1)] private int focusedVolleyDamage = 12;
        [SerializeField, Min(0.05f)] private float focusedVolleyInterval = 0.18f;
        [SerializeField, Min(0.1f)] private float focusedVolleyCooldown = 8f;
        [SerializeField, Min(1f)] private float focusedVolleyFinalScale = 1.35f;

        private CombatActorHost[] targets = Array.Empty<CombatActorHost>();
        private CombatActorHost[] projectileTargets = Array.Empty<CombatActorHost>();
        private int[] projectileDamage = Array.Empty<int>();
        private bool[] projectileIsFocusedVolley = Array.Empty<bool>();
        private float[] projectileScaleMultipliers = Array.Empty<float>();
        private Vector3[] projectileBaseScales = Array.Empty<Vector3>();
        private float[] impactEndsAt = Array.Empty<float>();
        private Vector3[] impactBaseScales = Array.Empty<Vector3>();
        private float[] impactScaleMultipliers = Array.Empty<float>();
        private float cooldownEndsAt;
        private float focusedVolleyCooldownEndsAt;
        private Coroutine focusedVolleyRoutine;
        private bool focusedVolleyUnlocked;

        public bool HasValidSetup => playerSourceActor != null && inputHost != null && projectilePool != null &&
                                     projectileRenderers != null && projectilePool.Length >= 3 &&
                                     projectilePool.Length == projectileRenderers.Length &&
                                     Array.TrueForAll(projectilePool, item => item != null) &&
                                     Array.TrueForAll(projectileRenderers, item => item != null);
        public bool HasImpactSetup => projectilePool != null && impactPool != null && impactRenderers != null &&
                                      impactPool.Length == projectilePool.Length &&
                                      impactPool.Length == impactRenderers.Length &&
                                      Array.TrueForAll(impactPool, item => item != null) &&
                                      Array.TrueForAll(impactRenderers, item => item != null);
        public bool IsFocusedVolleyUnlocked => focusedVolleyUnlocked;
        public bool IsFocusedVolleyExecuting => focusedVolleyRoutine != null;
        public float FocusedVolleyCooldownDuration => focusedVolleyCooldown;
        public float FocusedVolleyCooldownRemaining => Mathf.Max(0f, focusedVolleyCooldownEndsAt - Time.time);
        public bool IsFocusedVolleyReady => CanStartFocusedVolley(
            focusedVolleyUnlocked,
            IsFocusedVolleyExecuting,
            FocusedVolleyCooldownRemaining,
            playerSourceActor?.Runtime?.IsAlive == true,
            lightFormHost != null && lightFormHost.IsLightFormActive);
        public event Action<CombatActorHost> SupportHit;
        public event Action FocusedVolleyStarted;
        public event Action<int, CombatActorHost> FocusedVolleyShot;
        public event Action FocusedVolleyFinished;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialTheusRangedSupportHost requires player input, combat source, and a projectile pool.", this);
                enabled = false;
                return;
            }

            projectileTargets = new CombatActorHost[projectilePool.Length];
            projectileDamage = new int[projectilePool.Length];
            projectileIsFocusedVolley = new bool[projectilePool.Length];
            projectileScaleMultipliers = new float[projectilePool.Length];
            projectileBaseScales = new Vector3[projectilePool.Length];
            for (var index = 0; index < projectilePool.Length; index++)
            {
                projectileBaseScales[index] = projectilePool[index].transform.localScale;
                projectileScaleMultipliers[index] = 1f;
                projectilePool[index].SetActive(false);
            }
            if (HasImpactSetup)
            {
                impactEndsAt = new float[impactPool.Length];
                impactBaseScales = new Vector3[impactPool.Length];
                impactScaleMultipliers = new float[impactPool.Length];
                for (var index = 0; index < impactPool.Length; index++)
                {
                    impactBaseScales[index] = impactPool[index].transform.localScale;
                    impactScaleMultipliers[index] = 1f;
                    impactPool[index].SetActive(false);
                }
            }
            focusedVolleyUnlocked = startsFocusedVolleyUnlocked;
            RefreshTargets();
        }

        private void OnEnable()
        {
            if (inputHost != null) inputHost.TheusSkillRequested += TryStartFocusedVolley;
        }

        private void OnDisable()
        {
            if (inputHost != null) inputHost.TheusSkillRequested -= TryStartFocusedVolley;
            if (focusedVolleyRoutine != null) StopCoroutine(focusedVolleyRoutine);
            focusedVolleyRoutine = null;
            if (projectilePool != null && projectileTargets.Length == projectilePool.Length)
                for (var index = 0; index < projectilePool.Length; index++) Deactivate(index);
        }

        private void Update()
        {
            UpdateImpacts();
            UpdateProjectiles();
            if (bossCombatOnly && (bossArenaHost == null || !bossArenaHost.CombatActive)) return;
            if (focusedVolleyRoutine != null) return;
            if (Time.time < cooldownEndsAt || lightFormHost != null && lightFormHost.IsLightFormActive) return;
            var target = FindNearestTarget();
            if (target == null) return;
            var slot = Array.FindIndex(projectilePool, item => item != null && !item.activeSelf);
            if (slot < 0) return;
            Launch(slot, target, damage, 1f, false);
            cooldownEndsAt = Time.time + cooldown;
        }

        public void SetFocusedVolleyUnlocked(bool unlocked)
        {
            focusedVolleyUnlocked = unlocked;
            if (unlocked || focusedVolleyRoutine == null) return;
            StopCoroutine(focusedVolleyRoutine);
            focusedVolleyRoutine = null;
        }

        public void TryStartFocusedVolley()
        {
            if (!IsFocusedVolleyReady || FindNearestTarget() == null) return;
            focusedVolleyCooldownEndsAt = Time.time + focusedVolleyCooldown;
            focusedVolleyRoutine = StartCoroutine(RunFocusedVolley());
        }

        private IEnumerator RunFocusedVolley()
        {
            FocusedVolleyStarted?.Invoke();
            for (var shotIndex = 0; shotIndex < focusedVolleyShots; shotIndex++)
            {
                if (!focusedVolleyUnlocked || playerSourceActor?.Runtime?.IsAlive != true ||
                    lightFormHost != null && lightFormHost.IsLightFormActive)
                    break;

                var target = FindNearestTarget();
                if (target == null) break;
                var slot = Array.FindIndex(projectilePool, item => item != null && !item.activeSelf);
                if (slot < 0)
                {
                    yield return new WaitForSeconds(focusedVolleyInterval);
                    shotIndex--;
                    continue;
                }

                var finalShot = shotIndex == focusedVolleyShots - 1;
                Launch(slot, target, focusedVolleyDamage,
                    ResolveFocusedVolleyScale(shotIndex, focusedVolleyShots, focusedVolleyFinalScale), true);
                FocusedVolleyShot?.Invoke(shotIndex, target);
                if (!finalShot) yield return new WaitForSeconds(focusedVolleyInterval);
            }

            focusedVolleyRoutine = null;
            FocusedVolleyFinished?.Invoke();
        }

        private void RefreshTargets()
        {
            targets = FindObjectsByType<CombatActorHost>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private CombatActorHost FindNearestTarget()
        {
            if (targets == null || targets.Length == 0) RefreshTargets();
            CombatActorHost best = null;
            var bestDistance = range * range;
            foreach (var candidate in targets)
            {
                if (!IsEligible(candidate)) continue;
                var distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance > bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private static bool IsEligible(CombatActorHost candidate)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.Runtime == null ||
                !candidate.Runtime.IsAlive || candidate.Kind == CombatActorKind.Player) return false;
            var cursor = candidate.transform;
            while (cursor != null)
            {
                if (cursor.name == "TrainingPhaseContents" || cursor.name.Contains("훈련")) return false;
                cursor = cursor.parent;
            }
            return true;
        }

        private void Launch(int slot, CombatActorHost target, int shotDamage, float scaleMultiplier, bool focusedVolley)
        {
            projectileTargets[slot] = target;
            projectileDamage[slot] = Mathf.Max(1, shotDamage);
            projectileIsFocusedVolley[slot] = focusedVolley;
            projectileScaleMultipliers[slot] = Mathf.Max(1f, scaleMultiplier);
            var projectile = projectilePool[slot];
            projectile.transform.localScale = projectileBaseScales[slot] * projectileScaleMultipliers[slot];
            projectile.transform.position = transform.position + launchOffset;
            var delta = target.transform.position - projectile.transform.position;
            projectileRenderers[slot].flipX = delta.x < 0f;
            projectile.SetActive(true);
        }

        private void UpdateProjectiles()
        {
            for (var index = 0; index < projectilePool.Length; index++)
            {
                var projectile = projectilePool[index];
                if (projectile == null || !projectile.activeSelf) continue;
                var target = projectileTargets[index];
                if (!IsEligible(target))
                {
                    Deactivate(index);
                    continue;
                }

                var destination = target.transform.position + Vector3.up * 0.55f;
                projectile.transform.position = Vector3.MoveTowards(
                    projectile.transform.position,
                    destination,
                    projectileSpeed * Time.deltaTime);
                if ((projectile.transform.position - destination).sqrMagnitude > 0.04f) continue;
                var applied = playerSourceActor.CombatSystem?.TryApplyDamage(
                    target.ActorId,
                    new DamagePacket(playerSourceActor.ActorId,
                        projectileIsFocusedVolley[index]
                            ? "THEUS-FOCUSED-VOLLEY"
                            : "THEUS-SUPPORT-RANGED",
                        projectileDamage[index])) == true;
                if (applied)
                {
                    ShowImpact(index, destination);
                    SupportHit?.Invoke(target);
                }
                Deactivate(index);
            }
        }

        private void ShowImpact(int index, Vector3 position)
        {
            if (!HasImpactSetup || index < 0 || index >= impactPool.Length) return;
            var impact = impactPool[index];
            impact.transform.position = position;
            impactScaleMultipliers[index] = projectileScaleMultipliers[index];
            impact.transform.localScale = impactBaseScales[index] * impactScaleMultipliers[index];
            impactRenderers[index].color = Color.white;
            impactEndsAt[index] = Time.time + impactDuration;
            impact.SetActive(true);
        }

        private void UpdateImpacts()
        {
            if (!HasImpactSetup || impactEndsAt.Length != impactPool.Length) return;
            for (var index = 0; index < impactPool.Length; index++)
            {
                var impact = impactPool[index];
                if (impact == null || !impact.activeSelf) continue;
                var remaining = impactEndsAt[index] - Time.time;
                if (remaining <= 0f)
                {
                    impact.SetActive(false);
                    continue;
                }

                var age = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.05f, impactDuration));
                impact.transform.localScale = impactBaseScales[index] * impactScaleMultipliers[index] *
                                              (1f + impactExpansion * age);
                var color = impactRenderers[index].color;
                color.a = 1f - age;
                impactRenderers[index].color = color;
            }
        }

        private void Deactivate(int index)
        {
            projectileTargets[index] = null;
            projectileDamage[index] = 0;
            projectileIsFocusedVolley[index] = false;
            projectileScaleMultipliers[index] = 1f;
            projectilePool[index].transform.localScale = projectileBaseScales[index];
            projectilePool[index].SetActive(false);
        }

        public static bool CanStartFocusedVolley(bool unlocked, bool executing, float cooldownRemaining,
            bool playerAlive, bool lightFormActive) =>
            unlocked && !executing && cooldownRemaining <= 0f && playerAlive && !lightFormActive;

        public static float ResolveFocusedVolleyScale(int shotIndex, int shotCount, float finalScale) =>
            shotCount > 0 && shotIndex == shotCount - 1 ? Mathf.Max(1f, finalScale) : 1f;
    }
}
