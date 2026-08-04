using System;
using System.Collections;
using UnityEngine;

namespace Narthex.Gameplay
{
    /// <summary>
    /// Optional encounter support for Theus. It is intentionally target-driven so the
    /// same component can be attached to a future companion prefab without changing
    /// Helte's FSM or player controls.
    /// </summary>
    public sealed class TheusCombatSupportHost : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float autoAttackCooldownSeconds = 2.2f;
        [SerializeField, Min(1)] private int autoAttackDamage = 15;
        [SerializeField, Min(0.1f)] private float autoSkillCooldownSeconds = 6.5f;
        [SerializeField, Min(1)] private int autoSkillDamage = 50;
        [SerializeField, Min(1)] private int autoSkillHealAmount = 35;
        [SerializeField] private bool autoSkillEnabled = true;

        private CombatActorHost targetActor;
        private CombatActorHost playerActor;
        private Coroutine supportRoutine;
        private float nextAutoAttackAt;
        private float nextAutoSkillAt;

        public bool IsSupporting => supportRoutine != null;
        public int AutoAttackCount { get; private set; }
        public int AutoSkillCount { get; private set; }
        public event Action AutoAttackTriggered;
        public event Action AutoSkillTriggered;

        public void StartSupport(CombatActorHost target, CombatActorHost player)
        {
            StopSupport();
            targetActor = target;
            playerActor = player;
            if (targetActor == null || targetActor.CombatSystem == null) return;

            AutoAttackCount = 0;
            AutoSkillCount = 0;
            nextAutoAttackAt = Time.time + autoAttackCooldownSeconds;
            nextAutoSkillAt = Time.time + autoSkillCooldownSeconds;
            supportRoutine = StartCoroutine(SupportLoop());
        }

        public void StopSupport()
        {
            if (supportRoutine != null) StopCoroutine(supportRoutine);
            supportRoutine = null;
            targetActor = null;
            playerActor = null;
        }

        private IEnumerator SupportLoop()
        {
            while (targetActor != null && targetActor.Runtime != null && targetActor.Runtime.IsAlive)
            {
                if (Time.time >= nextAutoAttackAt)
                {
                    nextAutoAttackAt = Time.time + autoAttackCooldownSeconds;
                    if (TryApplySupportDamage(autoAttackDamage, "THEUS-AUTO-ATTACK"))
                    {
                        AutoAttackCount++;
                        AutoAttackTriggered?.Invoke();
                    }
                }

                if (autoSkillEnabled && Time.time >= nextAutoSkillAt)
                {
                    nextAutoSkillAt = Time.time + autoSkillCooldownSeconds;
                    if (TryApplySupportDamage(autoSkillDamage, "THEUS-AUTO-SKILL"))
                    {
                        AutoSkillCount++;
                        if (playerActor != null && playerActor.Runtime != null && playerActor.Runtime.IsAlive)
                        {
                            playerActor.Runtime.CurrentHealth = Mathf.Min(
                                playerActor.Runtime.MaxHealth,
                                playerActor.Runtime.CurrentHealth + autoSkillHealAmount);
                        }
                        AutoSkillTriggered?.Invoke();
                    }
                }

                yield return null;
            }

            supportRoutine = null;
        }

        private bool TryApplySupportDamage(int damage, string hitboxId)
        {
            if (targetActor == null || targetActor.Runtime == null || !targetActor.Runtime.IsAlive ||
                targetActor.CombatSystem == null)
                return false;

            return targetActor.CombatSystem.TryApplyDamage(
                targetActor.ActorId,
                new DamagePacket("THEUS-SUPPORT", hitboxId, Mathf.Max(1, damage)));
        }
    }
}
