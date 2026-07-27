using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Keeps a level-designer-owned thin door renderer referenced from the technical gate proxy.
    /// </summary>
    public sealed class TutorialGateVisualBindingHost : MonoBehaviour
    {
        [SerializeField] private Renderer boundRenderer;
        public Renderer BoundRenderer => boundRenderer;
    }
}
