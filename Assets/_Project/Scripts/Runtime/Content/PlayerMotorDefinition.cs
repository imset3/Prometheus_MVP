using UnityEngine;

namespace Narthex.Content
{
    [CreateAssetMenu(menuName = "Narthex/Content/Player Motor Definition")]
    public sealed class PlayerMotorDefinition : DefinitionBase
    {
        public float MaxRunSpeed = 6f;
        public float GroundAcceleration = 45f;
        public float JumpVelocity = 12f;
        public float GroundProbeRadius = 0.12f;
        public float DashSpeed = 14f;
        public float DashDuration = 0.16f;
        public float DashCooldown = 0.55f;
        public float GlideFallSpeed = 3f;
    }
}
