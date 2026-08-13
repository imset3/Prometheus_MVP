using UnityEngine;

namespace Narthex.Presentation
{
    public sealed class AuthoredVisualScaleMarker : MonoBehaviour
    {
        [SerializeField] private float factor = 1f;
        public float Factor { get => factor; set => factor = value; }
    }
}
