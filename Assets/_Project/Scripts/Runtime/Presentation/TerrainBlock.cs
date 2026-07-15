using UnityEngine;

namespace Narthex.Presentation
{
    [ExecuteAlways]
    public sealed class TerrainBlock : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D collisionShape;
        [SerializeField] private Transform visualShape;
        [SerializeField, Min(0.25f)] private Vector2 size = new Vector2(4f, 1f);
        [SerializeField, Min(0.01f)] private float visualDepth = 0.4f;

        public Vector2 Size => size;
        public bool HasValidSetup => collisionShape != null && visualShape != null;

        public void SetSize(Vector2 nextSize)
        {
            size = new Vector2(Mathf.Max(0.25f, nextSize.x), Mathf.Max(0.25f, nextSize.y));
            ApplyLayout();
        }

        public void ApplyLayout()
        {
            if (!HasValidSetup) return;
            collisionShape.size = size;
            collisionShape.offset = Vector2.zero;
            visualShape.localPosition = Vector3.zero;
            visualShape.localScale = new Vector3(size.x, size.y, visualDepth);
        }

        private void OnValidate() => ApplyLayout();
    }
}
