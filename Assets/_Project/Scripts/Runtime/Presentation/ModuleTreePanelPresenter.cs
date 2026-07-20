using Narthex.Gameplay;
using Narthex.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Presentation
{
    public sealed class ModuleTreePanelPresenter : MonoBehaviour
    {
        [SerializeField] private ServiceRoot serviceRoot;
        [SerializeField] private PlayerInputHost inputHost;
        [SerializeField] private ModuleTreeManagerHost moduleTreeManagerHost;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text pointsText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text inventoryModuleText;
        [SerializeField] private Text equippedSlotText;
        [SerializeField] private Button unlockButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject resultOverlay;
        [SerializeField] private string treeId = "TREE-BASIC-001";
        [SerializeField] private string selectedModuleId = "MOD-TUTO-001";
        [SerializeField] private string selectedModuleDisplayName = "튜토리얼 펄스";
        [SerializeField, Min(0)] private int equipSlotIndex;

        private bool restoreResultOverlay;

        public bool HasValidSetup => serviceRoot != null && inputHost != null && moduleTreeManagerHost != null &&
                                     panelRoot != null && titleText != null && pointsText != null && statusText != null &&
                                     inventoryModuleText != null && equippedSlotText != null && unlockButton != null &&
                                     equipButton != null && closeButton != null;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError("ModuleTreePanelPresenter requires pre-placed service, input, manager, inventory, slot, text, and button references.", this);
                enabled = false;
                return;
            }

            serviceRoot.Initialize();
            if (!moduleTreeManagerHost.Initialize())
            {
                enabled = false;
                return;
            }

            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (serviceRoot != null)
            {
                serviceRoot.Initialize();
                serviceRoot.Events.Subscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            }
            if (inputHost != null) inputHost.ModuleTreeRequested += Toggle;
            if (unlockButton != null) unlockButton.onClick.AddListener(UnlockSelectedModule);
            if (equipButton != null) equipButton.onClick.AddListener(EquipSelectedModule);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            serviceRoot?.Events?.Unsubscribe<TutorialObjectiveChanged>(HandleObjectiveChanged);
            if (inputHost != null) inputHost.ModuleTreeRequested -= Toggle;
            if (unlockButton != null) unlockButton.onClick.RemoveListener(UnlockSelectedModule);
            if (equipButton != null) equipButton.onClick.RemoveListener(EquipSelectedModule);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        private void Update()
        {
            if (panelRoot != null && panelRoot.activeSelf) Refresh();
        }

        private void Toggle()
        {
            if (panelRoot.activeSelf) Close();
            else Open();
        }

        private void HandleObjectiveChanged(TutorialObjectiveChanged message)
        {
            if (panelRoot != null && panelRoot.activeSelf) Close();
        }

        private void Open()
        {
            if (!moduleTreeManagerHost.System.HasTreeAccess(treeId))
            {
                statusText.text = "트리가 잠겨 있습니다.";
                return;
            }

            panelRoot.SetActive(true);
            restoreResultOverlay = resultOverlay != null && resultOverlay.activeSelf;
            if (restoreResultOverlay) resultOverlay.SetActive(false);
            panelRoot.transform.SetAsLastSibling();
            moduleTreeManagerHost.System.NotifyTreeOpened(treeId);
            Refresh();
        }

        private void Close()
        {
            panelRoot.SetActive(false);
            if (restoreResultOverlay && resultOverlay != null) resultOverlay.SetActive(true);
            restoreResultOverlay = false;
        }

        private void UnlockSelectedModule()
        {
            if (moduleTreeManagerHost.System.TryUnlockModule(selectedModuleId)) statusText.text = "모듈을 해금했습니다.";
            else statusText.text = "현재 조건에서는 해금할 수 없습니다.";
            Refresh();
        }

        private void EquipSelectedModule()
        {
            if (!moduleTreeManagerHost.System.TryGetModuleState(selectedModuleId, out var state) || !state.Unlocked)
            {
                statusText.text = "보유한 모듈만 장착할 수 있습니다.";
            }
            else if (moduleTreeManagerHost.System.TryEquipModule(selectedModuleId, equipSlotIndex))
            {
                statusText.text = "모듈을 1번 칸에 장착했습니다.";
            }
            else
            {
                statusText.text = "모듈 장착에 실패했습니다.";
            }

            Refresh();
        }

        private void Refresh()
        {
            if (!moduleTreeManagerHost.System.TryGetTree(treeId, out var tree)) return;
            titleText.text = string.IsNullOrWhiteSpace(tree.DisplayNameTextKey) ? tree.StableId : tree.DisplayNameTextKey;
            pointsText.text = "모듈 포인트: " + GetModulePoints();

            if (!moduleTreeManagerHost.System.TryGetModuleState(selectedModuleId, out var state))
            {
                inventoryModuleText.text = "등록된 모듈이 없습니다.";
                equippedSlotText.text = "1번 칸: 비어 있음";
                equipButton.interactable = false;
                return;
            }

            var cooldownRemaining = moduleTreeManagerHost.System.GetModuleCooldownRemaining(selectedModuleId);
            inventoryModuleText.text = state.Unlocked
                ? selectedModuleDisplayName + "\n" + (cooldownRemaining > 0f ? "재사용 대기: " + cooldownRemaining.ToString("0.0") + "초" : "준비 완료")
                : selectedModuleDisplayName + "\n미보유";
            equippedSlotText.text = state.Equipped && state.SlotIndex == equipSlotIndex
                ? "1번 칸: " + selectedModuleDisplayName
                : "1번 칸: 비어 있음";
            unlockButton.interactable = !state.Unlocked;
            equipButton.interactable = state.Unlocked && (!state.Equipped || state.SlotIndex != equipSlotIndex);
        }

        private int GetModulePoints()
        {
            return moduleTreeManagerHost.System == null ? 0 : moduleTreeManagerHost.System.ModulePoints;
        }
    }
}
