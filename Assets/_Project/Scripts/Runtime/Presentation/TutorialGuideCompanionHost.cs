using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Keeps a pre-placed tutorial guide root near the player. Swap only the Visual child
    /// when final companion art or animation is ready.
    /// </summary>
    public sealed class TutorialGuideCompanionHost : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Vector3 followOffset = new(-1.1f, 1.1f, 0f);
        [SerializeField, Min(0f)] private float followSpeed = 7f;
        [SerializeField, Min(0f)] private float hoverDistance = 0.12f;
        [SerializeField, Min(0f)] private float hoverFrequency = 2.5f;

        public bool HasValidSetup => player != null && visualRoot != null;

        private Vector3 visualBaseLocalPosition;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialGuideCompanionHost requires pre-placed player and visual references.", this);
                enabled = false;
                return;
            }

            visualBaseLocalPosition = visualRoot.localPosition;
        }

        private void LateUpdate()
        {
            var target = player.position + followOffset;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * hoverFrequency) * hoverDistance);
        }
    }
}
