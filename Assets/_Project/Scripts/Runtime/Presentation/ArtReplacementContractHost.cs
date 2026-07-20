using System;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Scene contract for replacing placeholder geometry with final character art. It keeps collision,
    /// foot alignment, attack anchors, and sorting validation independent from the visual asset itself.
    /// </summary>
    public sealed class ArtReplacementContractHost : MonoBehaviour
    {
        [SerializeField] private Transform actorRoot;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform footAnchor;
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private Collider2D[] attackHitboxes = Array.Empty<Collider2D>();
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();
        [SerializeField] private string expectedSortingLayer = "Default";
        [SerializeField, Min(0f)] private float footAlignmentTolerance = 0.25f;

        public bool HasValidSetup => actorRoot != null && visualRoot != null && footAnchor != null &&
                                     bodyCollider != null && attackHitboxes != null && attackHitboxes.Length > 0 &&
                                     renderers != null && renderers.Length > 0 && HasNoMissingReferences();
        public bool IsFootAligned => bodyCollider != null && footAnchor != null &&
                                     Mathf.Abs(bodyCollider.bounds.min.y - footAnchor.position.y) <= footAlignmentTolerance;
        public bool HasConsistentSorting
        {
            get
            {
                if (renderers == null || renderers.Length == 0 || string.IsNullOrWhiteSpace(expectedSortingLayer)) return false;
                foreach (var renderer in renderers)
                    if (renderer == null || renderer.sortingLayerName != expectedSortingLayer) return false;
                return true;
            }
        }
        public int AttackHitboxCount => attackHitboxes?.Length ?? 0;
        public int RendererCount => renderers?.Length ?? 0;
        public string ExpectedSortingLayer => expectedSortingLayer;

        private bool HasNoMissingReferences()
        {
            foreach (var hitbox in attackHitboxes)
                if (hitbox == null || !hitbox.isTrigger) return false;
            foreach (var renderer in renderers)
                if (renderer == null) return false;
            return true;
        }
    }
}
