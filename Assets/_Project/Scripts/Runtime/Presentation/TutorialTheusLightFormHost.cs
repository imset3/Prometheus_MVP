using UnityEngine;

namespace Narthex.Presentation
{
    /// <summary>
    /// Turns Theus himself into a replaceable light-form presentation and aims the
    /// authored beam at the passkey. Final art only needs to replace the ART_SLOT children.
    /// </summary>
    public sealed class TutorialTheusLightFormHost : MonoBehaviour
    {
        [SerializeField] private GameObject normalVisualRoot;
        [SerializeField] private GameObject lightFormRoot;
        [SerializeField] private Transform lightCoreVisual;
        [SerializeField] private Transform lightBeamVisual;
        [SerializeField] private Transform passkeyTarget;
        [SerializeField, Min(0.01f)] private float beamThickness = 0.32f;
        [SerializeField, Min(0f)] private float corePulseAmount = 0.12f;
        [SerializeField, Min(0f)] private float corePulseSpeed = 5f;

        private Vector3 coreBaseScale;
        private bool lightFormActive;

        public bool HasValidSetup => normalVisualRoot != null && lightFormRoot != null &&
                                     lightCoreVisual != null && lightBeamVisual != null && passkeyTarget != null;
        public bool IsLightFormActive => lightFormActive;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("TutorialTheusLightFormHost requires normal, light-core, beam, and passkey references.", this);
                enabled = false;
                return;
            }

            coreBaseScale = lightCoreVisual.localScale;
            ExitLightForm();
        }

        private void LateUpdate()
        {
            if (!lightFormActive) return;
            AimAtPasskey();
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * corePulseSpeed) * corePulseAmount;
            lightCoreVisual.localScale = coreBaseScale * pulse;
        }

        public void EnterLightForm()
        {
            if (!HasValidSetup) return;
            lightFormActive = true;
            // Theus remains visible while projecting light; the light form augments
            // the companion sprite rather than replacing it with an anonymous orb.
            normalVisualRoot.SetActive(true);
            lightFormRoot.SetActive(true);
            lightCoreVisual.gameObject.SetActive(true);
            lightBeamVisual.gameObject.SetActive(true);
            AimAtPasskey();
        }

        public void ExitLightForm()
        {
            lightFormActive = false;
            if (normalVisualRoot != null) normalVisualRoot.SetActive(true);
            if (lightFormRoot != null) lightFormRoot.SetActive(false);
            if (lightCoreVisual != null && coreBaseScale != Vector3.zero)
                lightCoreVisual.localScale = coreBaseScale;
        }

        private void AimAtPasskey()
        {
            var origin = lightCoreVisual.position;
            var delta = passkeyTarget.position - origin;
            var distance = Mathf.Max(0.05f, delta.magnitude);
            lightBeamVisual.position = origin + delta * 0.5f;
            lightBeamVisual.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            lightBeamVisual.localScale = new Vector3(distance, beamThickness, 0.12f);
        }
    }
}
