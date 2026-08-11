using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using Narthex.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.Tools
{
    public static class PrometheusTutorialDemoEndingAutomation
    {
        private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string BoardingAirshipSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialEnding/Generated/TUTO_END_Airship_v1.png";
        private const string FlightAirshipSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialEnding/Generated/TUTO_END_AirshipRear_v1.png";
        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (!scene.IsValid() || scene.path != TutorialScenePath)
                throw new InvalidOperationException("Demo ending automation is restricted to TutorialScene.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying the demo ending.");

            var changes = Describe(scene);
            var boardingSprite = LoadSprite(BoardingAirshipSpritePath);
            var flightSprite = LoadSprite(FlightAirshipSpritePath);
            if (dryRun) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply tutorial demo airship ending");
            try
            {
                var stageSystems = Require(scene, "StageSystems");
                var hud = Require(scene, "TutorialHUD").transform;
                var player = Require(scene, "PlayerRoot");
                var completion = RequireComponent<TutorialCompletionFlowHost>(stageSystems);
                var transition = RequireComponent<Chapter01TransitionHost>(stageSystems);
                var input = RequireComponent<PlayerInputHost>(player);
                var body = RequireComponent<Rigidbody2D>(player);
                var resultOverlay = Require(scene, "TutorialResultOverlay");
                var resultText = RequireComponent<Text>(Require(scene, "TutorialResultText"));
                var nextButton = Require(scene, "EnterChapter01Button");
                var nextButtonComponent = RequireComponent<Button>(nextButton);
                var nextButtonLabel = nextButton.GetComponentInChildren<Text>(true) ??
                                      throw new InvalidOperationException("EnterChapter01Button label is missing.");
                var helte = Require(scene, "TutorialHelte");
                var helteRegion = Require(scene, "H_Helte_Integration");
                var backgroundRoot = Require(scene, "AI_TutorialBackgroundRoot");
                var zenithPresenter = RequireBehaviourByTypeName(backgroundRoot, "ZenithApproachPresenter");
                var worldZenith = RequireComponent<SpriteRenderer>(Require(scene, "Zenith_Continuous"));

                var boardingPoint = ConfigureWorldMarker(helteRegion.transform,
                    "DemoEndingBoardingPoint_MARKER", "DEMO-END-BOARDING-POINT",
                    helte.transform.position + new Vector3(6f, 0f, 0f));
                var dockedAirship = ConfigureWorldAirship(helteRegion.transform,
                    "DemoEndingDockedAirship_ART", boardingSprite,
                    boardingPoint.position + new Vector3(3.5f, 2.8f, 0f), 14f);

                var cinematicRoot = GetOrCreateRectChild(hud, "DemoEndingCinematicRoot");
                Stretch(cinematicRoot);
                var cinematicCanvas = GetOrAdd<CanvasGroup>(cinematicRoot.gameObject);
                cinematicCanvas.alpha = 0f;
                cinematicCanvas.blocksRaycasts = false;
                cinematicCanvas.interactable = false;

                var startMarker = ConfigureMarker(cinematicRoot, "DemoEndingFlightStart_MARKER",
                    "DEMO-END-FLIGHT-START", new Vector2(-560f, -180f));
                var endMarker = ConfigureMarker(cinematicRoot, "DemoEndingFlightEnd_MARKER",
                    "DEMO-END-FLIGHT-END", new Vector2(-30f, 55f));
                DestroyChildIfPresent(cinematicRoot, "DemoEndingZenith_ART");
                DestroyChildIfPresent(cinematicRoot, "DemoEndingZenithStart_MARKER");
                DestroyChildIfPresent(cinematicRoot, "DemoEndingZenithEnd_MARKER");
                var zenithCenterMarker = ConfigureLocalWorldMarker(backgroundRoot.transform,
                    "DemoEndingZenithCenter_MARKER", "DEMO-END-ZENITH-CENTER", new Vector3(0f, 0.55f, 0f));

                var boardingAirship = ConfigureAirship(cinematicRoot, "DemoEndingBoardingAirship_ART",
                    boardingSprite, startMarker.anchoredPosition, new Vector2(760f, 380f));
                var flightAirship = ConfigureAirship(cinematicRoot, "DemoEndingRearFlightAirship_ART",
                    flightSprite, startMarker.anchoredPosition, new Vector2(620f, 620f));
                flightAirship.gameObject.SetActive(false);

                var caption = GetOrCreateRectChild(cinematicRoot, "DemoEndingCaptionText");
                caption.anchorMin = caption.anchorMax = new Vector2(0.5f, 0f);
                caption.pivot = new Vector2(0.5f, 0f);
                caption.anchoredPosition = new Vector2(0f, 115f);
                caption.sizeDelta = new Vector2(1400f, 72f);
                var captionText = GetOrAdd<Text>(caption.gameObject);
                captionText.font = resultText.font;
                captionText.fontSize = 34;
                captionText.alignment = TextAnchor.MiddleCenter;
                captionText.color = new Color(0.82f, 0.96f, 1f, 0.95f);
                captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
                captionText.verticalOverflow = VerticalWrapMode.Overflow;
                captionText.raycastTarget = false;
                captionText.text = "패스키 확인 · 비행정에 탑승합니다.";
                EnsureOutline(caption.gameObject);

                var fade = GetOrCreateRectChild(hud, "DemoEndingFadeOverlay");
                Stretch(fade);
                var fadeImage = GetOrAdd<Image>(fade.gameObject);
                fadeImage.sprite = null;
                fadeImage.color = Color.black;
                fadeImage.raycastTarget = true;
                var fadeCanvas = GetOrAdd<CanvasGroup>(fade.gameObject);
                fadeCanvas.alpha = 0f;
                fadeCanvas.blocksRaycasts = false;
                fadeCanvas.interactable = false;
                fade.SetAsLastSibling();

                var sequence = stageSystems.GetComponent<TutorialDemoEndingSequenceHost>();
                if (sequence == null) sequence = Undo.AddComponent<TutorialDemoEndingSequenceHost>(stageSystems);
                var sequenceSerialized = new SerializedObject(sequence);
                Assign(sequenceSerialized, "completionFlow", completion);
                Assign(sequenceSerialized, "playerInputHost", input);
                Assign(sequenceSerialized, "playerBody", body);
                AssignArray(sequenceSerialized, "playerRenderers",
                    player.GetComponentsInChildren<Renderer>(true).Cast<UnityEngine.Object>().ToArray());
                Assign(sequenceSerialized, "boardingPointMarker", boardingPoint);
                Assign(sequenceSerialized, "dockedAirshipVisual", dockedAirship);
                AssignArray(sequenceSerialized, "worldRootsToHideAfterBoarding",
                    CollectWorldRootsToHide(scene, stageSystems.transform, backgroundRoot.transform, hud));
                AssignArray(sequenceSerialized, "hudRootsToHideOnDefeat",
                    CollectHudRootsToHide(hud, resultOverlay.transform, cinematicRoot, fade));
                Assign(sequenceSerialized, "cinematicCanvas", cinematicCanvas);
                Assign(sequenceSerialized, "boardingAirshipVisual", boardingAirship);
                Assign(sequenceSerialized, "flightAirshipVisual", flightAirship);
                Assign(sequenceSerialized, "flightStartMarker", startMarker);
                Assign(sequenceSerialized, "flightEndMarker", endMarker);
                Assign(sequenceSerialized, "zenithApproachPresenter", zenithPresenter);
                Assign(sequenceSerialized, "worldZenithRenderer", worldZenith);
                Assign(sequenceSerialized, "zenithCenterMarker", zenithCenterMarker);
                Assign(sequenceSerialized, "fadeCanvas", fadeCanvas);
                Assign(sequenceSerialized, "captionText", captionText);
                var resultContentCanvas = GetOrAdd<CanvasGroup>(resultText.gameObject);
                Assign(sequenceSerialized, "resultContentCanvas", resultContentCanvas);
                Assign(sequenceSerialized, "resultTextRect", resultText.rectTransform);
                Assign(sequenceSerialized, "returnToTitleButton", nextButtonComponent);
                Assign(sequenceSerialized, "returnToTitleButtonLabel", nextButtonLabel);
                sequenceSerialized.FindProperty("titleSceneName").stringValue = "TitleScene";
                sequenceSerialized.FindProperty("autoBoardSeconds").floatValue = 2.8f;
                sequenceSerialized.FindProperty("boardingFadeSeconds").floatValue = 0.65f;
                sequenceSerialized.FindProperty("boardingHoldSeconds").floatValue = 0.45f;
                sequenceSerialized.FindProperty("flightSeconds").floatValue = 7f;
                sequenceSerialized.FindProperty("finalFadeSeconds").floatValue = 1.25f;
                sequenceSerialized.FindProperty("startScale").floatValue = 1f;
                sequenceSerialized.FindProperty("endScale").floatValue = 0.12f;
                sequenceSerialized.FindProperty("zenithEndScaleMultiplier").floatValue = 1.18f;
                sequenceSerialized.FindProperty("resultRiseSeconds").floatValue = 1.2f;
                sequenceSerialized.FindProperty("resultRiseDistance").floatValue = 120f;
                sequenceSerialized.FindProperty("boardingCaption").stringValue =
                    "프로메가 선착장의 비행정으로 향합니다.";
                sequenceSerialized.ApplyModifiedProperties();

                var completionSerialized = new SerializedObject(completion);
                Assign(completionSerialized, "demoEndingSequence", sequence);
                completionSerialized.ApplyModifiedProperties();

                var transitionSerialized = new SerializedObject(transition);
                transitionSerialized.FindProperty("allowChapterTransition").boolValue = false;
                transitionSerialized.ApplyModifiedProperties();

                Undo.RecordObject(nextButton, "Hide chapter transition button for demo ending");
                nextButton.SetActive(false);
                var nextButtonRect = RequireComponent<RectTransform>(nextButton);
                Undo.RecordObject(nextButtonRect, "Enlarge and lower title return button");
                nextButtonRect.anchorMin = nextButtonRect.anchorMax = new Vector2(0.5f, 0f);
                nextButtonRect.pivot = new Vector2(0.5f, 0f);
                nextButtonRect.anchoredPosition = new Vector2(0f, 72f);
                nextButtonRect.sizeDelta = new Vector2(560f, 116f);
                Undo.RecordObject(nextButtonLabel, "Configure title return label");
                nextButtonLabel.text = "타이틀 화면으로";
                nextButtonLabel.fontSize = 34;
                Undo.RecordObject(resultText, "Configure demo ending result text");
                Undo.RecordObject(resultText.rectTransform, "Resize demo ending result text");
                resultText.text = "DEMO VERSION\n\nTO BE CONTINUED";
                resultText.fontSize = 42;
                resultText.alignment = TextAnchor.MiddleCenter;
                resultText.rectTransform.sizeDelta = new Vector2(1100f, 300f);
                var overlayImage = resultOverlay.GetComponent<Image>();
                if (overlayImage != null)
                {
                    Undo.RecordObject(overlayImage, "Darken demo result overlay");
                    overlayImage.color = new Color(0.005f, 0.012f, 0.02f, 0.96f);
                }

                EditorUtility.SetDirty(sequence);
                EditorUtility.SetDirty(completion);
                EditorUtility.SetDirty(transition);
                EditorUtility.SetDirty(resultText);
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                return changes;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static List<PrometheusAiChange> Describe(Scene scene) => new()
        {
            Change("delay-result-until-epilogue", Require(scene, "StageSystems"), "instant result overlay", "airship epilogue then result overlay"),
            Change("add-boarding-airship", Require(scene, "TutorialHUD"), "no boarding visual", BoardingAirshipSpritePath),
            Change("add-rear-flight-airship", Require(scene, "TutorialHUD"), "no rear flight shot", FlightAirshipSpritePath),
            Change("add-flight-route-markers", Require(scene, "TutorialHUD"), "no editable route", "DEMO-END-FLIGHT-START / DEMO-END-FLIGHT-END"),
            Change("add-world-boarding-marker", Require(scene, "H_Helte_Integration"), "no authored boarding route", "DEMO-END-BOARDING-POINT"),
            Change("add-docked-airship", Require(scene, "H_Helte_Integration"), "no docked airship", BoardingAirshipSpritePath),
            Change("reuse-world-zenith", Require(scene, "AI_TutorialBackgroundRoot"), "separate UI Zenith duplicate", "Zenith_Continuous moves to screen center"),
            Change("hide-world-after-boarding", Require(scene, "H_Helte_Integration"), "H platforms and world objects remain visible", "only background, existing Zenith, and airship remain"),
            Change("hide-all-gameplay-hud-on-defeat", Require(scene, "TutorialHUD"), "some tutorial HUD survives Helte defeat", "all gameplay HUD roots hidden; ending/result roots retained"),
            Change("lock-player-during-ending", Require(scene, "PlayerRoot"), "player remains controllable", "input locked while boarding and flying"),
            Change("convert-result-to-demo-end", Require(scene, "TutorialResultOverlay"), "Chapter 1 transition", "DEMO VERSION / TO BE CONTINUED"),
            Change("repurpose-title-button", Require(scene, "EnterChapter01Button"), "small centered Chapter 1 transition", "560x116 lower title return button revealed after demo ending")
        };

        private static UnityEngine.Object[] CollectHudRootsToHide(
            Transform hud,
            Transform resultOverlay,
            Transform cinematicRoot,
            Transform fadeRoot)
        {
            var excluded = new HashSet<Transform> { resultOverlay, cinematicRoot, fadeRoot };
            var result = new List<UnityEngine.Object>();
            for (var index = 0; index < hud.childCount; index++)
            {
                var child = hud.GetChild(index);
                if (excluded.Contains(child)) continue;
                result.Add(child.gameObject);
            }
            return result.ToArray();
        }

        private static Transform ConfigureWorldMarker(Transform parent, string name, string markerId,
            Vector3 worldPosition)
        {
            var markerTransform = parent.Find(name);
            if (markerTransform == null)
            {
                var markerObject = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(markerObject, $"Create {name}");
                markerTransform = markerObject.transform;
                markerTransform.SetParent(parent, true);
            }

            Undo.RecordObject(markerTransform, $"Position {name}");
            markerTransform.position = worldPosition;
            markerTransform.rotation = Quaternion.identity;
            markerTransform.localScale = Vector3.one;
            var marker = GetOrAdd<TutorialFunctionMarkerHost>(markerTransform.gameObject);
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("markerId").stringValue = markerId;
            serialized.FindProperty("kind").enumValueIndex = (int)TutorialFunctionMarkerKind.Transition;
            serialized.FindProperty("gizmoSize").vector2Value = new Vector2(1.2f, 1.2f);
            serialized.ApplyModifiedProperties();
            return markerTransform;
        }

        private static Transform ConfigureLocalWorldMarker(Transform parent, string name, string markerId,
            Vector3 localPosition)
        {
            var marker = ConfigureWorldMarker(parent, name, markerId, parent.TransformPoint(localPosition));
            Undo.RecordObject(marker, $"Position {name} in background space");
            marker.localPosition = localPosition;
            marker.localRotation = Quaternion.identity;
            marker.localScale = Vector3.one;
            return marker;
        }

        private static UnityEngine.Object[] CollectWorldRootsToHide(
            Scene scene,
            Transform stageSystems,
            Transform backgroundRoot,
            Transform hud)
        {
            var targets = new List<GameObject>();
            var tutorialRuntimeRoot = Require(scene, "TutorialRuntimeRoot").transform;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.transform == tutorialRuntimeRoot || root.transform == backgroundRoot) continue;
                targets.Add(root);
            }

            var stageRoot = stageSystems.parent;
            if (stageRoot == null)
                throw new InvalidOperationException("StageSystems must be under StageRoot.");
            foreach (Transform child in stageRoot)
            {
                if (child == stageSystems || child.name == "TutorialAudioRoot") continue;
                targets.Add(child.gameObject);
            }

            return targets
                .Where(item => item != null && item.transform != backgroundRoot && item.transform != hud)
                .Distinct()
                .Cast<UnityEngine.Object>()
                .ToArray();
        }

        private static void DestroyChildIfPresent(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
        }

        private static GameObject ConfigureWorldAirship(Transform parent, string name, Sprite sprite,
            Vector3 worldPosition, float worldWidth)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var childObject = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(childObject, $"Create {name}");
                child = childObject.transform;
                child.SetParent(parent, true);
            }

            var renderer = GetOrAdd<SpriteRenderer>(child.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { child, renderer }, $"Configure {name}");
            child.position = worldPosition;
            child.rotation = Quaternion.identity;
            var scale = worldWidth / Mathf.Max(0.01f, sprite.bounds.size.x);
            child.localScale = new Vector3(scale, scale, 1f);
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 820;
            child.gameObject.SetActive(true);
            return child.gameObject;
        }

        private static RectTransform ConfigureMarker(Transform parent, string name, string markerId, Vector2 position)
        {
            var rect = GetOrCreateRectChild(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(24f, 24f);
            var marker = GetOrAdd<TutorialFunctionMarkerHost>(rect.gameObject);
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("markerId").stringValue = markerId;
            serialized.FindProperty("kind").enumValueIndex = (int)TutorialFunctionMarkerKind.Transition;
            serialized.FindProperty("gizmoSize").vector2Value = new Vector2(24f, 24f);
            serialized.ApplyModifiedProperties();
            return rect;
        }

        private static RectTransform ConfigureAirship(Transform parent, string name, Sprite sprite,
            Vector2 position, Vector2 size)
        {
            var rect = GetOrCreateRectChild(parent, name);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            var image = GetOrAdd<Image>(rect.gameObject);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            return rect;
        }

        private static Sprite LoadSprite(string path) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
            throw new InvalidOperationException($"Sprite is missing or not imported as Sprite: {path}");

        private static RectTransform GetOrCreateRectChild(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var child = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
            var rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureOutline(GameObject target)
        {
            var outline = GetOrAdd<Outline>(target);
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);
        }

        private static void Assign(SerializedObject serialized, string propertyName, UnityEngine.Object value) =>
            serialized.FindProperty(propertyName).objectReferenceValue = value;

        private static void AssignArray(SerializedObject serialized, string propertyName, UnityEngine.Object[] values)
        {
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static GameObject Require(Scene scene, string name)
        {
            var match = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == name);
            return match != null ? match.gameObject : throw new InvalidOperationException($"Missing scene object: {name}");
        }

        private static T RequireComponent<T>(GameObject gameObject) where T : Component =>
            gameObject.GetComponent<T>() ?? throw new InvalidOperationException($"{gameObject.name} is missing {typeof(T).Name}.");

        private static Behaviour RequireBehaviourByTypeName(GameObject gameObject, string typeName) =>
            gameObject.GetComponents<Behaviour>().FirstOrDefault(item => item.GetType().Name == typeName) ??
            throw new InvalidOperationException($"{gameObject.name} is missing {typeName}.");

        private static PrometheusAiChange Change(string action, GameObject target, string before, string after) =>
            new()
            {
                action = action,
                hierarchyPath = BuildPath(target.transform),
                before = before,
                after = after
            };

        private static string BuildPath(Transform transform)
        {
            var parts = new List<string>();
            for (var current = transform; current != null; current = current.parent) parts.Add(current.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
