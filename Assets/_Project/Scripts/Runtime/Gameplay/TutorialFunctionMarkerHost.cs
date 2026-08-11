using UnityEngine;

namespace Narthex.Gameplay
{
    public enum TutorialFunctionMarkerKind
    {
        Point,
        Wind,
        EnemySpawn,
        Objective,
        Checkpoint,
        Transition,
        TrainingStart,
        TrainingFinish,
        Interaction,
        FallRecovery,
        TilemapClearance
    }

    /// <summary>
    /// Scene-authored source of truth for tutorial functionality. Runtime systems keep
    /// references to this transform instead of copying its position into code.
    /// Moving, rotating, or scaling the marker therefore moves the bound function.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialFunctionMarkerHost : MonoBehaviour
    {
        [SerializeField] private string markerId;
        [SerializeField] private TutorialFunctionMarkerKind kind;
        [SerializeField] private Vector2 gizmoSize = Vector2.one;

        public string MarkerId => markerId;
        public TutorialFunctionMarkerKind Kind => kind;
        public Vector2 WorldDirection
        {
            get
            {
                var direction = (Vector2)transform.up;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            }
        }

        private void OnDrawGizmos()
        {
            var previousMatrix = Gizmos.matrix;
            var previousColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.color = ResolveColor(kind);

            var collider = GetComponent<BoxCollider2D>();
            var size = collider != null ? collider.size : gizmoSize;
            var center = collider != null ? collider.offset : Vector2.zero;
            Gizmos.DrawWireCube(center, new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y), 0.1f));
            Gizmos.DrawLine(Vector3.zero, Vector3.up * Mathf.Max(0.75f, size.y * 0.5f));
            Gizmos.DrawSphere(Vector3.up * Mathf.Max(0.75f, size.y * 0.5f), 0.1f);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private static Color ResolveColor(TutorialFunctionMarkerKind markerKind)
        {
            return markerKind switch
            {
                TutorialFunctionMarkerKind.Wind => new Color(0.1f, 0.75f, 1f, 0.9f),
                TutorialFunctionMarkerKind.EnemySpawn => new Color(1f, 0.25f, 0.2f, 0.9f),
                TutorialFunctionMarkerKind.Objective => new Color(1f, 0.9f, 0.1f, 0.9f),
                TutorialFunctionMarkerKind.Checkpoint => new Color(0.2f, 1f, 0.45f, 0.9f),
                TutorialFunctionMarkerKind.Transition => new Color(0.75f, 0.25f, 1f, 0.9f),
                TutorialFunctionMarkerKind.TrainingStart => new Color(0.25f, 1f, 0.9f, 0.9f),
                TutorialFunctionMarkerKind.TrainingFinish => new Color(1f, 0.55f, 0.1f, 0.9f),
                TutorialFunctionMarkerKind.Interaction => Color.white,
                TutorialFunctionMarkerKind.FallRecovery => new Color(1f, 0.15f, 0.75f, 0.9f),
                TutorialFunctionMarkerKind.TilemapClearance => new Color(1f, 0.35f, 0.8f, 0.9f),
                _ => new Color(0.7f, 0.7f, 0.7f, 0.9f)
            };
        }
    }
}
