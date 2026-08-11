using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Narthex.SceneFlow
{
    public sealed class TitleMenuHoldAnimator : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        [SerializeField] private Graphic accentGraphic;
        [SerializeField] private float heldScale = 0.96f;
        [SerializeField] private float hoverScale = 1.025f;
        [SerializeField] private float response = 14f;

        private bool held;
        private bool hovered;
        private Vector3 baseScale;

        public void Configure(Graphic accent)
        {
            accentGraphic = accent;
            baseScale = transform.localScale;
        }

        private void Awake() => baseScale = transform.localScale;

        private void Update()
        {
            var multiplier = held ? heldScale : hovered ? hoverScale : 1f;
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                baseScale * multiplier,
                1f - Mathf.Exp(-response * Time.unscaledDeltaTime));
            if (accentGraphic == null) return;
            var target = held ? 1f : hovered ? 0.82f : 0.55f;
            var color = accentGraphic.color;
            color.a = Mathf.Lerp(color.a, target, 1f - Mathf.Exp(-response * Time.unscaledDeltaTime));
            accentGraphic.color = color;
        }

        public void OnPointerDown(PointerEventData eventData) => held = true;
        public void OnPointerUp(PointerEventData eventData) => held = false;
        public void OnPointerExit(PointerEventData eventData) { held = false; hovered = false; }
        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    }
}
