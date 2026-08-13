using UnityEngine;
using UnityEngine.UI;

namespace Narthex.SceneFlow
{
    /// <summary>
    /// Keeps the authored slider artwork at a fixed size while revealing its energy fill.
    /// Unity's default Slider resizes fillRect; this presenter uses Image.fillAmount instead.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    public sealed class ThemedVolumeSliderPresenter : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Image energyFill;

        public Image EnergyFill => energyFill;

        private void Awake()
        {
            if (slider == null) slider = GetComponent<Slider>();
            if (slider == null || energyFill == null)
            {
                Debug.LogError("ThemedVolumeSliderPresenter requires authored Slider and EnergyFill references.", this);
                enabled = false;
                return;
            }

            slider.onValueChanged.AddListener(Refresh);
            Refresh(slider.value);
        }

        private void OnEnable()
        {
            if (slider != null && energyFill != null) Refresh(slider.value);
        }

        private void OnDestroy()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(Refresh);
        }

        public void Configure(Slider targetSlider, Image targetFill)
        {
            slider = targetSlider;
            energyFill = targetFill;
            if (energyFill != null)
            {
                energyFill.type = Image.Type.Filled;
                energyFill.fillMethod = Image.FillMethod.Horizontal;
                energyFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                energyFill.fillClockwise = true;
                energyFill.preserveAspect = false;
            }
            if (slider != null) Refresh(slider.value);
        }

        private void Refresh(float value)
        {
            if (energyFill == null) return;
            var range = slider != null ? slider.maxValue - slider.minValue : 1f;
            energyFill.fillAmount = range > Mathf.Epsilon
                ? Mathf.Clamp01((value - slider.minValue) / range)
                : 0f;
        }
    }
}
