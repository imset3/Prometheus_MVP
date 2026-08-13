using System.Collections;
using System.Collections.Generic;
using Narthex.Gameplay;
using Narthex.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.SceneFlow
{
    /// <summary>Controls the pause UI authored and serialized in the scene hierarchy.</summary>
    public sealed class TutorialPauseMenuHost : MonoBehaviour
    {
#if UNITY_EDITOR
        private const string UiFontPath = "Assets/_Project/Art/Fonts/GoogleFonts/DoHyeon-Regular.ttf";
#endif
        [SerializeField] private CanvasGroup root;
        [SerializeField] private CanvasGroup settingsPanel;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private SaveSystemHost saveSystemHost;
        [SerializeField] private PlayerInputHost playerInputHost;
        [SerializeField] private TutorialQuestSequenceHost questSequenceHost;
        [SerializeField] private RectTransform pausePanelRect;
        [SerializeField] private RectTransform settingsPanelRect;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button saveAndExitButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelButton;
        private Sprite buttonFrameSprite;
        private Sprite modalPanelSprite;
        private bool paused;
        private sealed class ButtonAction
        {
            public Button Button;
            public UnityEngine.Events.UnityAction Action;
        }
        private readonly List<ButtonAction> buttonActions = new();
        private int selectedButtonIndex;

        private void Awake()
        {
            if (!HasAuthoredSetup())
            {
                Debug.LogError("TutorialPauseMenuHost requires the authored PauseCanvas and Inspector references. Run ui.readability.apply.", this);
                enabled = false;
                return;
            }
            BindButton(resumeButton, Resume);
            BindButton(settingsButton, ShowSettings);
            BindButton(saveAndExitButton, SaveAndExit);
            BindButton(applyButton, ApplySettings);
            BindButton(cancelButton, HideSettings);
            masterSlider.onValueChanged.RemoveListener(ApplyMasterVolumeImmediately);
            masterSlider.onValueChanged.AddListener(ApplyMasterVolumeImmediately);
            RefreshPanelLayout();
            SetVisible(root, false);
            SetVisible(settingsPanel, false);
        }

        public bool HasAuthoredSetup() => root != null && settingsPanel != null && masterSlider != null &&
                                           saveSystemHost != null && playerInputHost != null && questSequenceHost != null &&
                                           pausePanelRect != null && settingsPanelRect != null && resumeButton != null &&
                                           settingsButton != null && saveAndExitButton != null && applyButton != null &&
                                           cancelButton != null;

        private void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            buttonActions.Add(new ButtonAction { Button = button, Action = action });
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (settingsPanel.alpha > 0f) HideSettings();
                else if (paused) Resume();
                else Pause();
                return;
            }
            if (paused) HandleMenuInput();
        }

        private void OnDestroy()
        {
            if (masterSlider != null)
                masterSlider.onValueChanged.RemoveListener(ApplyMasterVolumeImmediately);
            if (paused) Time.timeScale = 1f;
        }

        private void Pause()
        {
            paused = true;
            Time.timeScale = 0f;
            playerInputHost?.AcquireInputLock(PlayerInputLockReason.Pause);
            SetVisible(root, true);
            SelectFirstVisibleButton();
        }

        private void Resume()
        {
            paused = false;
            Time.timeScale = 1f;
            playerInputHost?.ReleaseInputLock(PlayerInputLockReason.Pause);
            SetVisible(settingsPanel, false);
            SetVisible(root, false);
        }

        private void ShowSettings()
        {
            var settings = LoadSettings();
            masterSlider.SetValueWithoutNotify(Mathf.Clamp01(settings.MasterVolume));
            AudioListener.volume = Mathf.Clamp01(settings.MasterVolume);
            SetVisible(root, false);
            SetVisible(settingsPanel, true);
            RefreshPanelLayout();
            SelectFirstVisibleButton();
        }

        private void ApplySettings()
        {
            ApplyMasterVolumeImmediately(masterSlider.value);
            HideSettings();
        }

        private void ApplyMasterVolumeImmediately(float value)
        {
            value = Mathf.Clamp01(value);
            var settings = LoadSettings();
            settings.MasterVolume = value;
            settings.MusicVolume = 1f;
            settings.SfxVolume = 1f;
            if (saveSystemHost != null && saveSystemHost.Initialize())
            {
                saveSystemHost.System.Current.Settings = settings;
                saveSystemHost.System.Save("PauseMasterVolumeChanged");
            }
            else GameLaunchSession.SaveSettings(settings);
            AudioListener.volume = value;
        }

        private void HideSettings()
        {
            SetVisible(settingsPanel, false);
            if (paused) SetVisible(root, true);
            SelectFirstVisibleButton();
        }

        private SettingsSaveData LoadSettings()
        {
            if (saveSystemHost != null && saveSystemHost.Initialize())
                return saveSystemHost.System.Current.Settings ??= new SettingsSaveData();
            return GameLaunchSession.LoadSave().Settings ?? new SettingsSaveData();
        }

        private void SaveAndExit()
        {
            if (saveSystemHost != null && saveSystemHost.Initialize())
            {
                var position = playerInputHost != null ? (Vector2)playerInputHost.transform.position : Vector2.zero;
                GameLaunchSession.SaveTutorialContinuePoint(
                    saveSystemHost.System.Current,
                    questSequenceHost != null ? questSequenceHost.CurrentQuestId : string.Empty,
                    position);
                saveSystemHost.System.Save("PauseMenuSaveAndExit");
            }
            paused = false;
            Time.timeScale = 1f;
            playerInputHost?.ReleaseInputLock(PlayerInputLockReason.Pause);
            StartCoroutine(ReturnToTitle());
        }

        private static IEnumerator ReturnToTitle()
        {
            yield return null;
            if (Application.CanStreamedLevelBeLoaded("TitleScene"))
                SceneManager.LoadScene("TitleScene", LoadSceneMode.Single);
        }

#if UNITY_EDITOR
        public void RebuildAuthoredPresentation()
        {
            if (Application.isPlaying) return;
            buttonFrameSprite = Resources.Load<Sprite>("UI/Title/TITLE_UI_ButtonPlate_v2");
            modalPanelSprite = Resources.Load<Sprite>("UI/Title/TITLE_UI_ModalPanel_v1");
            var existing = gameObject.scene.GetRootGameObjects();
            foreach (var sceneRoot in existing)
                if (sceneRoot.name == "PauseCanvas") DestroyImmediate(sceneRoot);
            var canvasObject = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root = CreateGroup("PauseRoot", canvas.transform);
            var blocker = CreateImage("Blocker", root.transform, new Color(0.01f, 0.02f, 0.035f, 0.82f));
            Stretch(blocker.rectTransform);
            var panel = CreateImage("PausePanel", root.transform,
                modalPanelSprite != null ? Color.white : new Color(0.035f, 0.065f, 0.095f, 0.98f),
                modalPanelSprite);
            SetRect(panel.rectTransform, Vector2.zero, new Vector2(560f, 560f));
            pausePanelRect = panel.rectTransform;
            var pauseSafeArea = CreateRect("ContentSafeArea", panel.transform, Vector2.zero, new Vector2(440f, 430f));
            AddText(pauseSafeArea, "일시 정지", 42, new Vector2(0f, 165f), new Vector2(420f, 65f));
            resumeButton = AddButton(pauseSafeArea, "계속하기", new Vector2(0f, 70f), Resume);
            settingsButton = AddButton(pauseSafeArea, "설정", new Vector2(0f, -10f), ShowSettings);
            saveAndExitButton = AddButton(pauseSafeArea, "저장 및 나가기", new Vector2(0f, -90f), SaveAndExit);
            AddText(pauseSafeArea, "ESC · 게임으로 돌아가기", 20, new Vector2(0f, -178f), new Vector2(420f, 42f));

            settingsPanel = CreateGroup("PauseSettings", canvas.transform);
            var settingsBlocker = CreateImage("Blocker", settingsPanel.transform, new Color(0.01f, 0.02f, 0.035f, 0.94f));
            Stretch(settingsBlocker.rectTransform);
            var settings = CreateImage("SettingsPanel", settingsPanel.transform,
                modalPanelSprite != null ? Color.white : new Color(0.035f, 0.065f, 0.095f, 1f),
                modalPanelSprite);
            SetRect(settings.rectTransform, Vector2.zero, new Vector2(700f, 600f));
            settingsPanelRect = settings.rectTransform;
            var settingsSafeArea = CreateRect("ContentSafeArea", settings.transform, Vector2.zero, new Vector2(540f, 450f));
            AddText(settingsSafeArea, "음량 설정", 40, new Vector2(0f, 175f), new Vector2(500f, 65f));
            masterSlider = AddSlider(settingsSafeArea, "전체 음량", 45f);
            applyButton = AddButton(settingsSafeArea, "적용", new Vector2(-115f, -165f), ApplySettings, new Vector2(200f, 58f));
            cancelButton = AddButton(settingsSafeArea, "취소", new Vector2(115f, -165f), HideSettings, new Vector2(200f, 58f));
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void OnRectTransformDimensionsChange() => RefreshPanelLayout();

        private void RefreshPanelLayout()
        {
            FitPanelToCanvas(pausePanelRect, 32f);
            FitPanelToCanvas(settingsPanelRect, 32f);
        }

        private static void FitPanelToCanvas(RectTransform panel, float margin)
        {
            if (panel == null || panel.GetComponentInParent<Canvas>()?.transform is not RectTransform canvasRect) return;
            var scale = TitleScreenHost.ResolvePanelScale(canvasRect.rect.size, panel.sizeDelta, margin);
            panel.localScale = new Vector3(scale, scale, 1f);
            panel.anchoredPosition = Vector2.zero;
        }

#if UNITY_EDITOR
        private static Slider AddSlider(Transform parent, string label, float y)
        {
            AddText(parent, label, 25, new Vector2(-155f, y), new Vector2(190f, 50f), TextAnchor.MiddleLeft);
            var root = new GameObject(label + "Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(105f, y), new Vector2(330f, 44f));
            var trackSprite = Resources.Load<Sprite>("UI/Title/TITLE_UI_VolumeTrack_v1");
            var fillSprite = Resources.Load<Sprite>("UI/Title/TITLE_UI_VolumeFill_v1");
            var track = CreateImage("Track", root.transform, Color.white, trackSprite);
            track.preserveAspect = true;
            SetRect(track.rectTransform, Vector2.zero, new Vector2(330f, 40f));
            var fill = CreateImage("EnergyFill", root.transform, Color.white, fillSprite);
            SetRect(fill.rectTransform, Vector2.zero, new Vector2(290f, 14f));
            fill.raycastTarget = false;
            var handleSprite = Resources.Load<Sprite>("UI/Title/TITLE_UI_VolumeHandle_v1");
            var handle = CreateImage("Handle", root.transform, Color.white, handleSprite);
            handle.preserveAspect = true;
            SetRect(handle.rectTransform, Vector2.zero, new Vector2(38f, 38f));
            var slider = root.GetComponent<Slider>();
            slider.fillRect = null;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            root.AddComponent<ThemedVolumeSliderPresenter>().Configure(slider, fill);
            return slider;
        }

        private Button AddButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action,
            Vector2? size = null)
        {
            var image = CreateImage(label, parent,
                buttonFrameSprite != null ? Color.white : new Color(0.06f, 0.105f, 0.145f, 1f),
                buttonFrameSprite);
            SetRect(image.rectTransform, position, size ?? new Vector2(390f, 66f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            buttonActions.Add(new ButtonAction { Button = button, Action = action });
            var labelSprite = ResolveButtonLabelSprite(label);
            if (labelSprite != null)
            {
                var labelImage = CreateImage("Label", image.transform, Color.white, labelSprite);
                labelImage.preserveAspect = true;
                labelImage.raycastTarget = false;
                var labelRect = labelImage.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 8f);
                labelRect.offsetMax = new Vector2(-12f, -8f);
            }
            else
            {
                AddText(image.transform, label, 26, Vector2.zero, size ?? new Vector2(390f, 66f));
            }
            image.gameObject.AddComponent<TitleMenuHoldAnimator>().Configure(image);
            return button;
        }

        private static Sprite ResolveButtonLabelSprite(string label)
        {
            var assetName = label switch
            {
                "계속하기" => "PAUSE_LABEL_Resume_v1",
                "설정" => "TITLE_LABEL_Settings_v1",
                "저장 및 나가기" => "PAUSE_LABEL_SaveAndExit_v1",
                "적용" => "PAUSE_LABEL_Apply_v1",
                "취소" => "PAUSE_LABEL_Cancel_v1",
                _ => string.Empty
            };
            return string.IsNullOrEmpty(assetName)
                ? null
                : Resources.Load<Sprite>($"UI/Title/Labels/{assetName}");
        }
#endif

        private void HandleMenuInput()
        {
            var visible = buttonActions.FindAll(binding => IsButtonVisible(binding.Button));
            var keyboard = Keyboard.current;
            if (keyboard != null && visible.Count > 0)
            {
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                    selectedButtonIndex = (selectedButtonIndex + 1) % visible.Count;
                else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                    selectedButtonIndex = (selectedButtonIndex - 1 + visible.Count) % visible.Count;
                selectedButtonIndex = Mathf.Clamp(selectedButtonIndex, 0, visible.Count - 1);
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(visible[selectedButtonIndex].Button.gameObject);
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    visible[selectedButtonIndex].Action?.Invoke();
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            var point = mouse.position.ReadValue();
            if (mouse.leftButton.isPressed)
            {
                if (SetSlider(masterSlider, point)) return;
            }
            if (!mouse.leftButton.wasReleasedThisFrame) return;
            foreach (var binding in visible)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(binding.Button.transform as RectTransform, point)) continue;
                binding.Action?.Invoke();
                return;
            }
        }

        private void SelectFirstVisibleButton()
        {
            selectedButtonIndex = 0;
            foreach (var binding in buttonActions)
            {
                if (!IsButtonVisible(binding.Button)) continue;
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(binding.Button.gameObject);
                return;
            }
        }

        private static bool IsButtonVisible(Button button)
        {
            if (button == null || !button.IsActive() || !button.interactable) return false;
            foreach (var group in button.GetComponentsInParent<CanvasGroup>(true))
                if (group.alpha <= 0.01f || !group.interactable) return false;
            return true;
        }

        private static bool SetSlider(Slider slider, Vector2 point)
        {
            if (slider == null || !slider.gameObject.activeInHierarchy ||
                !RectTransformUtility.RectangleContainsScreenPoint(slider.transform as RectTransform, point)) return false;
            var rect = slider.transform as RectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, point, null, out var local)) return false;
            slider.value = Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, local.x);
            return true;
        }

#if UNITY_EDITOR
        private static CanvasGroup CreateGroup(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            return root.GetComponent<CanvasGroup>();
        }

        private static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            return image;
        }

        private static Text AddText(Transform parent, string value, int size, Vector2 position, Vector2 dimensions,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var root = new GameObject(value, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            var text = root.GetComponent<Text>();
            text.text = value;
            text.font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UiFontPath) ??
                        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            SetRect(text.rectTransform, position, dimensions);
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            SetRect(rect, position, size);
            return rect;
        }
#endif

        private static void SetVisible(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

#if UNITY_EDITOR
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
#endif
    }
}
