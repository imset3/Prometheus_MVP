using UnityEngine;

namespace Narthex.Content
{
    public enum HitboxShape { Box, Circle, Capsule }
    public enum KnockbackSpace { Local, World, AwayFromSource }

    [CreateAssetMenu(menuName = "Narthex/Content/Hitbox Definition")]
    public sealed class HitboxDefinition : DefinitionBase
    {
        public HitboxShape Shape;
        public string AnchorId;
        public Vector2 LocalOffset;
        public Vector2 Size = Vector2.one;
        public bool FacingMirror = true;
        public float ActiveDelay;
        public float ActiveDuration = 0.1f;
        public LayerMask TargetLayerMask;
        public int MaxHitsPerCast = 1;
        public float HitInterval;
        public string[] EffectIds;
        public int Damage = 1;
        public float HitstopDuration;
        public Vector2 KnockbackVector;
        public KnockbackSpace KnockbackSpace;
    }
}
