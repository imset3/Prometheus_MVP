using Narthex.Core;
using UnityEngine;

namespace Narthex.Presentation
{
    public sealed class CameraFollowHost : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float followSpeed = 14f;
        [SerializeField] private float minX = -41f;
        [SerializeField] private float maxX = 41f;
        [SerializeField] private float fixedY;
        [SerializeField] private float fixedZ = -10f;

        public bool HasValidSetup => serviceRoot != null && target != null && minX <= maxX;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("CameraFollowHost requires a target and valid horizontal bounds.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            SnapToTarget();
        }

        private void OnEnable()
        {
            serviceRoot?.Initialize();
            serviceRoot?.Events?.Subscribe<PlayerRespawned>(HandlePlayerRespawned);
        }

        private void OnDisable() => serviceRoot?.Events?.Unsubscribe<PlayerRespawned>(HandlePlayerRespawned);

        private void Start() => SnapToTarget();

        private void LateUpdate()
        {
            if (target == null) return;
            var desiredX = Mathf.Clamp(target.position.x, minX, maxX);
            var nextX = Mathf.MoveTowards(transform.position.x, desiredX, followSpeed * Time.deltaTime);
            transform.position = new Vector3(nextX, fixedY, fixedZ);
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = new Vector3(Mathf.Clamp(target.position.x, minX, maxX), fixedY, fixedZ);
        }

        private void HandlePlayerRespawned(PlayerRespawned message) => SnapToTarget();
    }
}
