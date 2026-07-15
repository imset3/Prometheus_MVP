using UnityEngine;

namespace Narthex.Gameplay
{
    public enum ModuleCastPolicy
    {
        DeusFront,
        PlayerCenter,
        TargetPosition,
        CustomAnchor
    }

    public readonly struct ModuleCastRequest
    {
        public readonly string ModuleId;
        public readonly ModuleCastPolicy Policy;
        public readonly Vector2 TargetPosition;

        public ModuleCastRequest(string moduleId, ModuleCastPolicy policy, Vector2 targetPosition)
        {
            ModuleId = moduleId;
            Policy = policy;
            TargetPosition = targetPosition;
        }
    }

    public sealed class DeusCastAnchor : MonoBehaviour
    {
        [SerializeField] private Transform frontAnchor;
        [SerializeField] private Transform customAnchor;

        public Transform FrontAnchor => frontAnchor != null ? frontAnchor : transform;
        public Transform CustomAnchor => customAnchor != null ? customAnchor : transform;
    }
}
