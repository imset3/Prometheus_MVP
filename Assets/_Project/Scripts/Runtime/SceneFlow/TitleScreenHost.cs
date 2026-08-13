using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Narthex.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.SceneFlow
{
    /// <summary>
    /// Builds the title presentation from replaceable art layers. The background, Prome sequence,
    /// Zenith, clouds and foreground all animate independently so the title never depends on one video.
    /// </summary>
    public sealed class TitleScreenHost : MonoBehaviour
    {
        [Header("Replaceable title art")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Sprite zenithSprite;
        [SerializeField] private Sprite cloudSprite;
        [SerializeField] private Sprite[] promeIdleFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite titleLogoFrameSprite;
        [SerializeField] private Sprite buttonFrameSprite;
        [SerializeField] private Sprite loadingCompassSprite;
        [SerializeField] private Sprite modalPanelSprite;
        [SerializeField] private Sprite volumeTrackSprite;
        [SerializeField] private Sprite volumeFillSprite;
        [SerializeField] private Sprite volumeHandleSprite;
        [SerializeField] private Sprite newGameLabelSprite;
        [SerializeField] private Sprite continueLabelSprite;
        [SerializeField] private Sprite settingsLabelSprite;
        [SerializeField] private Sprite quitLabelSprite;
        [SerializeField] private Sprite applyLabelSprite;
        [SerializeField] private Sprite backLabelSprite;
        [SerializeField] private Sprite resetLabelSprite;
        [SerializeField] private Sprite resetConfirmLabelSprite;
        [SerializeField] private Font titleFont;
        [SerializeField] private Font bodyFont;
        [SerializeField] private AudioClip titleMusic;

        [Header("Scenes")]
        [SerializeField] private string tutorialSceneName = "TutorialScene";

        [Header("Motion")]
        [SerializeField, Min(1f)] private float promeFramesPerSecond = 12f;
        [SerializeField, Min(1f)] private float cloudPixelsPerSecond = 7f;
        [SerializeField, Min(0.1f)] private float zenithEaseSeconds = 8f;

        private readonly List<Vector2Int> supportedResolutions = new();
        private readonly FullScreenMode[] supportedDisplayModes =
        {
            FullScreenMode.Windowed,
            FullScreenMode.ExclusiveFullScreen,
            FullScreenMode.FullScreenWindow
        };
        private static readonly string[] DisplayModeLabels =
        {
            "창 모드",
            "전체 화면",
            "창 없는 전체 화면"
        };
        private sealed class ButtonAction
        {
            public Button Button;
        }
        private readonly List<ButtonAction> buttonActions = new();
        private int selectedButtonIndex;

        private CanvasGroup introGroup;
        private CanvasGroup menuGroup;
        private CanvasGroup settingsGroup;
        private CanvasGroup loadingGroup;
        private RectTransform settingsPanelRect;
        private Text loadingText;
        private Image loadingBar;
        private Image backgroundImage;
        private Image zenithImage;
        private Image promeImage;
        private RectTransform loadingCompassRect;
        private Image loadingCompassGlow;
        private RectTransform[] cloudRects;
        private Button continueButton;
        private Button resetButton;
        private Image resetButtonLabel;
        private Dropdown resolutionDropdown;
        private Dropdown displayModeDropdown;
        private Slider masterSlider;
        private CanvasGroup resolutionConfirmGroup;
        private Text resolutionConfirmCountdown;
        private Button keepResolutionButton;
        private Button revertResolutionButton;
        private Coroutine resolutionConfirmRoutine;
        private SettingsSaveData pendingSettings;
        private SettingsSaveData previousSettings;
        private AudioSource musicSource;
        private SaveData saveData;
        private float elapsed;
        private int promeFrameIndex;
        private bool menuShown;
        private bool loading;
        private bool usesAuthoredPresentation;

        public bool HasValidSetup => backgroundSprite != null && zenithSprite != null &&
                                     promeIdleFrames != null && promeIdleFrames.Length > 0;
        public bool HasThemeSpriteSetup => titleLogoFrameSprite != null && buttonFrameSprite != null &&
                                           loadingCompassSprite != null && modalPanelSprite != null &&
                                           volumeTrackSprite != null && volumeFillSprite != null &&
                                           volumeHandleSprite != null;
        public bool HasButtonLabelSpriteSetup => newGameLabelSprite != null && continueLabelSprite != null &&
                                                 settingsLabelSprite != null && quitLabelSprite != null && applyLabelSprite != null &&
                                                 backLabelSprite != null && resetLabelSprite != null &&
                                                 resetConfirmLabelSprite != null;
        public bool MainMenuVisible => IsGroupVisible(menuGroup);
        public bool SettingsVisible => IsGroupVisible(settingsGroup);
        public bool IsLoading => loading;
        public bool UsesAuthoredPresentation => usesAuthoredPresentation;
        public string TutorialSceneName => tutorialSceneName;
        public int RegisteredButtonCount => buttonActions.Count;
        public bool HasUniqueButtonBindings
        {
            get
            {
                var unique = new HashSet<Button>();
                foreach (var binding in buttonActions)
                    if (binding?.Button == null || !unique.Add(binding.Button)) return false;
                return true;
            }
        }

        private void Awake()
        {
            Time.timeScale = 1f;
            saveData = GameLaunchSession.LoadSave();
            RefreshSupportedResolutions();
            ResolveThemeSprites();
            if (EventSystem.current == null)
                Debug.LogError("TitleScene requires a pre-authored EventSystem.", this);
            if (!TryBindAuthoredPresentation())
            {
                Debug.LogError("TitleScene requires a fully authored TitleCanvas. Run title.scene.apply in Edit Mode.", this);
                enabled = false;
                return;
            }
            ApplySavedSettings();
            RefreshSettingsLayout();
        }

        private void Start()
        {
            if (titleMusic == null) return;
            musicSource = GetComponent<AudioSource>();
            if (musicSource == null)
            {
                Debug.LogError("TitleScreenRoot requires a pre-authored AudioSource.", this);
                return;
            }
            musicSource.clip = titleMusic;
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0.62f;
            musicSource.Play();
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            AnimateArtLayers();
            if (!menuShown && !loading && AnyInputPressed()) ShowMenu();
            else if (menuShown && !loading) HandleManualUiInput();
        }

#if UNITY_EDITOR
        private void BuildPresentation()
        {
            var canvasObject = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            backgroundImage = CreateImage("Background", canvas.transform, backgroundSprite, Color.white);
            Stretch(backgroundImage.rectTransform);
            backgroundImage.preserveAspect = false;

            cloudRects = new RectTransform[3];
            for (var index = 0; index < cloudRects.Length; index++)
            {
                var cloud = CreateImage("CloudLayer_" + (index + 1), canvas.transform, cloudSprite,
                    new Color(0.85f, 0.9f, 0.94f, 0.1f + index * 0.035f));
                cloud.rectTransform.anchorMin = new Vector2(0f, 0.3f + index * 0.11f);
                cloud.rectTransform.anchorMax = new Vector2(0f, 0.3f + index * 0.11f);
                cloud.rectTransform.sizeDelta = new Vector2(780f + index * 150f, 220f + index * 35f);
                cloud.rectTransform.anchoredPosition = new Vector2(-350f + index * 620f, 0f);
                cloudRects[index] = cloud.rectTransform;
            }

            zenithImage = CreateImage("Zenith", canvas.transform, zenithSprite, Color.white);
            zenithImage.preserveAspect = true;
            SetRect(zenithImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(1380f, 760f));

            var vignette = CreateImage("Vignette", canvas.transform, null, new Color(0.015f, 0.025f, 0.045f, 0.28f));
            Stretch(vignette.rectTransform);

            promeImage = CreateImage("Prome", canvas.transform, promeIdleFrames[0], Color.white);
            promeImage.preserveAspect = true;
            SetRect(promeImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-575f, -175f), new Vector2(280f, 480f));

            var titleFrame = CreateImage("TitleLogoFrame", canvas.transform, titleLogoFrameSprite, Color.white);
            SetRect(titleFrame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(410f, 155f), new Vector2(920f, 345f));
            var title = CreateText("Title", titleFrame.transform, "PROME&THEUS", 92, TextAnchor.MiddleCenter, titleFont, Color.white);
            SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(720f, 125f));
            title.fontStyle = FontStyle.Bold;
            var subtitle = CreateText("Subtitle", titleFrame.transform, "CHAPTER 0  ·  DEMO", 23, TextAnchor.MiddleCenter, bodyFont,
                new Color(0.67f, 0.9f, 0.94f, 0.92f));
            subtitle.fontStyle = FontStyle.Bold;
            SetRect(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(650f, 44f));

            introGroup = CreateGroup("IntroPrompt", canvas.transform);
            var prompt = CreateText("Prompt", introGroup.transform, "아무 키나 눌러 시작", 30, TextAnchor.MiddleCenter,
                bodyFont, Color.white);
            prompt.fontStyle = FontStyle.Bold;
            SetRect(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(410f, -295f), new Vector2(700f, 72f));

            menuGroup = CreateGroup("MainMenu", canvas.transform);
            SetGroup(menuGroup, false);
            var menuPanel = CreateImage("MenuPanel", menuGroup.transform, modalPanelSprite,
                modalPanelSprite != null ? Color.white : new Color(0.025f, 0.045f, 0.07f, 0.84f));
            SetRect(menuPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(430f, -220f), new Vector2(700f, 610f));
            var menuSafeArea = CreateRect("ContentSafeArea", menuPanel.transform,
                new Vector2(0f, 0f), new Vector2(560f, 440f));
            var vertical = menuSafeArea.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(0, 0, 0, 0);
            vertical.spacing = 16f;
            vertical.childControlHeight = true;
            vertical.childForceExpandHeight = false;
            vertical.childControlWidth = true;
            vertical.childForceExpandWidth = true;
            CreateMenuButton(menuSafeArea, "새 게임 시작", newGameLabelSprite, StartNewGame);
            continueButton = CreateMenuButton(menuSafeArea, "이어하기", continueLabelSprite, ContinueGame);
            CreateMenuButton(menuSafeArea, "설정", settingsLabelSprite, ShowSettings);
            CreateMenuButton(menuSafeArea, "나가기", quitLabelSprite, QuitGame);
            continueButton.interactable = saveData != null && GameLaunchSession.CanContinue(saveData);

            settingsGroup = BuildSettings(canvas.transform);
            SetGroup(settingsGroup, false);
            resolutionConfirmGroup = BuildResolutionConfirm(canvas.transform);
            SetGroup(resolutionConfirmGroup, false);
            loadingGroup = BuildLoading(canvas.transform);
            SetGroup(loadingGroup, false);
        }

        /// <summary>
        /// Bakes the runtime presentation into the scene hierarchy so artists can inspect and replace
        /// every button/frame/label sprite without entering Play Mode. Called only by the title scene tool.
        /// </summary>
        public void RebuildAuthoredPresentation()
        {
            if (Application.isPlaying) return;
            ResolveThemeSprites();
            var scene = gameObject.scene;
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == "TitleCanvas" || rootObject.name == "EventSystem")
                    DestroyImmediate(rootObject);
            }
            buttonActions.Clear();
            BuildPresentation();
            EnsureInputEventSystem();
            usesAuthoredPresentation = true;
        }
#endif

        private bool TryBindAuthoredPresentation()
        {
            var canvasObject = FindSceneRoot("TitleCanvas");
            if (canvasObject == null) return false;

            buttonActions.Clear();
            backgroundImage = FindNamedComponent<Image>(canvasObject.transform, "Background");
            zenithImage = FindNamedComponent<Image>(canvasObject.transform, "Zenith");
            promeImage = FindNamedComponent<Image>(canvasObject.transform, "Prome");
            introGroup = FindNamedComponent<CanvasGroup>(canvasObject.transform, "IntroPrompt");
            menuGroup = FindNamedComponent<CanvasGroup>(canvasObject.transform, "MainMenu");
            settingsGroup = FindNamedComponent<CanvasGroup>(canvasObject.transform, "Settings");
            settingsPanelRect = FindNamedComponent<Image>(canvasObject.transform, "SettingsPanel")?.rectTransform;
            loadingGroup = FindNamedComponent<CanvasGroup>(canvasObject.transform, "LoadingScreen");
            resolutionDropdown = FindNamedComponent<Dropdown>(canvasObject.transform, "ResolutionDropdown");
            displayModeDropdown = FindNamedComponent<Dropdown>(canvasObject.transform, "DisplayModeDropdown");
            masterSlider = FindNamedComponent<Slider>(canvasObject.transform, "전체 음량Slider");
            loadingText = FindNamedComponent<Text>(canvasObject.transform, "ProgressText");
            loadingBar = FindNamedComponent<Image>(canvasObject.transform, "ProgressFill");
            loadingCompassGlow = FindNamedComponent<Image>(canvasObject.transform, "CompassGlow");
            loadingCompassRect = FindNamedComponent<Image>(canvasObject.transform, "LoadingCompass")?.rectTransform;
            resolutionConfirmGroup = FindNamedComponent<CanvasGroup>(canvasObject.transform, "ResolutionConfirmPanel");
            resolutionConfirmCountdown = FindNamedComponent<Text>(canvasObject.transform, "ResolutionConfirmCountdown");

            cloudRects = new RectTransform[3];
            for (var index = 0; index < cloudRects.Length; index++)
            {
                var cloud = FindNamedComponent<Image>(canvasObject.transform, "CloudLayer_" + (index + 1));
                if (cloud == null) return false;
                cloudRects[index] = cloud.rectTransform;
            }

            if (backgroundImage == null || zenithImage == null || promeImage == null || introGroup == null ||
                menuGroup == null || settingsGroup == null || loadingGroup == null || resolutionDropdown == null ||
                displayModeDropdown == null || masterSlider == null ||
                loadingText == null || loadingBar == null || loadingCompassRect == null ||
                resolutionConfirmGroup == null || resolutionConfirmCountdown == null)
                return false;

            BindResolutionDropdown();
            BindMasterVolumeSlider();
            displayModeDropdown.ClearOptions();
            displayModeDropdown.AddOptions(new List<string>(DisplayModeLabels));

            RegisterAuthoredButton(canvasObject.transform, "새 게임 시작", StartNewGame);
            continueButton = RegisterAuthoredButton(canvasObject.transform, "이어하기", ContinueGame);
            RegisterAuthoredButton(canvasObject.transform, "설정", ShowSettings);
            RegisterAuthoredButton(canvasObject.transform, "나가기", QuitGame);
            RegisterAuthoredButton(canvasObject.transform, "설정 적용", ApplyAndCloseSettings);
            RegisterAuthoredButton(canvasObject.transform, "돌아가기", HideSettings);
            resetButton = RegisterAuthoredButton(canvasObject.transform, "초기화", RequestResetAllData);
            keepResolutionButton = RegisterAuthoredButton(canvasObject.transform, "해상도 유지", KeepResolution);
            revertResolutionButton = RegisterAuthoredButton(canvasObject.transform, "해상도 되돌리기", RevertResolution);
            resetButtonLabel = resetButton != null ? resetButton.transform.Find("Label")?.GetComponent<Image>() : null;
            if (buttonActions.Count != 9 || continueButton == null || resetButton == null || resetButtonLabel == null ||
                keepResolutionButton == null || revertResolutionButton == null)
                return false;

            continueButton.interactable = GameLaunchSession.CanContinue(saveData);
            menuShown = false;
            loading = false;
            SetGroup(introGroup, true);
            SetGroup(menuGroup, false);
            SetGroup(settingsGroup, false);
            SetGroup(resolutionConfirmGroup, false);
            SetGroup(loadingGroup, false);
            usesAuthoredPresentation = true;
            return true;
        }

        private Button RegisterAuthoredButton(
            Transform root,
            string objectName,
            UnityEngine.Events.UnityAction action)
        {
            var button = FindNamedComponent<Button>(root, objectName);
            if (button == null) return null;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            buttonActions.Add(new ButtonAction { Button = button });
            return button;
        }

        private GameObject FindSceneRoot(string objectName)
        {
            foreach (var rootObject in gameObject.scene.GetRootGameObjects())
                if (rootObject.name == objectName) return rootObject;
            return null;
        }

        private static T FindNamedComponent<T>(Transform root, string objectName) where T : Component
        {
            foreach (var component in root.GetComponentsInChildren<T>(true))
                if (component.name == objectName) return component;
            return null;
        }

#if UNITY_EDITOR
        private CanvasGroup BuildSettings(Transform parent)
        {
            var group = CreateGroup("Settings", parent);
            var blocker = CreatePanel("Blocker", group.transform, new Color(0.01f, 0.02f, 0.035f, 0.82f));
            Stretch(blocker.rectTransform);
            var panel = CreateImage("SettingsPanel", group.transform, modalPanelSprite,
                modalPanelSprite != null ? Color.white : new Color(0.035f, 0.065f, 0.095f, 0.98f));
            SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 820f));
            settingsPanelRect = panel.rectTransform;
            var contentBackdrop = CreatePanel("ContentBackdrop", panel.transform, new Color(0.01f, 0.025f, 0.045f, 0.7f));
            SetRect(contentBackdrop.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(820f, 660f));
            var safeArea = CreateRect("ContentSafeArea", panel.transform, new Vector2(0f, -10f), new Vector2(760f, 620f));
            CreateLabel(safeArea, "환경 설정", new Vector2(0f, 265f), 46, 700f, TextAnchor.MiddleCenter);
            CreateLabel(safeArea, "화면 설정", new Vector2(-280f, 198f), 25, 180f, TextAnchor.MiddleLeft,
                new Color(0.4f, 0.92f, 0.96f, 1f));
            CreateLabel(safeArea, "해상도", new Vector2(-245f, 132f), 27, 220f);
            resolutionDropdown = CreateDropdown(safeArea, new Vector2(120f, 132f), null, "ResolutionDropdown");
            CreateLabel(safeArea, "화면 모드", new Vector2(-245f, 58f), 27, 220f);
            displayModeDropdown = CreateDropdown(
                safeArea,
                new Vector2(120f, 58f),
                DisplayModeLabels,
                "DisplayModeDropdown");
            CreateLabel(safeArea, "오디오", new Vector2(-280f, -10f), 25, 180f, TextAnchor.MiddleLeft,
                new Color(0.4f, 0.92f, 0.96f, 1f));
            masterSlider = CreateVolumeRow(safeArea, "전체 음량", -76f);
            var apply = CreateMenuButton(safeArea, "설정 적용", applyLabelSprite, ApplyAndCloseSettings);
            SetRect(apply.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-145f, -178f), new Vector2(260f, 72f));
            var close = CreateMenuButton(safeArea, "돌아가기", backLabelSprite, HideSettings);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(145f, -178f), new Vector2(260f, 72f));
            resetButton = CreateMenuButton(safeArea, "초기화", resetLabelSprite, RequestResetAllData);
            SetRect(resetButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, -267f), new Vector2(360f, 72f));
            resetButtonLabel = resetButton.transform.Find("Label")?.GetComponent<Image>();
            return group;
        }

        private CanvasGroup BuildResolutionConfirm(Transform parent)
        {
            var group = CreateGroup("ResolutionConfirmPanel", parent);
            var blocker = CreatePanel("Blocker", group.transform, new Color(0.005f, 0.01f, 0.02f, 0.88f));
            Stretch(blocker.rectTransform);
            var panel = CreateImage("ConfirmFrame", group.transform, modalPanelSprite,
                modalPanelSprite != null ? Color.white : new Color(0.035f, 0.065f, 0.095f, 1f));
            SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 420f));
            var heading = CreateText("ConfirmTitle", panel.transform, "이 화면 설정을 유지할까요?", 34,
                TextAnchor.MiddleCenter, titleFont, Color.white);
            heading.fontStyle = FontStyle.Bold;
            SetRect(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 105f), new Vector2(600f, 64f));
            resolutionConfirmCountdown = CreateText("ResolutionConfirmCountdown", panel.transform, "10초 후 이전 설정으로 돌아갑니다.", 24,
                TextAnchor.MiddleCenter, bodyFont, Color.white);
            resolutionConfirmCountdown.fontStyle = FontStyle.Bold;
            SetRect(resolutionConfirmCountdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(600f, 54f));
            var keep = CreateMenuButton(panel.transform, "해상도 유지", applyLabelSprite, KeepResolution);
            SetRect(keep.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-155f, -105f), new Vector2(270f, 72f));
            var revert = CreateMenuButton(panel.transform, "해상도 되돌리기", backLabelSprite, RevertResolution);
            SetRect(revert.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(155f, -105f), new Vector2(270f, 72f));
            return group;
        }

        private CanvasGroup BuildLoading(Transform parent)
        {
            var group = CreateGroup("LoadingScreen", parent);
            var cover = CreatePanel("Cover", group.transform, new Color(0.012f, 0.022f, 0.04f, 1f));
            Stretch(cover.rectTransform);
            loadingCompassGlow = CreateImage("CompassGlow", group.transform, loadingCompassSprite,
                new Color(0.15f, 0.9f, 1f, 0.16f));
            loadingCompassGlow.preserveAspect = true;
            SetRect(loadingCompassGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 145f), new Vector2(300f, 300f));
            var compass = CreateImage("LoadingCompass", group.transform, loadingCompassSprite, Color.white);
            compass.preserveAspect = true;
            SetRect(compass.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 145f), new Vector2(245f, 245f));
            loadingCompassRect = compass.rectTransform;
            var informationFrame = CreateImage("LoadingInformationFrame", group.transform, titleLogoFrameSprite, Color.white);
            SetRect(informationFrame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -145f), new Vector2(840f, 315f));
            var heading = CreateText("Heading", informationFrame.transform, "제니스의 빛을 따라가는 중", 36,
                TextAnchor.MiddleCenter, titleFont, Color.white);
            heading.fontStyle = FontStyle.Bold;
            SetRect(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(620f, 65f));
            loadingText = CreateText("ProgressText", informationFrame.transform, "LOADING  0%", 23,
                TextAnchor.MiddleCenter, bodyFont, new Color(0.63f, 0.9f, 0.94f, 1f));
            loadingText.fontStyle = FontStyle.Bold;
            SetRect(loadingText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(500f, 50f));
            var track = CreateImage("ProgressTrack", informationFrame.transform, null, new Color(0.13f, 0.18f, 0.23f, 1f));
            SetRect(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -76f), new Vector2(570f, 12f));
            loadingBar = CreateImage("ProgressFill", track.transform, null, new Color(0.3f, 0.88f, 0.9f, 1f));
            loadingBar.type = Image.Type.Filled;
            loadingBar.fillMethod = Image.FillMethod.Horizontal;
            loadingBar.fillOrigin = 0;
            loadingBar.fillAmount = 0f;
            Stretch(loadingBar.rectTransform);
            return group;
        }
#endif

        private void AnimateArtLayers()
        {
            if (backgroundImage != null)
                backgroundImage.rectTransform.localScale = Vector3.one * (1.025f + Mathf.Sin(elapsed * 0.12f) * 0.008f);
            if (zenithImage != null)
            {
                var progress = Mathf.PingPong(elapsed / Mathf.Max(0.1f, zenithEaseSeconds), 1f);
                progress = progress * progress * (3f - 2f * progress);
                zenithImage.rectTransform.anchoredPosition = new Vector2(0f, 175f + Mathf.Lerp(-14f, 18f, progress));
                zenithImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.97f, 1.035f, progress);
            }
            if (cloudRects != null)
            {
                for (var index = 0; index < cloudRects.Length; index++)
                {
                    var rect = cloudRects[index];
                    rect.anchoredPosition += Vector2.right * (cloudPixelsPerSecond * (0.55f + index * 0.28f) * Time.unscaledDeltaTime);
                    if (rect.anchoredPosition.x > 2250f) rect.anchoredPosition = new Vector2(-900f, rect.anchoredPosition.y);
                }
            }
            if (promeImage != null && promeIdleFrames.Length > 0)
            {
                var next = Mathf.FloorToInt(elapsed * promeFramesPerSecond) % promeIdleFrames.Length;
                if (next != promeFrameIndex)
                {
                    promeFrameIndex = next;
                    promeImage.sprite = promeIdleFrames[promeFrameIndex];
                }
                promeImage.rectTransform.anchoredPosition = new Vector2(-575f, -175f + Mathf.Sin(elapsed * 1.35f) * 2.5f);
            }
            if (loadingCompassRect != null && loadingGroup != null && loadingGroup.alpha > 0.01f)
            {
                loadingCompassRect.localEulerAngles = new Vector3(0f, 0f, -elapsed * 22f);
                var pulse = 1f + Mathf.Sin(elapsed * 2.4f) * 0.035f;
                loadingCompassRect.localScale = Vector3.one * pulse;
                if (loadingCompassGlow != null)
                {
                    loadingCompassGlow.rectTransform.localEulerAngles = new Vector3(0f, 0f, elapsed * 9f);
                    loadingCompassGlow.rectTransform.localScale = Vector3.one * (1.08f + Mathf.Sin(elapsed * 1.7f) * 0.06f);
                    var glow = loadingCompassGlow.color;
                    glow.a = 0.12f + (Mathf.Sin(elapsed * 2.1f) + 1f) * 0.055f;
                    loadingCompassGlow.color = glow;
                }
            }
            if (!menuShown && introGroup != null)
                introGroup.alpha = 0.58f + Mathf.Sin(elapsed * 2.6f) * 0.32f;
        }

        private void ShowMenu()
        {
            menuShown = true;
            SetGroup(introGroup, false);
            SetGroup(menuGroup, true);
            SelectFirstVisibleButton();
        }

        private void ShowSettings()
        {
            PopulateSettingsUi();
            SetGroup(menuGroup, false);
            SetGroup(settingsGroup, true);
            RefreshSettingsLayout();
            SelectFirstVisibleButton();
        }

        private void OnRectTransformDimensionsChange() => RefreshSettingsLayout();

        private void RefreshSettingsLayout()
        {
            if (settingsPanelRect == null ||
                settingsPanelRect.GetComponentInParent<Canvas>()?.transform is not RectTransform canvasRect) return;
            var scale = ResolvePanelScale(canvasRect.rect.size, settingsPanelRect.sizeDelta, 40f);
            settingsPanelRect.localScale = new Vector3(scale, scale, 1f);
            settingsPanelRect.anchoredPosition = Vector2.zero;
        }

        public static float ResolvePanelScale(Vector2 canvasSize, Vector2 panelSize, float margin)
        {
            if (canvasSize.x <= 0f || canvasSize.y <= 0f || panelSize.x <= 0f || panelSize.y <= 0f) return 1f;
            var availableWidth = Mathf.Max(1f, canvasSize.x - margin * 2f);
            var availableHeight = Mathf.Max(1f, canvasSize.y - margin * 2f);
            return Mathf.Clamp(Mathf.Min(availableWidth / panelSize.x, availableHeight / panelSize.y), 0.45f, 1f);
        }

        private void HideSettings()
        {
            SetGroup(settingsGroup, false);
            if (menuShown && !loading) SetGroup(menuGroup, true);
            SelectFirstVisibleButton();
        }

        private void BindMasterVolumeSlider()
        {
            if (masterSlider == null) return;
            masterSlider.onValueChanged.RemoveListener(ApplyMasterVolumeImmediately);
            masterSlider.onValueChanged.AddListener(ApplyMasterVolumeImmediately);
        }

        private void ApplyMasterVolumeImmediately(float value)
        {
            value = Mathf.Clamp01(value);
            AudioListener.volume = value;
            if (saveData == null) return;
            var settings = saveData.Settings ??= new SettingsSaveData();
            settings.MasterVolume = value;
            settings.MusicVolume = 1f;
            settings.SfxVolume = 1f;
            GameLaunchSession.SaveSettings(CloneSettings(settings));
        }

        private void RequestResetAllData()
        {
            GameLaunchSession.ResetAllLocalData();
            saveData = GameLaunchSession.LoadSave();
            ApplySavedSettings();
            if (continueButton != null) continueButton.interactable = false;
            menuShown = false;
            loading = false;
            SetGroup(settingsGroup, false);
            SetGroup(menuGroup, false);
            SetGroup(loadingGroup, false);
            SetGroup(introGroup, true);
        }

        private void ApplyAndCloseSettings()
        {
            if (supportedResolutions.Count == 0) return;
            var resolution = supportedResolutions[Mathf.Clamp(resolutionDropdown.value, 0, supportedResolutions.Count - 1)];
            var displayMode = supportedDisplayModes[
                Mathf.Clamp(displayModeDropdown.value, 0, supportedDisplayModes.Length - 1)];
            if (displayMode == FullScreenMode.FullScreenWindow)
                resolution = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);

            previousSettings = CloneSettings(saveData.Settings);
            pendingSettings = CloneSettings(saveData.Settings);
            pendingSettings.ResolutionWidth = resolution.x;
            pendingSettings.ResolutionHeight = resolution.y;
            pendingSettings.DisplayMode = (int)displayMode;
            pendingSettings.HasDisplayModeSelection = true;
            pendingSettings.Fullscreen = displayMode != FullScreenMode.Windowed;
            pendingSettings.MasterVolume = masterSlider.value;
            pendingSettings.MusicVolume = 1f;
            pendingSettings.SfxVolume = 1f;
            ApplyScreenSettings(pendingSettings);
            SetGroup(settingsGroup, false);
            SetGroup(resolutionConfirmGroup, true);
            if (resolutionConfirmRoutine != null) StopCoroutine(resolutionConfirmRoutine);
            resolutionConfirmRoutine = StartCoroutine(ConfirmResolutionAfterApply());
        }

        private void ApplySavedSettings()
        {
            var settings = saveData.Settings ??= new SettingsSaveData();
            var resolution = FindClosestSupportedResolution(
                supportedResolutions,
                new Vector2Int(settings.ResolutionWidth, settings.ResolutionHeight));
            AudioListener.volume = Mathf.Clamp01(settings.MasterVolume);
            ApplyScreenSettings(settings);
            settings.MusicVolume = 1f;
            settings.SfxVolume = 1f;
            if (musicSource != null) musicSource.volume = 0.62f;
            PopulateSettingsUi();
        }

        private void PopulateSettingsUi()
        {
            if (resolutionDropdown == null) return;
            var settings = saveData.Settings ?? new SettingsSaveData();
            var best = 0;
            var bestDifference = int.MaxValue;
            for (var index = 0; index < supportedResolutions.Count; index++)
            {
                var difference = Mathf.Abs(supportedResolutions[index].x - settings.ResolutionWidth);
                if (difference >= bestDifference) continue;
                bestDifference = difference;
                best = index;
            }
            resolutionDropdown.SetValueWithoutNotify(best);
            var displayMode = ResolveDisplayMode(settings);
            var displayModeIndex = System.Array.IndexOf(supportedDisplayModes, displayMode);
            displayModeDropdown.value = Mathf.Max(0, displayModeIndex);
            resolutionDropdown.interactable = displayMode != FullScreenMode.FullScreenWindow;
            masterSlider.SetValueWithoutNotify(Mathf.Clamp01(settings.MasterVolume));
        }

        private void BindResolutionDropdown()
        {
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(supportedResolutions
                .Select(resolution => $"{resolution.x} × {resolution.y}").ToList());
            displayModeDropdown.onValueChanged.RemoveAllListeners();
            displayModeDropdown.onValueChanged.AddListener(index =>
                resolutionDropdown.interactable = supportedDisplayModes[Mathf.Clamp(index, 0, supportedDisplayModes.Length - 1)] !=
                                                   FullScreenMode.FullScreenWindow);
        }

        private IEnumerator ConfirmResolutionAfterApply()
        {
            const float timeout = 10f;
            const float stabilizationTimeout = 3f;
            const float requiredStableDuration = 0.15f;
            var stabilizationRemaining = stabilizationTimeout;
            var stableDuration = 0f;
            while (stabilizationRemaining > 0f && stableDuration < requiredStableDuration)
            {
                if (ScreenSettingsMatch(pendingSettings, Screen.width, Screen.height, Screen.fullScreenMode))
                    stableDuration += Time.unscaledDeltaTime;
                else
                    stableDuration = 0f;

                stabilizationRemaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (stableDuration < requiredStableDuration)
            {
                RevertResolution();
                yield break;
            }

            pendingSettings.ResolutionWidth = Mathf.Max(1, Screen.width);
            pendingSettings.ResolutionHeight = Mathf.Max(1, Screen.height);
            var remaining = timeout;
            while (remaining > 0f)
            {
                if (!ScreenSettingsMatch(pendingSettings, Screen.width, Screen.height, Screen.fullScreenMode))
                {
                    RevertResolution();
                    yield break;
                }
                if (resolutionConfirmCountdown != null)
                    resolutionConfirmCountdown.text = $"{Mathf.CeilToInt(remaining)}초 후 이전 설정으로 돌아갑니다.";
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
            RevertResolution();
        }

        public static bool ScreenSettingsMatch(
            SettingsSaveData settings,
            int actualWidth,
            int actualHeight,
            FullScreenMode actualMode)
        {
            const int sizeTolerancePixels = 2;
            if (settings == null || actualMode != ResolveDisplayMode(settings)) return false;

            var expectedWidth = settings.ResolutionWidth;
            var expectedHeight = settings.ResolutionHeight;
            if (actualMode == FullScreenMode.FullScreenWindow)
            {
                expectedWidth = Screen.currentResolution.width;
                expectedHeight = Screen.currentResolution.height;
            }

            return Mathf.Abs(actualWidth - Mathf.Max(1, expectedWidth)) <= sizeTolerancePixels &&
                   Mathf.Abs(actualHeight - Mathf.Max(1, expectedHeight)) <= sizeTolerancePixels;
        }

        private void KeepResolution()
        {
            if (pendingSettings == null) return;
            if (resolutionConfirmRoutine != null) StopCoroutine(resolutionConfirmRoutine);
            resolutionConfirmRoutine = null;
            saveData.Settings = CloneSettings(pendingSettings);
            GameLaunchSession.SaveSettings(saveData.Settings);
            pendingSettings = null;
            previousSettings = null;
            SetGroup(resolutionConfirmGroup, false);
            HideSettings();
        }

        private void RevertResolution()
        {
            if (resolutionConfirmRoutine != null) StopCoroutine(resolutionConfirmRoutine);
            resolutionConfirmRoutine = null;
            if (previousSettings != null) ApplyScreenSettings(previousSettings);
            pendingSettings = null;
            previousSettings = null;
            SetGroup(resolutionConfirmGroup, false);
            SetGroup(settingsGroup, true);
            PopulateSettingsUi();
            SelectFirstVisibleButton();
        }

        private static SettingsSaveData CloneSettings(SettingsSaveData source)
        {
            source ??= new SettingsSaveData();
            return new SettingsSaveData
            {
                MasterVolume = source.MasterVolume,
                MusicVolume = source.MusicVolume,
                SfxVolume = source.SfxVolume,
                ResolutionWidth = source.ResolutionWidth,
                ResolutionHeight = source.ResolutionHeight,
                Fullscreen = source.Fullscreen,
                DisplayMode = source.DisplayMode,
                HasDisplayModeSelection = source.HasDisplayModeSelection,
                InputBindingJson = source.InputBindingJson
            };
        }

        private static void ApplyScreenSettings(SettingsSaveData settings)
        {
            var mode = ResolveDisplayMode(settings);
            var width = settings.ResolutionWidth;
            var height = settings.ResolutionHeight;
            if (mode == FullScreenMode.FullScreenWindow)
            {
                width = Screen.currentResolution.width;
                height = Screen.currentResolution.height;
            }
            Screen.SetResolution(Mathf.Max(1, width), Mathf.Max(1, height), mode);
        }

        private void RefreshSupportedResolutions()
        {
            var detected = new List<Vector2Int>();
            foreach (var resolution in Screen.resolutions)
                detected.Add(new Vector2Int(resolution.width, resolution.height));

            var current = new Vector2Int(
                Mathf.Max(1, Screen.currentResolution.width),
                Mathf.Max(1, Screen.currentResolution.height));
            supportedResolutions.Clear();
            var options = BuildResolutionOptions(detected, current);
            supportedResolutions.AddRange(options.GetRange(0, Mathf.Min(16, options.Count)));
        }

        public static List<Vector2Int> BuildResolutionOptions(
            IEnumerable<Vector2Int> detectedResolutions,
            Vector2Int currentResolution)
        {
            var unique = new HashSet<Vector2Int>();
            if (detectedResolutions != null)
            {
                foreach (var resolution in detectedResolutions)
                {
                    if (resolution.x < 1280 || resolution.y < 720) continue;
                    unique.Add(resolution);
                }
            }

            if (unique.Count == 0)
            {
                var fallbacks = new[]
                {
                    new Vector2Int(1280, 720),
                    new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440),
                    new Vector2Int(3840, 2160)
                };
                var maxPixels = Mathf.Max(1920 * 1080, currentResolution.x * currentResolution.y);
                foreach (var fallback in fallbacks)
                    if (fallback.x * fallback.y <= maxPixels) unique.Add(fallback);
            }

            if (unique.Count == 0)
                unique.Add(new Vector2Int(1920, 1080));

            var options = new List<Vector2Int>(unique);
            options.Sort((left, right) =>
            {
                var pixelComparison = (left.x * left.y).CompareTo(right.x * right.y);
                return pixelComparison != 0 ? pixelComparison : left.x.CompareTo(right.x);
            });
            return options;
        }

        public static Vector2Int FindClosestSupportedResolution(
            IReadOnlyList<Vector2Int> options,
            Vector2Int requested)
        {
            if (options == null || options.Count == 0) return new Vector2Int(1920, 1080);
            var best = options[0];
            var bestScore = long.MaxValue;
            foreach (var option in options)
            {
                var widthDelta = (long)option.x - requested.x;
                var heightDelta = (long)option.y - requested.y;
                var score = widthDelta * widthDelta + heightDelta * heightDelta;
                if (score >= bestScore) continue;
                bestScore = score;
                best = option;
            }
            return best;
        }

        private void StartNewGame()
        {
            GameLaunchSession.PrepareNewGame();
            StartCoroutine(LoadScene(tutorialSceneName));
        }

        private void ContinueGame()
        {
            if (!GameLaunchSession.PrepareContinue()) return;
            StartCoroutine(LoadScene(tutorialSceneName));
        }

        private IEnumerator LoadScene(string sceneName)
        {
            if (loading || string.IsNullOrWhiteSpace(sceneName)) yield break;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"Build Settings에 '{sceneName}' 씬이 없습니다.", this);
                yield break;
            }
            loading = true;
            SetGroup(menuGroup, false);
            SetGroup(settingsGroup, false);
            SetGroup(loadingGroup, true);
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            operation.allowSceneActivation = false;
            var minimumPresentation = 0f;
            while (operation.progress < 0.9f || minimumPresentation < 1.25f)
            {
                minimumPresentation += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(operation.progress / 0.9f);
                loadingBar.fillAmount = normalized;
                loadingText.text = $"LOADING  {Mathf.RoundToInt(normalized * 100f)}%";
                yield return null;
            }
            loadingBar.fillAmount = 1f;
            loadingText.text = "LOADING  100%";
            yield return new WaitForSecondsRealtime(0.25f);
            operation.allowSceneActivation = true;
        }

        private static bool AnyInputPressed()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
            if (Gamepad.current == null) return false;
            foreach (var control in Gamepad.current.allControls)
                if (control.IsPressed()) return true;
            return false;
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("나가기는 플레이어 빌드에서 애플리케이션을 종료합니다.", this);
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        private void CreateLabel(
            Transform parent,
            string value,
            Vector2 position,
            int size,
            float width = 300f,
            TextAnchor alignment = TextAnchor.MiddleLeft,
            Color? color = null)
        {
            var label = CreateText(value, parent, value, size, alignment, titleFont, color ?? Color.white);
            label.fontStyle = FontStyle.Bold;
            SetRect(label.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(width, 56f));
        }

        private Slider CreateVolumeRow(Transform parent, string label, float y)
        {
            CreateLabel(parent, label, new Vector2(-245f, y), 26, 220f);
            var sliderObject = new GameObject(label + "Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            var slider = sliderObject.GetComponent<Slider>();
            SetRect(slider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(120f, y), new Vector2(460f, 44f));
            var background = CreateImage("Track", sliderObject.transform, volumeTrackSprite, Color.white);
            background.preserveAspect = true;
            SetRect(background.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 54f));
            var fill = CreateImage("EnergyFill", sliderObject.transform, volumeFillSprite, Color.white);
            SetRect(fill.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(404f, 20f));
            fill.raycastTarget = false;
            var handle = CreateImage("Handle", sliderObject.transform, volumeHandleSprite, Color.white);
            handle.preserveAspect = true;
            SetRect(handle.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            slider.fillRect = null;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            sliderObject.AddComponent<ThemedVolumeSliderPresenter>().Configure(slider, fill);
            return slider;
        }

        private Dropdown CreateDropdown(Transform parent, Vector2 position, IReadOnlyList<string> labels = null,
            string objectName = "ResolutionDropdown")
        {
            var root = CreatePanel(objectName, parent, new Color(0.08f, 0.13f, 0.18f, 1f));
            SetRect(root.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(460f, 58f));
            var dropdown = root.gameObject.AddComponent<Dropdown>();
            var caption = CreateText("Label", root.transform, "", 23, TextAnchor.MiddleLeft, titleFont, Color.white);
            caption.fontStyle = FontStyle.Bold;
            caption.rectTransform.anchorMin = Vector2.zero;
            caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = new Vector2(20f, 8f);
            caption.rectTransform.offsetMax = new Vector2(-60f, -8f);
            var arrow = CreateText("Arrow", root.transform, "▼", 22, TextAnchor.MiddleCenter, titleFont,
                new Color(0.4f, 0.92f, 0.96f, 1f));
            arrow.fontStyle = FontStyle.Bold;
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = Vector2.one;
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.anchoredPosition = new Vector2(-12f, 0f);
            arrow.rectTransform.sizeDelta = new Vector2(44f, 0f);
            dropdown.captionText = caption;
            dropdown.targetGraphic = root;
            dropdown.options = new List<Dropdown.OptionData>();
            if (labels != null)
            {
                foreach (var label in labels)
                    dropdown.options.Add(new Dropdown.OptionData(label));
            }
            else
            {
                foreach (var resolution in supportedResolutions)
                    dropdown.options.Add(new Dropdown.OptionData($"{resolution.x} × {resolution.y}"));
            }
            var template = CreatePanel("Template", root.transform, new Color(0.055f, 0.09f, 0.13f, 1f));
            template.gameObject.AddComponent<ScrollRect>();
            template.rectTransform.anchorMin = new Vector2(0f, 0f);
            template.rectTransform.anchorMax = new Vector2(1f, 0f);
            template.rectTransform.pivot = new Vector2(0.5f, 1f);
            template.rectTransform.anchoredPosition = new Vector2(0f, -4f);
            template.rectTransform.sizeDelta = new Vector2(0f, 210f);
            var viewport = CreatePanel("Viewport", template.transform, new Color(1f, 1f, 1f, 0.01f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewport.rectTransform, 4f);
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
            item.transform.SetParent(content.transform, false);
            item.GetComponent<LayoutElement>().preferredHeight = 48f;
            var itemBackground = CreateImage("Item Background", item.transform, null, new Color(0.08f, 0.14f, 0.19f, 0.94f));
            Stretch(itemBackground.rectTransform);
            var itemCheck = CreateImage("Item Checkmark", item.transform, null, new Color(0.25f, 0.86f, 0.9f, 0.8f));
            itemCheck.rectTransform.anchorMin = new Vector2(0f, 0f);
            itemCheck.rectTransform.anchorMax = new Vector2(0f, 1f);
            itemCheck.rectTransform.sizeDelta = new Vector2(5f, 0f);
            var itemLabel = CreateText("Item Label", item.transform, "Option", 21, TextAnchor.MiddleCenter, titleFont, Color.white);
            itemLabel.fontStyle = FontStyle.Bold;
            Stretch(itemLabel.rectTransform, 6f);
            var itemToggle = item.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBackground;
            itemToggle.graphic = itemCheck;
            var scrollRect = template.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport.rectTransform;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            dropdown.template = template.rectTransform;
            dropdown.itemText = itemLabel;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private Button CreateMenuButton(
            Transform parent,
            string label,
            Sprite labelSprite,
            UnityEngine.Events.UnityAction action)
        {
            var panel = CreateImage(label, parent, buttonFrameSprite,
                buttonFrameSprite != null ? Color.white : new Color(0.06f, 0.105f, 0.145f, 0.96f));
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            button.onClick.AddListener(action);
            buttonActions.Add(new ButtonAction { Button = button });
            var layoutElement = panel.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 96f;
            layoutElement.flexibleHeight = 0f;
            if (labelSprite != null)
            {
                var labelImage = CreateImage("Label", panel.transform, labelSprite, Color.white);
                labelImage.preserveAspect = true;
                Stretch(labelImage.rectTransform, 16f);
                labelImage.raycastTarget = false;
            }
            else
            {
                var fallbackText = CreateText("LabelFallback", panel.transform, label, 32, TextAnchor.MiddleCenter,
                    titleFont, Color.white);
                fallbackText.fontStyle = FontStyle.Bold;
                Stretch(fallbackText.rectTransform, 16f);
            }
            panel.gameObject.AddComponent<TitleMenuHoldAnimator>().Configure(null);
            return button;
        }
#endif

        private void HandleManualUiInput()
        {
            HandleKeyboardNavigation();
        }

        private void HandleKeyboardNavigation()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            var visible = buttonActions.FindAll(binding => IsButtonVisible(binding.Button));
            if (visible.Count == 0) return;
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                selectedButtonIndex = (selectedButtonIndex + 1) % visible.Count;
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                selectedButtonIndex = (selectedButtonIndex - 1 + visible.Count) % visible.Count;
            selectedButtonIndex = Mathf.Clamp(selectedButtonIndex, 0, visible.Count - 1);
            EventSystem.current?.SetSelectedGameObject(visible[selectedButtonIndex].Button.gameObject);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                visible[selectedButtonIndex].Button.onClick.Invoke();
            if (SettingsVisible && keyboard.escapeKey.wasPressedThisFrame) HideSettings();
        }

        private void SelectFirstVisibleButton()
        {
            selectedButtonIndex = 0;
            foreach (var binding in buttonActions)
            {
                if (!IsButtonVisible(binding.Button)) continue;
                EventSystem.current?.SetSelectedGameObject(binding.Button.gameObject);
                return;
            }
        }

        private static bool IsButtonVisible(Button button)
        {
            if (button == null || !button.IsActive() || !button.interactable) return false;
            var groups = button.GetComponentsInParent<CanvasGroup>(true);
            foreach (var group in groups)
                if (group.alpha <= 0.01f || !group.interactable) return false;
            return true;
        }

#if UNITY_EDITOR
        private CanvasGroup CreateGroup(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            return root.GetComponent<CanvasGroup>();
        }

        private static Image CreatePanel(string name, Transform parent, Color color) => CreateImage(name, parent, null, color);

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(0.5f, 0.5f), position, size);
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor, Font font, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Text));
            root.transform.SetParent(parent, false);
            var text = root.GetComponent<Text>();
            text.text = value;
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(14, size / 2);
            text.resizeTextMaxSize = size;
            text.raycastTarget = false;
            var outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }
#endif

        private static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * margin;
            rect.offsetMax = Vector2.one * -margin;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetGroup(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static bool IsGroupVisible(CanvasGroup group) =>
            group != null && group.alpha > 0.01f && group.interactable && group.blocksRaycasts;

#if UNITY_EDITOR
        private static Sprite CreateCloudSprite()
        {
            const int width = 256;
            const int height = 96;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "RuntimeTitleCloud",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var alpha = 0f;
                alpha += Blob(x, y, 58f, 55f, 52f, 25f);
                alpha += Blob(x, y, 115f, 40f, 63f, 34f);
                alpha += Blob(x, y, 176f, 57f, 70f, 24f);
                alpha += Blob(x, y, 222f, 48f, 42f, 22f);
                pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha * 0.62f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float Blob(float x, float y, float centerX, float centerY, float radiusX, float radiusY)
        {
            var dx = (x - centerX) / radiusX;
            var dy = (y - centerY) / radiusY;
            return Mathf.Exp(-(dx * dx + dy * dy) * 2.4f);
        }
#endif

        private void ResolveThemeSprites()
        {
            titleLogoFrameSprite ??= Resources.Load<Sprite>("UI/Title/TITLE_UI_LogoFrame_v1");
            buttonFrameSprite ??= Resources.Load<Sprite>("UI/Title/TITLE_UI_ButtonPlate_v2");
            volumeTrackSprite ??= Resources.Load<Sprite>("UI/Title/TITLE_UI_VolumeTrack_v1");
            volumeFillSprite ??= Resources.Load<Sprite>("UI/Title/TITLE_UI_VolumeFill_v1");
            volumeHandleSprite ??= Resources.Load<Sprite>("UI/Title/TITLE_UI_VolumeHandle_v1");
            loadingCompassSprite ??= Resources.Load<Sprite>("UI/Title/TITLE_UI_LoadingCompass_v1");
            modalPanelSprite ??= Resources.Load<Sprite>("UI/Title/TITLE_UI_ModalPanel_v1");
            newGameLabelSprite ??= Resources.Load<Sprite>("UI/Title/Labels/TITLE_LABEL_NewGame_v1");
            continueLabelSprite ??= Resources.Load<Sprite>("UI/Title/Labels/TITLE_LABEL_Continue_v1");
            settingsLabelSprite ??= Resources.Load<Sprite>("UI/Title/Labels/TITLE_LABEL_Settings_v1");
            quitLabelSprite ??= Resources.Load<Sprite>("UI/Title/Labels/TITLE_LABEL_Quit_v1");
            applyLabelSprite ??= Resources.Load<Sprite>("UI/Title/Labels/TITLE_LABEL_Apply_v1");
            backLabelSprite ??= Resources.Load<Sprite>("UI/Title/Labels/TITLE_LABEL_Back_v1");
            resetLabelSprite ??= Resources.Load<Sprite>("UI/Title/Labels/TITLE_LABEL_Reset_v1");
            resetConfirmLabelSprite ??= Resources.Load<Sprite>("UI/Title/Labels/TITLE_LABEL_ResetConfirm_v1");
        }

        public static FullScreenMode ResolveDisplayMode(SettingsSaveData settings)
        {
            if (settings == null) return FullScreenMode.FullScreenWindow;
            if (!settings.HasDisplayModeSelection)
                return settings.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            return settings.DisplayMode switch
            {
                (int)FullScreenMode.ExclusiveFullScreen => FullScreenMode.ExclusiveFullScreen,
                (int)FullScreenMode.FullScreenWindow => FullScreenMode.FullScreenWindow,
                (int)FullScreenMode.Windowed => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };
        }

#if UNITY_EDITOR
        private static void EnsureInputEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            var inputModule = eventSystem.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }
#endif
    }
}
