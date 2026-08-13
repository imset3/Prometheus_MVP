using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using Narthex.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.Tools
{
    public static class PrometheusHierarchyAuthoringAutomation
    {
        private static readonly string[] UiRoots =
        {
            "SafeAreaRoot", "PrimaryHudRoot", "ModalRoot", "DialogueRoot", "TransitionRoot"
        };

        public static List<PrometheusAiChange> ApplyUiReadability(Scene scene, bool dryRun)
        {
            var changes = UiRoots.Select(name => Change("author-ui-root", name)).ToList();
            changes.Add(Change("author-canvas", "1920x1080 / match 0.5 / authored-only panels"));
            if (dryRun) return changes;

            if (scene.name == "TitleScene")
            {
                PrometheusTitleSceneAutomation.Apply(scene, false);
                var titleCanvas = FindComponent<Canvas>(scene);
                if (titleCanvas == null) throw new InvalidOperationException("TitleCanvas is missing after authoring.");
                ConfigureCanvas(titleCanvas);
                OrganizeUiRoots(titleCanvas.transform);
            }
            else
            {
                var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(item => item.name == "TutorialHUD");
                var canvas = hud != null ? hud.GetComponent<Canvas>() : FindComponent<Canvas>(scene);
                if (canvas == null) throw new InvalidOperationException($"{scene.name} has no authored Canvas.");
                ConfigureCanvas(canvas);
                OrganizeUiRoots(canvas.transform);
                var safeArea = FindChild(canvas.transform, "SafeAreaRoot").gameObject;
                if (scene.name == "TutorialScene") AuthorPauseMenu(scene, safeArea.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var validation = ValidateUiReadability(scene);
            if (validation.Count > 0)
                throw new InvalidOperationException("Authored UI validation failed: " +
                                                    string.Join(" | ", validation.Select(item => item.after)));
            return changes;
        }

        public static List<PrometheusAiChange> ValidateUiReadability(Scene scene)
        {
            var issues = new List<PrometheusAiChange>();
            var canvasNames = scene.name == "TitleScene"
                ? new[] { "TitleCanvas" }
                : scene.name == "TutorialScene" ? new[] { "PauseCanvas" } : Array.Empty<string>();
            foreach (var canvasName in canvasNames)
            {
                var canvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == canvasName);
                if (canvas == null)
                {
                    issues.Add(Change("ui-canvas-missing", canvasName));
                    continue;
                }

                foreach (var panelName in scene.name == "TitleScene"
                             ? new[] { "MenuPanel", "SettingsPanel" }
                             : new[] { "PausePanel", "SettingsPanel" })
                {
                    var panel = canvas.GetComponentsInChildren<RectTransform>(true)
                        .FirstOrDefault(item => item.name == panelName);
                    if (panel == null || panel.Find("ContentSafeArea") == null)
                        issues.Add(Change("ui-safe-area-missing", $"{canvasName}/{panelName}"));
                }

                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    var label = button.transform.Find("Label") as RectTransform;
                    if (label == null || label.GetComponent<Image>()?.sprite == null)
                    {
                        issues.Add(Change("ui-button-label-missing", PrometheusSceneQuery.Path(button.gameObject)));
                        continue;
                    }
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(button.transform, label);
                    if (Mathf.Abs(bounds.center.x) > 0.1f || Mathf.Abs(bounds.center.y) > 0.1f)
                        issues.Add(Change("ui-button-label-off-center", PrometheusSceneQuery.Path(button.gameObject)));
                }

                foreach (var text in canvas.GetComponentsInChildren<Text>(true))
                    if (text.fontStyle != FontStyle.Bold)
                        issues.Add(Change("ui-text-not-bold", PrometheusSceneQuery.Path(text.gameObject)));

                foreach (var slider in canvas.GetComponentsInChildren<Slider>(true))
                {
                    var presenter = slider.GetComponent<ThemedVolumeSliderPresenter>();
                    var track = slider.transform.Find("Track")?.GetComponent<Image>();
                    var fill = slider.transform.Find("EnergyFill")?.GetComponent<Image>();
                    var handle = slider.transform.Find("Handle")?.GetComponent<Image>();
                    if (presenter == null || presenter.EnergyFill != fill || track?.sprite == null ||
                        fill?.sprite == null || handle?.sprite == null || fill.type != Image.Type.Filled ||
                        slider.fillRect != null)
                        issues.Add(Change("ui-volume-slider-theme-invalid", PrometheusSceneQuery.Path(slider.gameObject)));
                }
            }
            return issues;
        }

        public static List<PrometheusAiChange> AuthorResolutionUi(Scene scene, bool dryRun)
        {
            var changes = new List<PrometheusAiChange>
            {
                Change("author-resolution-dropdown", "hierarchy-authored resolution dropdown"),
                Change("author-resolution-confirm", "10 second keep/revert panel")
            };
            if (dryRun) return changes;
            if (scene.name != "TitleScene") throw new InvalidOperationException("Resolution UI belongs to TitleScene.");
            PrometheusTitleSceneAutomation.Apply(scene, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return changes;
        }

        public static List<PrometheusAiChange> ApplySpriteScale(Scene scene, bool dryRun)
        {
            var contracts = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Narthex.Presentation.ArtReplacementContractHost>(true)).ToArray();
            var changes = contracts.Select(contract => Change("store-visual-scale", PrometheusSceneQuery.Path(contract.gameObject))).ToList();
            if (dryRun) return changes;
            foreach (var contract in contracts)
            {
                var serialized = new SerializedObject(contract);
                var visual = serialized.FindProperty("visualRoot").objectReferenceValue as Transform;
                var actor = serialized.FindProperty("actorRoot").objectReferenceValue as Transform;
                if (visual == null || actor == null) continue;
                var targetHeight = actor.GetComponent<HelteBossPatternHost>() != null ||
                                   actor.name.IndexOf("Helte", StringComparison.OrdinalIgnoreCase) >= 0
                    ? 1.35f
                    : 1.2f;
                var renderersProperty = serialized.FindProperty("renderers");
                var renderers = new List<Renderer>();
                for (var index = 0; renderersProperty != null && index < renderersProperty.arraySize; index++)
                    if (renderersProperty.GetArrayElementAtIndex(index).objectReferenceValue is Renderer renderer)
                        renderers.Add(renderer);
                var factor = ScaleVisualToWorldHeight(visual, renderers, targetHeight);
                foreach (var child in actor.GetComponentsInChildren<Transform>(true))
                    if (child != visual && !child.IsChildOf(visual) &&
                        (child.name.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         child.name.IndexOf("EffectsSlot", StringComparison.OrdinalIgnoreCase) >= 0))
                        ScaleTransformRelative(child, factor);
            }
            var theus = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TutorialGuideCompanionHost>(true)).FirstOrDefault();
            if (theus != null)
            {
                var modelSlot = FindChild(theus.transform, "ModelSlot");
                var effectsSlot = FindChild(theus.transform, "EffectsSlot");
                var factor = modelSlot != null
                    ? ScaleVisualToWorldHeight(modelSlot, modelSlot.GetComponentsInChildren<Renderer>(true), 1.2f)
                    : 1f;
                if (effectsSlot != null && !effectsSlot.IsChildOf(modelSlot)) ScaleTransformRelative(effectsSlot, factor);
            }
            NormalizeMeetingNpcScale(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return changes;
        }

        public static List<PrometheusAiChange> ValidateSpriteScale(Scene scene)
        {
            var changes = new List<PrometheusAiChange>();
            foreach (var contract in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Narthex.Presentation.ArtReplacementContractHost>(true)))
                if (!contract.IsFootAligned)
                    changes.Add(Change("foot-alignment-error", PrometheusSceneQuery.Path(contract.gameObject)));
            return changes;
        }

        public static List<PrometheusAiChange> ApplyRetryContract(Scene scene, bool dryRun)
        {
            var restart = FindComponent<TutorialRestartHost>(scene);
            if (restart == null) throw new InvalidOperationException("TutorialRestartHost is missing.");
            var changes = new List<PrometheusAiChange> { Change("author-retry-contract", "10 quest checkpoints + defeat panel + participants") };
            if (dryRun) return changes;

            var serialized = new SerializedObject(restart);
            var checkpoints = serialized.FindProperty("questCheckpoints");
            var participants = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component is ITutorialRetryParticipant)
                .Distinct()
                .ToArray();
            var training = FindComponent<TutorialTrainingPhaseControllerHost>(scene);
            var sections = new List<TutorialRetrySection>();
            for (var index = 0; index < checkpoints.arraySize; index++)
            {
                var item = checkpoints.GetArrayElementAtIndex(index);
                var questId = item.FindPropertyRelative("questId").stringValue;
                var checkpoint = item.FindPropertyRelative("spawnPoint").objectReferenceValue as Transform;
                if (checkpoint == null) continue;
                var section = checkpoint.GetComponent<TutorialRetrySection>() ?? Undo.AddComponent<TutorialRetrySection>(checkpoint.gameObject);
                section.Configure(questId, checkpoint, participants, BuildInitialStates(training, questId));
                sections.Add(section);
            }
            SetArray(serialized.FindProperty("retrySections"), sections.Cast<UnityEngine.Object>().ToArray());
            SetObject(serialized, "questManagerHost", FindComponent<QuestManagerHost>(scene));
            SetObject(serialized, "trainingPhaseController", FindComponent<TutorialTrainingPhaseControllerHost>(scene));
            foreach (var marker in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TutorialTrainingArrivalMarkerHost>(true)))
                if (marker.SignalTargetId == "TRAINING-DOUBLE-JUMP-SUMMIT") marker.gameObject.tag = "Untagged";
            var defeat = EnsureDefeatPanel(scene);
            SetObject(serialized, "defeatCanvasGroup", defeat);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(restart);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return changes;
        }

        private static TutorialRetryObjectState[] BuildInitialStates(
            TutorialTrainingPhaseControllerHost training,
            string questId)
        {
            if (training == null) return Array.Empty<TutorialRetryObjectState>();
            var serialized = new SerializedObject(training);
            var ids = serialized.FindProperty("trainingQuestIds");
            var roots = serialized.FindProperty("phaseContentRoots");
            if (ids == null || roots == null || ids.arraySize != roots.arraySize)
                return Array.Empty<TutorialRetryObjectState>();

            var states = new List<TutorialRetryObjectState>();
            var belongsToTraining = false;
            for (var index = 0; index < ids.arraySize; index++)
                belongsToTraining |= ids.GetArrayElementAtIndex(index).stringValue == questId;
            if (!belongsToTraining) return Array.Empty<TutorialRetryObjectState>();

            for (var index = 0; index < roots.arraySize; index++)
            {
                var root = roots.GetArrayElementAtIndex(index).objectReferenceValue as GameObject;
                if (root == null) continue;
                states.Add(new TutorialRetryObjectState(
                    root,
                    ids.GetArrayElementAtIndex(index).stringValue == questId));
            }
            return states.ToArray();
        }

        private static CanvasGroup EnsureDefeatPanel(Scene scene)
        {
            var existing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CanvasGroup>(true))
                .FirstOrDefault(group => group.name == "DefeatPanel");
            if (existing != null) return existing;
            var canvas = FindComponent<Canvas>(scene) ?? throw new InvalidOperationException("Tutorial canvas missing.");
            var modal = FindChild(canvas.transform, "ModalRoot") ?? EnsureChild(canvas.transform, "ModalRoot").transform;
            var root = new GameObject("DefeatPanel", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            Undo.RegisterCreatedObjectUndo(root, "Author defeat panel");
            root.transform.SetParent(modal, false);
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0.02f, 0.01f, 0.025f, 0.82f);
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        private static void AuthorPauseMenu(Scene scene, Transform safeArea)
        {
            var host = FindComponent<TutorialPauseMenuHost>(scene);
            if (host == null)
            {
                var hostObject = new GameObject("TutorialPauseMenuHost");
                SceneManager.MoveGameObjectToScene(hostObject, scene);
                host = Undo.AddComponent<TutorialPauseMenuHost>(hostObject);
            }
            var serialized = new SerializedObject(host);
            SetObject(serialized, "saveSystemHost", FindComponent<Narthex.Save.SaveSystemHost>(scene));
            SetObject(serialized, "playerInputHost", FindComponent<PlayerInputHost>(scene));
            SetObject(serialized, "questSequenceHost", FindComponent<TutorialQuestSequenceHost>(scene));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            host.RebuildAuthoredPresentation();
        }

        private static void ConfigureCanvas(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>() ?? Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }

        private static void OrganizeUiRoots(Transform canvas)
        {
            var safeArea = EnsureChild(canvas, "SafeAreaRoot").transform;
            var roots = new Dictionary<string, Transform>();
            foreach (var rootName in UiRoots.Skip(1)) roots[rootName] = EnsureChild(safeArea, rootName).transform;

            var directChildren = canvas.Cast<Transform>().Where(child => child != safeArea).ToArray();
            foreach (var child in directChildren)
            {
                if (child.name is "Background" or "Vignette" || child.name.StartsWith("CloudLayer_", StringComparison.Ordinal))
                    continue;
                var destination = child.name switch
                {
                    "IntroPrompt" or "MainMenu" or "TitleLogoFrame" or "Prome" or "Zenith" => roots["PrimaryHudRoot"],
                    "Settings" or "ResolutionConfirmPanel" or "PauseRoot" or "PauseSettings" or "DefeatPanel" => roots["ModalRoot"],
                    "LoadingScreen" or "FadeOverlay" => roots["TransitionRoot"],
                    _ when child.name.IndexOf("Dialogue", StringComparison.OrdinalIgnoreCase) >= 0 => roots["DialogueRoot"],
                    _ => roots["PrimaryHudRoot"]
                };
                Undo.SetTransformParent(child, destination, "Organize authored UI hierarchy");
            }
        }

        private static float ScaleVisualToWorldHeight(
            Transform visual,
            IEnumerable<Renderer> renderers,
            float targetHeight)
        {
            if (visual == null) return 1f;
            var valid = renderers?.Where(renderer => renderer != null).ToArray() ?? Array.Empty<Renderer>();
            if (valid.Length == 0) return 1f;
            var bounds = valid[0].bounds;
            for (var index = 1; index < valid.Length; index++) bounds.Encapsulate(valid[index].bounds);
            if (bounds.size.y <= 0.001f) return 1f;
            var factor = targetHeight / bounds.size.y;
            if (Mathf.Abs(factor - 1f) <= 0.005f) return 1f;
            Undo.RecordObject(visual, "Apply authored visual scale");
            visual.localScale = Vector3.Scale(visual.localScale, new Vector3(factor, factor, 1f));
            var marker = visual.gameObject.GetComponent<AuthoredVisualScaleMarker>() ??
                         Undo.AddComponent<AuthoredVisualScaleMarker>(visual.gameObject);
            marker.Factor *= factor;
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(visual);
            return factor;
        }

        private static void ScaleTransformRelative(Transform visual, float factor)
        {
            if (visual == null || Mathf.Abs(factor - 1f) <= 0.005f) return;
            Undo.RecordObject(visual, "Scale authored effect with character");
            visual.localScale = Vector3.Scale(visual.localScale, new Vector3(factor, factor, 1f));
            var marker = visual.gameObject.GetComponent<AuthoredVisualScaleMarker>() ??
                         Undo.AddComponent<AuthoredVisualScaleMarker>(visual.gameObject);
            marker.Factor *= factor;
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(visual);
        }

        private static void NormalizeMeetingNpcScale(Scene scene)
        {
            var names = new HashSet<string>(new[] { "ART_SLOT_Eon", "ART_SLOT_Aron", "ART_SLOT_Elium" },
                StringComparer.Ordinal);
            foreach (var target in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                         .Where(item => names.Contains(item.name)))
            {
                var renderers = target.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var oldBounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) oldBounds.Encapsulate(renderer.bounds);
                var floor = oldBounds.min.y;
                ScaleVisualToWorldHeight(target, renderers, 1.2f);
                var newBounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) newBounds.Encapsulate(renderer.bounds);
                Undo.RecordObject(target, "Align authored NPC feet");
                target.position += Vector3.up * (floor - newBounds.min.y);
                target.position = new Vector3(target.position.x, target.position.y, -0.5f);
                EditorUtility.SetDirty(target);
            }
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            var existing = FindChild(parent, name);
            if (existing != null) return existing.gameObject;
            var root = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Author UI root");
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());
            return root;
        }

        private static Transform FindChild(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);

        private static T FindComponent<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();

        private static void SetObject(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(name);
            if (property != null) property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            if (property == null) return;
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static PrometheusAiChange Change(string action, string after) => new()
        {
            action = action,
            hierarchyPath = after,
            before = "missing or legacy runtime dependency",
            after = after
        };
    }
}
