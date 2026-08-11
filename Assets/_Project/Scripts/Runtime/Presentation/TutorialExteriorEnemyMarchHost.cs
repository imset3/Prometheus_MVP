using System;
using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Presentation-only formation shown below the first exterior route. Soldiers are
    /// pre-placed by the scene tool and contain no combat or collision components.
    /// </summary>
    public sealed class TutorialExteriorEnemyMarchHost : MonoBehaviour
    {
        [SerializeField] private Transform[] soldiers = Array.Empty<Transform>();
        [SerializeField, Min(0.1f)] private float marchSpeed = 1.35f;
        [SerializeField] private float wrapMinX = -18f;
        [SerializeField] private float wrapMaxX = 18f;
        [SerializeField, Min(0f)] private float bobHeight = 0.08f;
        [SerializeField, Min(0.1f)] private float bobFrequency = 7f;

        private Vector3[] startingPositions = Array.Empty<Vector3>();
        private float distance;

        public bool HasValidSetup => soldiers != null && soldiers.Length >= 6 &&
                                     Array.TrueForAll(soldiers, item => item != null) &&
                                     wrapMaxX > wrapMinX;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialExteriorEnemyMarchHost requires a pre-placed visual formation.", this);
                enabled = false;
                return;
            }

            CacheStartingPositions();
        }

        private void OnEnable()
        {
            distance = 0f;
            if (startingPositions.Length != soldiers.Length) CacheStartingPositions();
            ApplyPose();
        }

        private void Update()
        {
            distance += marchSpeed * Time.deltaTime;
            ApplyPose();
        }

        private void CacheStartingPositions()
        {
            startingPositions = new Vector3[soldiers.Length];
            for (var index = 0; index < soldiers.Length; index++)
                startingPositions[index] = soldiers[index].localPosition;
        }

        private void ApplyPose()
        {
            var width = wrapMaxX - wrapMinX;
            for (var index = 0; index < soldiers.Length; index++)
            {
                var start = startingPositions[index];
                // The formation is observed from the elevated exterior route, so the
                // invasion must read as moving toward Adamas on the left.
                var x = wrapMinX + Mathf.Repeat(start.x - wrapMinX - distance, width);
                var bob = Mathf.Abs(Mathf.Sin(Time.time * bobFrequency + index * 0.75f)) * bobHeight;
                soldiers[index].localPosition = new Vector3(x, start.y + bob, start.z);
            }
        }
    }
}
