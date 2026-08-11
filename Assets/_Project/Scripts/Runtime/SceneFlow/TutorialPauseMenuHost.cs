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
    /// <summary>Runtime-installed pause menu for TutorialScene. It never changes the authored level hierarchy.</summary>
    public sealed class TutorialPauseMenuHost : MonoBehaviour
    {
        private CanvasGroup root;
        private CanvasGroup settingsPanel;
        private Slider masterSlider;
        private Slider musicSlider;
        private Slider sfxSlider;
        private SaveSystemHost saveSystemHost;
        private PlayerInputHost playerInputHost;
        private Sprite buttonFrameSprite;
        private Sprite modalPanelSprite;
        private bool wasPlayerInputEnabled;
        private bool paused;
        private sealed class ButtonAction
        {
            public Button Button;
            public UnityEngine.Events.UnityAction Action;
        }
        private readonly List<ButtonAction> buttonActions = new();
        private int selectedButtonIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterInstaller()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TutorialScene" || FindFirstObjectByType<TutorialPauseMenuHost>() != null)
                return;
            new GameObject("TutorialPauseMenuHost").AddComponent<TutorialPauseMenuHost>();
        }

        private void Awake()
        {
            saveSystemHost = FindFirstObjectByType<SaveSystemHost>();
            playerInputHost = FindFirstObjectByType<PlayerInputHost>();
            buttonFrameSprite = Resources.Load<Sprite>("UI/Title/TITLE_UI_ButtonPlate_v1");
            modalPanelSprite = Resources.Load<Sprite>("UI/Title/TITLE_UI_ModalPanel_v1");
            BuildUi();
            SetVisible(root, false);
            SetVisible(settingsPanel, false);
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
            if (paused) Time.timeScale = 1f;
        }

        private void Pause()
        {
            paused = true;
            Time.timeScale = 0f;
            if (playerInputHost != null)
            {
                wasPlayerInputEnabled = playerInputHost.enabled;
                playerInputHost.enabled = false;
            }
            SetVisible(root, true);
            SelectFirstVisibleButton();
        }

        private void Resume()
        {
            paused = false;
            Time.timeScale = 1f;
            if (playerInputHost != null) playerInputHost.enabled = wasPlayerInputEnabled;
            SetVisible(settingsPanel, false);
            SetVisible(root, false);
        }

        private void ShowSettings()
        {
            var settings = LoadSettings();
            masterSlider.value = settings.MasterVolume;
            musicSlider.value = settings.MusicVolume;
            sfxSlider.value = settings.SfxVolume;
            SetVisible(root, false);
            SetVisible(settingsPanel, true);
            SelectFirstVisibleButton();
        }

        private void ApplySettings()
        {
            var settings = LoadSettings();
            settings.MasterVolume = masterSlider.value;
            settings.MusicVolume = musicSlider.value;
            settings.SfxVolume = sfxSlider.value;
            if (saveSystemHost != null && saveSystemHost.Initialize())
            {
                saveSystemHost.System.Current.Settings = settings;
                saveSystemHost.System.Save("PauseSettingsChanged");
            }
            else GameLaunchSession.SaveSettings(settings);
            AudioListener.volume = settings.MasterVolume;
            HideSettings();
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
                saveSystemHost.System.Save("PauseMenuSaveAndExit");
            paused = false;
            Time.timeScale = 1f;
            StartCoroutine(ReturnToTitle());
        }

        private static IEnumerator ReturnToTitle()
        {
            yield return null;
            if (Application.CanStreamedLevelBeLoaded("TitleScene"))
                SceneManager.LoadScene("TitleScene", LoadSceneMode.Single);
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            root = CreateGroup("PauseRoot", canvas.transform);
            var blocker = CreateImage("Blocker", root.transform, new Color(0.01f, 0.02f, 0.035f, 0.82f));
            Stretch(blocker.rectTransform);
            var panel = CreateImage("PausePanel", root.transform,
                modalPanelSprite != null ? Color.white : new Color(0.035f, 0.065f, 0.095f, 0.98f),
                modalPanelSprite);
            SetRect(panel.rectTransform, Vector2.zero, new Vector2(560f, 560f));
            AddText(panel.transform, "일시 정지", 42, new Vector2(0f, 205f), new Vector2(460f, 70f));
            AddButton(panel.transform, "계속하기", new Vector2(0f, 90f), Resume);
            AddButton(panel.transform, "설정", new Vector2(0f, 5f), ShowSettings);
            AddButton(panel.transform, "저장 및 나가기", new Vector2(0f, -80f), SaveAndExit);
            AddText(panel.transform, "ESC · 게임으로 돌아가기", 20, new Vector2(0f, -205f), new Vector2(460f, 45f));

            settingsPanel = CreateGroup("PauseSettings", canvas.transform);
            var settingsBlocker = CreateImage("Blocker", settingsPanel.transform, new Color(0.01f, 0.02f, 0.035f, 0.94f));
            Stretch(settingsBlocker.rectTransform);
            var settings = CreateImage("SettingsPanel", settingsPanel.transform,
                modalPanelSprite != null ? Color.white : new Color(0.035f, 0.065f, 0.095f, 1f),
                modalPanelSprite);
            SetRect(settings.rectTransform, Vector2.zero, new Vector2(700f, 600f));
            AddText(settings.transform, "음량 설정", 40, new Vector2(0f, 235f), new Vector2(500f, 65f));
            masterSlider = AddSlider(settings.transform, "전체 음량", 105f);
            musicSlider = AddSlider(settings.transform, "음악", 15f);
            sfxSlider = AddSlider(settings.transform, "효과음", -75f);
            AddButton(settings.transform, "적용", new Vector2(-115f, -205f), ApplySettings, new Vector2(200f, 58f));
            AddButton(settings.transform, "취소", new Vector2(115f, -205f), HideSettings, new Vector2(200f, 58f));
        }

        private static Slider AddSlider(Transform parent, string label, float y)
        {
            AddText(parent, label, 24, new Vector2(-205f, y), new Vector2(220f, 50f), TextAnchor.MiddleLeft);
            var root = new GameObject(label + "Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(105f, y), new Vector2(350f, 40f));
            var track = CreateImage("Track", root.transform, new Color(0.12f, 0.18f, 0.23f, 1f));
            SetRect(track.rectTransform, Vector2.zero, new Vector2(350f, 8f));
            var fill = CreateImage("Fill", root.transform, new Color(0.25f, 0.86f, 0.9f, 1f));
            SetRect(fill.rectTransform, Vector2.zero, new Vector2(350f, 8f));
            var handle = CreateImage("Handle", root.transform, Color.white);
            SetRect(handle.rectTransform, Vector2.zero, new Vector2(22f, 22f));
            var slider = root.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        private void AddButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action,
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
                if (SetSlider(masterSlider, point) || SetSlider(musicSlider, point) || SetSlider(sfxSlider, point)) return;
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            SetRect(text.rectTransform, position, dimensions);
            return text;
        }

        private static void SetVisible(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

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
    }
}
