using System;
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
        [SerializeField, Min(0.1f)] private float guideAdvanceDistance = 2.5f;
        [SerializeField, Min(0.01f)] private float waypointArrivalDistance = 0.15f;

        public bool HasValidSetup => player != null && visualRoot != null;
        public bool IsGuiding => guideMode;

        private Vector3 visualBaseLocalPosition;
        private Transform[] guidePoints = Array.Empty<Transform>();
        private int guidePointIndex;
        private bool guideMode;

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
            var target = GetTargetPosition();
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * (Mathf.Sin(Time.time * hoverFrequency) * hoverDistance);

            if (!guideMode || guidePoints.Length == 0 || guidePointIndex >= guidePoints.Length - 1) return;
            var waypoint = guidePoints[guidePointIndex];
            if (waypoint == null) return;
            if (Vector3.Distance(transform.position, waypoint.position) > waypointArrivalDistance) return;
            if (Vector3.Distance(player.position, waypoint.position) > guideAdvanceDistance) return;
            guidePointIndex++;
        }

        public void BeginGuide(Transform[] waypoints)
        {
            if (waypoints == null || waypoints.Length == 0) return;
            guidePoints = waypoints;
            guidePointIndex = 0;
            guideMode = true;
        }

        public void CancelGuide()
        {
            guideMode = false;
            guidePoints = Array.Empty<Transform>();
            guidePointIndex = 0;
        }

        private Vector3 GetTargetPosition()
        {
            if (!guideMode || guidePoints == null || guidePoints.Length == 0)
                return player.position + followOffset;

            guidePointIndex = Mathf.Clamp(guidePointIndex, 0, guidePoints.Length - 1);
            var waypoint = guidePoints[guidePointIndex];
            return waypoint != null ? waypoint.position : player.position + followOffset;
        }
    }
}
