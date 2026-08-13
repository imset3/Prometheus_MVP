using Narthex.Gameplay;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>Receives the authored Attack01 Animation Event on the Animator object.</summary>
    public sealed class AttackImpactAnimationRelay : MonoBehaviour
    {
        [SerializeField] private MeleeAttackHost meleeAttackHost;

        public bool HasValidSetup => meleeAttackHost != null;

        // AnimationEvent entry point. Keep the name stable for generated clips.
        public void TriggerImpact()
        {
            meleeAttackHost?.ResolveQueuedImpact();
        }

#if UNITY_EDITOR
        public void Configure(MeleeAttackHost configuredHost)
        {
            meleeAttackHost = configuredHost;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
