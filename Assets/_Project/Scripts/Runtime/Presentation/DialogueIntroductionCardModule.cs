using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    /// <summary>
    /// Renders a pre-placed tutorial character introduction card.
    /// The module only changes existing scene UI and never creates objects at runtime.
    /// </summary>
    public sealed class DialogueIntroductionCardModule : MonoBehaviour
    {
        [SerializeField] private GameObject cardRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text continueText;

        public bool HasValidSetup => cardRoot != null && titleText != null && descriptionText != null;

        public void Show(string title, string subtitle, string description, string prompt)
        {
            SetText(titleText, title);
            SetText(subtitleText, subtitle);
            SetText(descriptionText, description);
            SetText(continueText, prompt);
            if (cardRoot != null) cardRoot.SetActive(true);
        }

        public void Hide()
        {
            if (cardRoot != null) cardRoot.SetActive(false);
        }

        private static void SetText(Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
