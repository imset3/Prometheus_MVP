using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    /// <summary>
    /// Connects a pre-placed inventory UI button to its presenter.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class InventoryPanelButtonHost : MonoBehaviour
    {
        [SerializeField] private InventoryPanelPresenter inventoryPanelPresenter;
        [SerializeField] private bool closePanel;

        private Button button;

        public bool HasValidSetup => inventoryPanelPresenter != null;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (!HasValidSetup)
            {
                Debug.LogError("InventoryPanelButtonHost requires a pre-placed InventoryPanelPresenter reference.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick()
        {
            if (closePanel) inventoryPanelPresenter.Close();
            else inventoryPanelPresenter.Toggle();
        }
    }
}
