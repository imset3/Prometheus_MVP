using Narthex.Content;
using Narthex.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    /// <summary>
    /// Presents the pre-placed inventory panel. Item entries remain scene-authored;
    /// this presenter only updates labels and visibility.
    /// </summary>
    public sealed class InventoryPanelPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private ModuleTreeManagerHost moduleTreeManagerHost;
        [SerializeField] private ModuleDefinition tutorialModule;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text moduleStateText;
        [SerializeField] private Text detailText;

        public bool HasValidSetup => playerInputHost != null && moduleTreeManagerHost != null && tutorialModule != null &&
                                     panelRoot != null && moduleStateText != null && detailText != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("InventoryPanelPresenter requires pre-placed input, module, panel, and Text references.", this);
                enabled = false;
                return;
            }

            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (playerInputHost != null) playerInputHost.InventoryRequested += Toggle;
        }

        private void OnDisable()
        {
            if (playerInputHost != null) playerInputHost.InventoryRequested -= Toggle;
        }

        public void Toggle()
        {
            if (panelRoot.activeSelf) Close();
            else Open();
        }

        public void Open()
        {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            panelRoot.SetActive(false);
        }

        private void Refresh()
        {
            var unlocked = moduleTreeManagerHost.System != null &&
                           moduleTreeManagerHost.System.TryGetModuleState(tutorialModule.StableId, out var state) && state.Unlocked;
            moduleStateText.text = unlocked ? "펄스 모듈  |  보유" : "펄스 모듈  |  미보유";
            detailText.text = unlocked ? "전방 에너지 파동\n피해 35  |  재사용 3초" : "훈련을 진행하면 모듈을 획득합니다.";
        }
    }
}
