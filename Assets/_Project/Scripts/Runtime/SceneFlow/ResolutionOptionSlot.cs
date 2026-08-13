using UnityEngine;
using UnityEngine.UI;

namespace Narthex.SceneFlow
{
    /// <summary>A pre-authored resolution choice. Runtime only updates state and text.</summary>
    public sealed class ResolutionOptionSlot : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private Image selectionFrame;

        public Button Button => button;
        public Text Label => label;
        public bool HasValidReferences => button != null && label != null;

        public void SetLabel(string value)
        {
            if (label != null) label.text = value;
        }

        public void SetSelected(bool selected)
        {
            if (selectionFrame != null) selectionFrame.enabled = selected;
        }

        public void Configure(Button authoredButton, Text authoredLabel, Image authoredSelectionFrame)
        {
            button = authoredButton;
            label = authoredLabel;
            selectionFrame = authoredSelectionFrame;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
