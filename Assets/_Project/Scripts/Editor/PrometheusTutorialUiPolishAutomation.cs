using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.Tools
{
    public static class PrometheusTutorialUiPolishAutomation
    {
        private const float DoubleJumpLandingSurfaceNormalizedY = 0.51f;
        private const string SupportedScene = "Assets/Scenes/TutorialScene.unity";
        private const string HudPath = "TutorialRuntimeRoot/TutorialHUD";
        private const string DialogueSpritePath = "Assets/_Project/Art/UI/Tutorial/Generated_v3/TUTO_UI_DialogueFrame_WideTall_v4.png";
        private const string CompactStripSpritePath = "Assets/_Project/Art/UI/Tutorial/Generated_v4/TUTO_UI_CompactStrip_v4.png";
        private const string InformationCardSpritePath = "Assets/_Project/Art/UI/Tutorial/Generated_v4/TUTO_UI_InformationCard_v4.png";
        private const string BarTrackSpritePath = "Assets/_Project/Art/UI/Tutorial/Generated_v4/TUTO_UI_BarTrack_v4.png";
        private const string RangedIconSpritePath = "Assets/_Project/Art/UI/Tutorial/Generated_v2/TUTO_UI_RangedSkillIcon_v2.png";
        private const string TrainingDummySpritePath = "Assets/_Project/Art/AIConcepts/TutorialTrainingProps/Generated/TUTO_D_TrainingDummy_v2.png";
        private const string BodyFontPath = "Assets/_Project/Art/Fonts/GoogleFonts/GowunDodum-Regular.ttf";
        private const string HeadingFontPath = "Assets/_Project/Art/Fonts/GoogleFonts/DoHyeon-Regular.ttf";

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (!string.Equals(scene.path, SupportedScene, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"UI polish only supports {SupportedScene}.");
            if (!dryRun && EditorApplication.isPlaying)
                throw new InvalidOperationException("Tutorial UI polish requires Edit Mode.");

            var hud = PrometheusSceneQuery.Resolve(scene, string.Empty, HudPath, string.Empty);
            if (hud == null) throw new InvalidOperationException($"Tutorial HUD was not found: {HudPath}");

            var changes = DescribeChanges(hud);
            if (dryRun) return changes;

            var dialogueSprite = ImportSprite(DialogueSpritePath, Vector4.zero);
            var compactStripSprite = ImportSprite(CompactStripSpritePath, new Vector4(96f, 72f, 96f, 72f));
            var informationCardSprite = ImportSprite(InformationCardSpritePath, new Vector4(88f, 88f, 88f, 88f));
            var barTrackSprite = ImportSprite(BarTrackSpritePath, new Vector4(36f, 18f, 36f, 18f));
            var rangedIconSprite = ImportSprite(RangedIconSpritePath, Vector4.zero);
            ConfigurePurposeBuiltHudSprites(hud, compactStripSprite, informationCardSprite, barTrackSprite);
            ConfigureTutorialProgressHud(hud, compactStripSprite);

            foreach (var button in hud.GetComponentsInChildren<Button>(true))
            {
                var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
                if (image == null) continue;
                Undo.RecordObject(image, "Apply tutorial button sprite");
                image.sprite = compactStripSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                image.raycastTarget = true;
                EditorUtility.SetDirty(image);
                ConfigureTextPadding(button.gameObject, 16f, 6f, false);
            }

            ConfigureFixedDialogueWindow(scene, hud, dialogueSprite, informationCardSprite);
            ConfigureRangedTargets(scene);
            ConfigureDoubleJumpPlatforms(scene);
            ConfigureRangedCooldownHud(hud, compactStripSprite, rangedIconSprite);
            ConfigureSkillUnlockFlow(scene, hud);
            ConfigureTypography(hud);

            foreach (var text in hud.GetComponentsInChildren<Text>(true))
            {
                var outline = text.GetComponent<Outline>();
                if (outline == null) outline = Undo.AddComponent<Outline>(text.gameObject);
                Undo.RecordObject(outline, "Improve tutorial text readability");
                outline.effectColor = new Color(0f, 0f, 0f, 0.86f);
                outline.effectDistance = new Vector2(1.25f, -1.25f);
                outline.useGraphicAlpha = true;
                EditorUtility.SetDirty(outline);
            }

            SetMinimumFontSize(hud, "DialogueText", 46);
            SetMinimumFontSize(hud, "StageText", 30);
            SetMinimumFontSize(hud, "ContinueText", 28);
            SetMinimumFontSize(hud, "TutorialProgressText", 34);
            SetMinimumFontSize(hud, "TutorialAmountText", 30);
            SetMinimumFontSize(hud, "TutorialStatusText", 40);
            SetMinimumFontSize(hud, "TutorialKeyPromptText", 30);
            SetMinimumFontSize(hud, "TutorialStageCaptionText", 30);
            SetMinimumFontSize(hud, "PromptText", 30);
            SetMinimumFontSize(hud, "SubtitleText", 30);
            SetMinimumFontSize(hud, "PlayerHealthText", 28);
            SetMinimumFontSize(hud, "EnemyHealthText", 28);
            SetMinimumFontSize(hud, "BossCombatCueText", 28);
            SetMinimumFontSize(hud, "BossHealthValueText", 26);

            var presenter = hud.GetComponentInChildren<TutorialStatusPresenter>(true);
            if (presenter != null)
            {
                Undo.RecordObject(presenter, "Hide tutorial developer IDs");
                var serialized = new SerializedObject(presenter);
                var progressFormat = serialized.FindProperty("progressFormat");
                if (progressFormat != null) progressFormat.stringValue = "튜토리얼 {0} / {1}";
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(presenter);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        public static List<PrometheusAiChange> AlignDoubleJumpPlatforms(Scene scene, bool dryRun)
        {
            if (!string.Equals(scene.path, SupportedScene, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Double-jump alignment only supports {SupportedScene}.");
            if (!dryRun && EditorApplication.isPlaying)
                throw new InvalidOperationException("Double-jump alignment requires Edit Mode.");

            var phase = FindDoubleJumpPhase(scene);
            if (phase == null)
                throw new InvalidOperationException("Sequential double-jump training phase is missing.");
            var changes = new List<PrometheusAiChange>
            {
                Change(phase.gameObject, "align-double-jump-deck-surface",
                    "collider follows the decorative ring top",
                    "collider follows the visible platform deck")
            };
            if (dryRun) return changes;

            ConfigureDoubleJumpPlatforms(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        private static List<PrometheusAiChange> DescribeChanges(GameObject hud)
        {
            return new List<PrometheusAiChange>
            {
                Change(hud, "assign-purpose-built-hud-sprites", "one ornate square frame reused everywhere", "compact strip + information card + bar track + dialogue frame"),
                Change(hud, "increase-dialogue-contrast", "floating text", "92% dark panel + outlined text"),
                Change(hud, "use-wide-dialogue-frame", "square frame stretched into a short strip", DialogueSpritePath),
                Change(hud, "place-dialogue-space-prompt", "continue text hidden inside the bottom border", "visible bottom-center SPACE dialogue prompt"),
                Change(hud, "increase-tutorial-hud-contrast", "mixed transparent UI", "readable tinted sprite panels"),
                Change(hud, "rebuild-tutorial-progress-hud", "progress, objective, and key guide compressed together", "separate progress row + wrapped objective + key guide"),
                Change(hud, "add-lore-dismiss-prompt", "world lore closes only by timeout", "SPACE dismiss prompt appears after one second"),
                Change(hud, "hide-developer-progress-labels", "TUTO_* visible", "player-facing progress only"),
                Change(hud, "repair-ranged-training-targets", "targets behind ranged start marker", "three targets aligned ahead of the player"),
                Change(hud, "align-double-jump-collision", "visual top and collision top differ", "three platform colliders follow sprite top surfaces"),
                Change(hud, "add-ranged-cooldown-hud", "no ranged skill feedback", "bottom-left icon + radial cooldown + seconds"),
                Change(hud, "gate-tutorial-skills", "ranged attack available from tutorial start", "key 2 unlocks at ranged lesson; key 3 announced at Helte entry"),
                Change(hud, "apply-themed-korean-fonts", "mixed legacy fonts", "Gowun Dodum body + Do Hyeon headings")
            };
        }

        private static PrometheusAiChange Change(GameObject hud, string action, string before, string after) => new()
        {
            action = action,
            objectId = PrometheusSceneQuery.ObjectId(hud),
            hierarchyPath = PrometheusSceneQuery.Path(hud),
            before = before,
            after = after
        };

        private static Sprite ImportSprite(string path, Vector4 border)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException($"UI sprite could not be imported: {path}");
            return sprite;
        }

        private static void ConfigurePurposeBuiltHudSprites(
            GameObject hud,
            Sprite compactStripSprite,
            Sprite informationCardSprite,
            Sprite barTrackSprite)
        {
            foreach (var name in new[]
                     {
                         "TutorialObjectivePanel",
                         "TutorialInteractionPromptPanel",
                         "TutorialLoreSubtitlePanel",
                         "BossHealthBarPanel",
                         "HiddenRoomGlideInstruction",
                         "InventoryOpenButton",
                         "SpaceKey_ART_SLOT"
                     })
            {
                ApplyHudSprite(FindByName<Image>(hud, name), compactStripSprite, Color.white);
            }

            foreach (var name in new[] { "ModuleTreePanel", "InventoryPanel", "TutorialIntroductionCard" })
            {
                ApplyHudSprite(FindByName<Image>(hud, name), informationCardSprite, Color.white);
            }

            var resultOverlay = FindByName<Image>(hud, "TutorialResultOverlay");
            if (resultOverlay != null)
            {
                Undo.RecordObject(resultOverlay, "Use quiet tutorial result backdrop");
                resultOverlay.sprite = null;
                resultOverlay.type = Image.Type.Simple;
                resultOverlay.color = new Color(0.005f, 0.012f, 0.02f, 0.86f);
                resultOverlay.raycastTarget = true;
                EditorUtility.SetDirty(resultOverlay);
            }

            ApplyHudSprite(FindByName<Image>(hud, "BossHealthBarTrack"), barTrackSprite, Color.white);
            ApplyHudSprite(FindByName<Image>(hud, "PhaseDivider_ART_SLOT"), barTrackSprite, Color.white);

            var bossFill = FindByName<Image>(hud, "BossHealthBarFill_ART_SLOT");
            if (bossFill != null)
            {
                Undo.RecordObject(bossFill, "Use clear boss health fill");
                bossFill.sprite = null;
                bossFill.type = Image.Type.Filled;
                bossFill.color = new Color(0.92f, 0.18f, 0.12f, 1f);
                EditorUtility.SetDirty(bossFill);
            }

            ConfigureSolidAccent(hud, "TutorialObjectiveDivider");
            ConfigureSolidAccent(hud, "AccentBar");
            ConfigureTextPadding(FindByName<Image>(hud, "TutorialInteractionPromptPanel")?.gameObject, 30f, 7f, false);
            ConfigureLoreSubtitleHud(hud, compactStripSprite);
            ConfigureTextPadding(FindByName<Image>(hud, "HiddenRoomGlideInstruction")?.gameObject, 30f, 10f, true);
        }

        private static void ConfigureLoreSubtitleHud(GameObject hud, Sprite compactStripSprite)
        {
            var panel = FindByName<Image>(hud, "TutorialLoreSubtitlePanel");
            var subtitle = panel != null ? FindByName<Text>(panel.gameObject, "SubtitleText") : null;
            var presenter = panel != null ? panel.GetComponent<TutorialLoreSubtitlePresenter>() : null;
            if (panel == null || subtitle == null || presenter == null)
                throw new InvalidOperationException("Lore subtitle HUD requires its panel, subtitle text, and presenter.");

            ApplyHudSprite(panel, compactStripSprite, Color.white);
            Undo.RecordObject(panel.rectTransform, "Lay out lore subtitle HUD");
            panel.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            panel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            panel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            panel.rectTransform.anchoredPosition = new Vector2(0f, 220f);
            panel.rectTransform.sizeDelta = new Vector2(1080f, 120f);

            Undo.RecordObject(subtitle, "Reserve lore subtitle safe area");
            Undo.RecordObject(subtitle.rectTransform, "Reserve lore subtitle safe area");
            subtitle.rectTransform.anchorMin = Vector2.zero;
            subtitle.rectTransform.anchorMax = Vector2.one;
            subtitle.rectTransform.offsetMin = new Vector2(44f, 38f);
            subtitle.rectTransform.offsetMax = new Vector2(-44f, -10f);
            subtitle.fontSize = 30;
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.resizeTextForBestFit = false;
            subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitle.verticalOverflow = VerticalWrapMode.Truncate;
            subtitle.raycastTarget = false;

            var prompt = EnsureText(panel.transform, "LoreDismissPromptText", "SPACE  ·  눌러서 닫기", 24, TextAnchor.MiddleCenter);
            Undo.RecordObject(prompt.rectTransform, "Lay out lore dismiss prompt");
            prompt.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            prompt.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            prompt.rectTransform.pivot = new Vector2(0.5f, 0f);
            prompt.rectTransform.anchoredPosition = new Vector2(0f, 9f);
            prompt.rectTransform.sizeDelta = new Vector2(900f, 28f);
            prompt.fontSize = 24;
            prompt.color = new Color(0.42f, 0.96f, 0.9f, 1f);
            prompt.resizeTextForBestFit = false;
            prompt.horizontalOverflow = HorizontalWrapMode.Overflow;
            prompt.verticalOverflow = VerticalWrapMode.Overflow;
            prompt.enabled = false;

            Undo.RecordObject(presenter, "Connect lore dismiss prompt");
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("dismissPromptText").objectReferenceValue = prompt;
            serialized.FindProperty("minimumDismissDelay").floatValue = 1f;
            serialized.FindProperty("dismissPrompt").stringValue = "SPACE  ·  눌러서 닫기";
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(presenter);
        }

        private static void ConfigureTutorialProgressHud(GameObject hud, Sprite compactStripSprite)
        {
            var panel = FindByName<Image>(hud, "TutorialObjectivePanel");
            var status = FindByName<Text>(hud, "TutorialStatusText");
            var keyPrompt = FindByName<Text>(hud, "TutorialKeyPromptText");
            if (panel == null || status == null || keyPrompt == null)
                throw new InvalidOperationException("Tutorial progress HUD requires its panel, objective text, and key prompt.");

            ApplyHudSprite(panel, compactStripSprite, Color.white);
            ConfigureTopAnchoredRect(panel.rectTransform, Vector2.zero, new Vector2(1060f, 220f), new Vector2(0.5f, 1f));
            panel.rectTransform.anchoredPosition = new Vector2(0f, -8f);

            var progress = EnsureText(hud.transform, "TutorialProgressText", "튜토리얼 1 / 10", 34, TextAnchor.MiddleLeft);
            var amount = EnsureText(hud.transform, "TutorialAmountText", string.Empty, 30, TextAnchor.MiddleRight);

            ConfigureTopAnchoredRect(progress.rectTransform, new Vector2(-454f, -34f), new Vector2(430f, 36f), new Vector2(0f, 1f));
            ConfigureTopAnchoredRect(amount.rectTransform, new Vector2(454f, -34f), new Vector2(430f, 36f), new Vector2(1f, 1f));
            ConfigureTopAnchoredRect(status.rectTransform, new Vector2(0f, -72f), new Vector2(940f, 82f), new Vector2(0.5f, 1f));
            ConfigureTopAnchoredRect(keyPrompt.rectTransform, new Vector2(0f, -174f), new Vector2(920f, 36f), new Vector2(0.5f, 1f));

            ConfigureProgressText(progress, 34, TextAnchor.MiddleLeft, new Color(0.35f, 0.96f, 0.9f, 1f), false);
            ConfigureProgressText(amount, 30, TextAnchor.MiddleRight, new Color(0.72f, 0.88f, 0.9f, 1f), false);
            ConfigureProgressText(status, 40, TextAnchor.MiddleCenter, new Color(0.96f, 0.98f, 1f, 1f), true);
            ConfigureProgressText(keyPrompt, 30, TextAnchor.MiddleCenter, new Color(0.4f, 0.95f, 0.88f, 1f), false);

            var divider = FindByName<Image>(hud, "TutorialObjectiveDivider");
            if (divider != null)
            {
                ConfigureTopAnchoredRect(divider.rectTransform, new Vector2(0f, -164f), new Vector2(880f, 2f), new Vector2(0.5f, 1f));
                ConfigureSolidAccent(hud, "TutorialObjectiveDivider");
            }

            var presenter = hud.GetComponentInChildren<TutorialStatusPresenter>(true);
            if (presenter == null) throw new InvalidOperationException("TutorialStatusPresenter is missing.");
            Undo.RecordObject(presenter, "Connect readable tutorial progress HUD");
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("progressText").objectReferenceValue = progress;
            serialized.FindProperty("amountText").objectReferenceValue = amount;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(presenter);
        }

        private static void ConfigureTopAnchoredRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 pivot)
        {
            Undo.RecordObject(rect, "Lay out tutorial progress HUD");
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            EditorUtility.SetDirty(rect);
        }

        private static void ConfigureProgressText(
            Text text,
            int fontSize,
            TextAnchor alignment,
            Color color,
            bool wrap)
        {
            Undo.RecordObject(text, "Polish tutorial progress text");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = wrap ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            text.lineSpacing = 1.05f;
            text.raycastTarget = false;
            EditorUtility.SetDirty(text);
        }

        private static void ApplyHudSprite(Image image, Sprite sprite, Color color)
        {
            if (image == null) return;
            Undo.RecordObject(image, "Assign purpose-built tutorial HUD sprite");
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            image.color = color;
            image.raycastTarget = image.GetComponent<Button>() != null;
            EditorUtility.SetDirty(image);
        }

        private static void ConfigureSolidAccent(GameObject hud, string name)
        {
            var image = FindByName<Image>(hud, name);
            if (image == null) return;
            Undo.RecordObject(image, "Simplify tutorial HUD accent");
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = new Color(0.24f, 0.88f, 0.84f, 0.9f);
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
        }

        private static void ConfigureTextPadding(GameObject root, float horizontal, float vertical, bool wrap)
        {
            if (root == null) return;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                Undo.RecordObject(text, "Keep HUD text inside its frame");
                Undo.RecordObject(text.rectTransform, "Keep HUD text inside its frame");
                text.rectTransform.anchorMin = Vector2.zero;
                text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(horizontal, vertical);
                text.rectTransform.offsetMax = new Vector2(-horizontal, -vertical);
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.alignment = TextAnchor.MiddleCenter;
                EditorUtility.SetDirty(text);
                EditorUtility.SetDirty(text.rectTransform);
            }
        }

        private static void ConfigureRangedTargets(Scene scene)
        {
            var start = FindSceneTransform(scene, "훈련_원거리_시작");
            if (start == null) throw new InvalidOperationException("Ranged training start marker is missing.");
            var flow = FindSceneComponent<TutorialImportedTrainingFlowHost>(scene);
            if (flow == null) throw new InvalidOperationException("Training flow manager is missing.");
            var dummySprite = AssetDatabase.LoadAssetAtPath<Sprite>(TrainingDummySpritePath);
            if (dummySprite == null) throw new InvalidOperationException("Training dummy sprite is missing: " + TrainingDummySpritePath);

            var offsets = new[] { 1.5f, 3.2f, 4.8f };
            var targetObjects = new GameObject[offsets.Length];
            var targetRenderers = new SpriteRenderer[offsets.Length];
            for (var index = 0; index < offsets.Length; index++)
            {
                var suffix = (index + 1).ToString("00");
                var marker = FindSceneTransform(scene, "훈련_원거리_" + suffix);
                var target = FindSceneTransform(scene, "RangedTarget_" + suffix);
                if (marker == null || target == null)
                    throw new InvalidOperationException("Ranged training target or marker is missing: " + suffix);

                var position = new Vector3(start.position.x + offsets[index], start.position.y - 0.1f, 0f);
                Undo.RecordObject(marker, "Align ranged training marker");
                Undo.RecordObject(target, "Align ranged training dummy");
                marker.position = position;
                target.position = position;
                EditorUtility.SetDirty(marker);
                EditorUtility.SetDirty(target);

                foreach (var collider in target.GetComponentsInChildren<Collider2D>(true))
                {
                    Undo.RecordObject(collider, "Enable ranged training dummy hitbox");
                    collider.enabled = true;
                    collider.isTrigger = true;
                    EditorUtility.SetDirty(collider);
                }

                foreach (var oldRenderer in target.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    Undo.RecordObject(oldRenderer, "Hide obsolete ranged target placeholder");
                    oldRenderer.enabled = false;
                    EditorUtility.SetDirty(oldRenderer);
                }

                var visual = target.Find("TrainingDummyVisual_ART_EDITABLE");
                if (visual == null)
                {
                    var visualObject = new GameObject("TrainingDummyVisual_ART_EDITABLE", typeof(SpriteRenderer));
                    Undo.RegisterCreatedObjectUndo(visualObject, "Create ranged training dummy visual");
                    visualObject.transform.SetParent(target, false);
                    visual = visualObject.transform;
                }
                var renderer = visual.GetComponent<SpriteRenderer>();
                Undo.RecordObject(renderer, "Configure ranged training dummy visual");
                Undo.RecordObject(visual, "Fit ranged training dummy visual");
                renderer.sprite = dummySprite;
                renderer.color = Color.white;
                renderer.sortingOrder = 12;
                renderer.enabled = false;
                var scale = 1.9f / Mathf.Max(0.01f, dummySprite.bounds.size.y);
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = new Vector3(scale, scale, 1f);
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(visual);

                targetObjects[index] = target.gameObject;
                targetRenderers[index] = renderer;
            }

            Undo.RecordObject(flow, "Reconnect ranged training targets");
            var serialized = new SerializedObject(flow);
            var targetsProperty = serialized.FindProperty("rangedTargets");
            var renderersProperty = serialized.FindProperty("rangedTargetRenderers");
            targetsProperty.arraySize = targetObjects.Length;
            renderersProperty.arraySize = targetRenderers.Length;
            for (var index = 0; index < targetObjects.Length; index++)
            {
                targetsProperty.GetArrayElementAtIndex(index).objectReferenceValue = targetObjects[index];
                renderersProperty.GetArrayElementAtIndex(index).objectReferenceValue = targetRenderers[index];
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(flow);
            Physics2D.SyncTransforms();
        }

        private static void ConfigureFixedDialogueWindow(
            Scene scene,
            GameObject hud,
            Sprite dialogueSprite,
            Sprite informationCardSprite)
        {
            var panel = FindByName<Image>(hud, "TutorialDialoguePanel");
            var dialogue = FindByName<Text>(hud, "DialogueText");
            if (panel == null || dialogue == null)
                throw new InvalidOperationException("Tutorial dialogue panel or dialogue text is missing.");

            Undo.RecordObject(panel, "Use undistorted wide dialogue frame");
            panel.sprite = dialogueSprite;
            panel.type = Image.Type.Simple;
            panel.preserveAspect = false;
            panel.color = Color.white;
            EditorUtility.SetDirty(panel);

            Undo.RecordObject(panel.rectTransform, "Keep fixed tutorial dialogue window");
            panel.rectTransform.sizeDelta = new Vector2(1760f, 660f);
            panel.rectTransform.anchoredPosition = new Vector2(0f, 24f);

            var interactionPrompt = FindByName<Image>(hud, "TutorialInteractionPromptPanel");
            if (interactionPrompt != null)
            {
                Undo.RecordObject(interactionPrompt.rectTransform, "Keep interaction prompt above dialogue window");
                interactionPrompt.rectTransform.anchoredPosition = new Vector2(0f, 735f);
                EditorUtility.SetDirty(interactionPrompt.rectTransform);
            }

            Undo.RecordObject(dialogue, "Keep dialogue text inside fixed window");
            Undo.RecordObject(dialogue.rectTransform, "Keep dialogue text inside fixed window");
            dialogue.rectTransform.anchorMin = new Vector2(0.22f, 0.16f);
            dialogue.rectTransform.anchorMax = new Vector2(0.78f, 0.80f);
            dialogue.rectTransform.offsetMin = new Vector2(12f, 8f);
            dialogue.rectTransform.offsetMax = new Vector2(-12f, -8f);
            dialogue.rectTransform.anchoredPosition = Vector2.zero;
            dialogue.resizeTextForBestFit = false;
            dialogue.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogue.verticalOverflow = VerticalWrapMode.Overflow;
            EditorUtility.SetDirty(panel.rectTransform);
            EditorUtility.SetDirty(dialogue);
            EditorUtility.SetDirty(dialogue.rectTransform);

            var continueText = panel.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(item => item.name == "ContinueText");
            if (continueText == null) throw new InvalidOperationException("Dialogue continue text is missing.");
            Undo.RecordObject(continueText, "Show dialogue SPACE prompt");
            Undo.RecordObject(continueText.rectTransform, "Place dialogue SPACE prompt");
            continueText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            continueText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            continueText.rectTransform.pivot = new Vector2(0.5f, 0f);
            continueText.rectTransform.anchoredPosition = new Vector2(0f, 52f);
            continueText.rectTransform.sizeDelta = new Vector2(760f, 42f);
            continueText.fontSize = 30;
            continueText.alignment = TextAnchor.MiddleCenter;
            continueText.color = new Color(0.42f, 0.96f, 0.9f, 1f);
            continueText.resizeTextForBestFit = false;
            continueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            continueText.verticalOverflow = VerticalWrapMode.Overflow;
            continueText.raycastTarget = false;
            EditorUtility.SetDirty(continueText);
            EditorUtility.SetDirty(continueText.rectTransform);

            var presenter = FindSceneComponent<TutorialDialoguePresenter>(scene);
            if (presenter == null) throw new InvalidOperationException("TutorialDialoguePresenter is missing.");
            Undo.RecordObject(presenter, "Use readable dialogue SPACE prompts");
            var presenterSerialized = new SerializedObject(presenter);
            presenterSerialized.FindProperty("continuePrompt").stringValue = "SPACE · 대화 진행";
            presenterSerialized.FindProperty("closePrompt").stringValue = "SPACE · 대화 닫기";
            presenterSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(presenter);

            ConfigureSpeakerProfile(hud, "DialogueSpeakerLeft", true, informationCardSprite);
            ConfigureSpeakerProfile(hud, "DialogueSpeakerRight", false, informationCardSprite);

            var fitter = panel.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                Undo.RecordObject(fitter, "Disable dialogue auto sizing");
                fitter.enabled = false;
                EditorUtility.SetDirty(fitter);
            }
        }

        private static void ConfigureSpeakerProfile(GameObject hud, string name, bool left, Sprite informationCardSprite)
        {
            var image = FindByName<Image>(hud, name);
            if (image == null) throw new InvalidOperationException("Dialogue speaker profile is missing: " + name);
            ApplyHudSprite(image, informationCardSprite, Color.white);
            var rect = image.rectTransform;
            Undo.RecordObject(rect, "Enlarge dialogue speaker profile");
            rect.anchorMin = new Vector2(left ? 0.025f : 0.78f, 0.12f);
            rect.anchorMax = new Vector2(left ? 0.22f : 0.975f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var portrait = image.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(item => item.name == "Portrait_ART_SLOT");
            if (portrait != null)
            {
                Undo.RecordObject(portrait.rectTransform, "Enlarge dialogue portrait");
                portrait.rectTransform.anchorMin = new Vector2(0.5f, 0.45f);
                portrait.rectTransform.anchorMax = new Vector2(0.5f, 0.45f);
                portrait.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                portrait.rectTransform.anchoredPosition = Vector2.zero;
                portrait.rectTransform.sizeDelta = new Vector2(280f, 280f);
                portrait.preserveAspect = true;
            }

            var nameText = image.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(item => item.name == "NameText");
            if (nameText != null)
            {
                Undo.RecordObject(nameText.rectTransform, "Place dialogue speaker name");
                nameText.rectTransform.anchorMin = new Vector2(0.08f, 0.78f);
                nameText.rectTransform.anchorMax = new Vector2(0.92f, 0.98f);
                nameText.rectTransform.offsetMin = Vector2.zero;
                nameText.rectTransform.offsetMax = Vector2.zero;
                nameText.alignment = TextAnchor.MiddleCenter;
                nameText.fontSize = Mathf.Max(nameText.fontSize, 25);
            }
        }

        private static void ConfigureRangedCooldownHud(GameObject hud, Sprite buttonSprite, Sprite iconSprite)
        {
            var rangedAttack = FindSceneComponent<PlayerRangedAttackHost>(hud.scene);
            if (rangedAttack == null) throw new InvalidOperationException("Player ranged attack host is missing.");

            var root = EnsureUiObject(hud.transform, "RangedAttackCooldownHUD");
            var rootRect = root.GetComponent<RectTransform>();
            Undo.RecordObject(rootRect, "Place ranged cooldown HUD");
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.zero;
            rootRect.pivot = Vector2.zero;
            rootRect.anchoredPosition = new Vector2(42f, 42f);
            rootRect.sizeDelta = new Vector2(112f, 112f);

            var baseImage = root.GetComponent<Image>();
            Undo.RecordObject(baseImage, "Configure ranged cooldown icon");
            baseImage.sprite = iconSprite;
            baseImage.type = Image.Type.Simple;
            baseImage.preserveAspect = true;
            baseImage.color = Color.white;
            baseImage.raycastTarget = false;

            var canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = Undo.AddComponent<CanvasGroup>(root);
            Undo.RecordObject(canvasGroup, "Configure ranged cooldown visibility");
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var overlayObject = EnsureUiObject(root.transform, "CooldownRadialOverlay");
            Stretch(overlayObject.GetComponent<RectTransform>());
            var overlay = overlayObject.GetComponent<Image>();
            Undo.RecordObject(overlay, "Configure ranged cooldown radial overlay");
            overlay.sprite = iconSprite;
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = false;
            overlay.fillAmount = 0f;
            overlay.color = new Color(0.02f, 0.03f, 0.04f, 0.76f);
            overlay.raycastTarget = false;

            var cooldownText = EnsureText(root.transform, "CooldownSecondsText", string.Empty, 30, TextAnchor.MiddleCenter);
            Stretch(cooldownText.rectTransform);

            var badge = EnsureUiObject(root.transform, "KeyBadge");
            var badgeRect = badge.GetComponent<RectTransform>();
            Undo.RecordObject(badgeRect, "Place ranged cooldown key badge");
            badgeRect.anchorMin = new Vector2(1f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(1f, 0f);
            badgeRect.anchoredPosition = new Vector2(-1f, 1f);
            badgeRect.sizeDelta = new Vector2(38f, 30f);
            var badgeImage = badge.GetComponent<Image>();
            Undo.RecordObject(badgeImage, "Configure ranged cooldown key badge");
            badgeImage.sprite = buttonSprite;
            badgeImage.type = Image.Type.Sliced;
            badgeImage.color = Color.white;
            badgeImage.raycastTarget = false;
            var keyText = EnsureText(badge.transform, "KeyText", "1", 20, TextAnchor.MiddleCenter);
            Stretch(keyText.rectTransform);

            var presenter = root.GetComponent<RangedAttackCooldownPresenter>();
            if (presenter == null) presenter = Undo.AddComponent<RangedAttackCooldownPresenter>(root);
            Undo.RecordObject(presenter, "Configure ranged cooldown presenter");
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("rangedAttack").objectReferenceValue = rangedAttack;
            serialized.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            serialized.FindProperty("cooldownOverlay").objectReferenceValue = overlay;
            serialized.FindProperty("cooldownText").objectReferenceValue = cooldownText;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(presenter);
        }

        private static void ConfigureSkillUnlockFlow(Scene scene, GameObject hud)
        {
            var rangedAttack = FindSceneComponent<PlayerRangedAttackHost>(scene);
            var theusRangedSupport = FindSceneComponent<TutorialTheusRangedSupportHost>(scene);
            var serviceRoot = FindSceneComponent<Narthex.Core.ServiceRoot>(scene);
            var questSequence = FindSceneComponent<TutorialQuestSequenceHost>(scene);
            var bossArena = FindSceneComponent<TutorialBossArenaHost>(scene);
            var subtitlePresenter = FindSceneComponent<TutorialLoreSubtitlePresenter>(scene);
            if (rangedAttack == null || theusRangedSupport == null || serviceRoot == null ||
                questSequence == null || bossArena == null ||
                subtitlePresenter == null)
                throw new InvalidOperationException("Tutorial skill unlock flow references are incomplete.");

            var rangedSerialized = new SerializedObject(rangedAttack);
            rangedSerialized.FindProperty("startsUnlocked").boolValue = false;
            rangedSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(rangedAttack);

            var supportSerialized = new SerializedObject(theusRangedSupport);
            supportSerialized.FindProperty("startsFocusedVolleyUnlocked").boolValue = false;
            supportSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(theusRangedSupport);

            var host = hud.GetComponent<TutorialSkillUnlockHost>();
            if (host == null) host = Undo.AddComponent<TutorialSkillUnlockHost>(hud);
            Undo.RecordObject(host, "Configure tutorial skill unlock flow");
            var serialized = new SerializedObject(host);
            serialized.FindProperty("serviceRoot").objectReferenceValue = serviceRoot;
            serialized.FindProperty("questSequenceHost").objectReferenceValue = questSequence;
            serialized.FindProperty("rangedAttack").objectReferenceValue = rangedAttack;
            serialized.FindProperty("theusRangedSupport").objectReferenceValue = theusRangedSupport;
            serialized.FindProperty("bossArenaHost").objectReferenceValue = bossArena;
            serialized.FindProperty("subtitlePresenter").objectReferenceValue = subtitlePresenter;
            serialized.FindProperty("rangedUnlockQuestId").stringValue = "QST-TUTO-005";
            serialized.FindProperty("focusedVolleyUnlockQuestId").stringValue = "QST-TUTO-007-A";
            serialized.FindProperty("rangedUnlockMessage").stringValue = "테우스 · 원거리 공격을 사용할 수 있어!  [1]";
            serialized.FindProperty("focusedVolleyUnlockMessage").stringValue =
                "테우스 · 이제 나도 제대로 도울게. 집중포화 해금!  [2]";
            serialized.FindProperty("bossSkillUnlockMessage").stringValue =
                "테우스 · 프로메, 연속 동작을 써 봐. 4연속 참격 해금!  [3]";
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(host);
        }

        private static void ConfigureDoubleJumpPlatforms(Scene scene)
        {
            var phase = FindDoubleJumpPhase(scene);
            if (phase == null) throw new InvalidOperationException("Sequential double-jump training phase is missing.");

            var visuals = phase.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(item => item.name.StartsWith("DoubleJumpPadVisual_", StringComparison.Ordinal))
                .OrderBy(item => item.transform.position.x)
                .ToArray();
            var colliders = phase.GetComponentsInChildren<BoxCollider2D>(true)
                .Where(item => item.GetComponent<TutorialTrainingArrivalMarkerHost>() == null)
                .OrderBy(item => item.transform.position.x)
                .ToArray();
            if (visuals.Length != 3 || colliders.Length != 3)
                throw new InvalidOperationException("Double-jump training requires exactly three visuals and platform colliders.");

            for (var index = 0; index < visuals.Length; index++)
            {
                var visual = visuals[index];
                var collider = colliders[index];
                var landingSurface = ResolveDoubleJumpLandingSurfaceWorldY(visual);
                var scaleY = Mathf.Abs(collider.transform.lossyScale.y);
                Undo.RecordObject(collider, "Align double-jump platform collision surface");
                var size = collider.size;
                size.y = 0.5f / Mathf.Max(0.01f, scaleY);
                collider.size = size;
                var offset = collider.offset;
                offset.y = (landingSurface - collider.transform.position.y) / Mathf.Max(0.01f, scaleY) - size.y * 0.5f;
                collider.offset = offset;
                collider.isTrigger = false;
                collider.enabled = true;
                EditorUtility.SetDirty(collider);
            }

            var summit = FindSceneTransform(scene, "훈련_더블점프_끝");
            var summitCollider = summit != null ? summit.GetComponent<BoxCollider2D>() : null;
            if (summit != null && summitCollider != null)
            {
                var highestTop = visuals.Max(ResolveDoubleJumpLandingSurfaceWorldY);
                var scaleY = Mathf.Abs(summit.lossyScale.y);
                Undo.RecordObject(summit, "Align double-jump summit marker");
                summit.position = new Vector3(
                    visuals.OrderByDescending(visual => visual.transform.position.x).First().transform.position.x,
                    highestTop + summitCollider.size.y * scaleY * 0.5f,
                    summit.position.z);
                EditorUtility.SetDirty(summit);
            }
            Physics2D.SyncTransforms();
        }

        public static float ResolveDoubleJumpLandingSurfaceWorldY(SpriteRenderer visual)
        {
            if (visual == null || visual.sprite == null) return 0f;
            var bounds = visual.sprite.bounds;
            var localY = Mathf.Lerp(bounds.min.y, bounds.max.y, DoubleJumpLandingSurfaceNormalizedY);
            return visual.transform.TransformPoint(new Vector3(0f, localY, 0f)).y;
        }

        private static Transform FindDoubleJumpPhase(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var phase = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item =>
                    item.name == "02_더블점프" && item.parent != null && item.parent.name == "TrainingPhaseContents");
                if (phase != null) return phase;
            }
            return null;
        }

        private static void ConfigureTypography(GameObject hud)
        {
            AssetDatabase.ImportAsset(BodyFontPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(HeadingFontPath, ImportAssetOptions.ForceSynchronousImport);
            var bodyFont = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
            var headingFont = AssetDatabase.LoadAssetAtPath<Font>(HeadingFontPath);
            if (bodyFont == null || headingFont == null)
                throw new InvalidOperationException("Tutorial Korean UI fonts could not be imported.");

            var headingNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "StageText", "NameText", "ContinueText", "TutorialStageCaptionText", "TutorialProgressText", "LoreDismissPromptText", "InventoryTitle",
                "TitleText", "BossNameText", "CooldownSecondsText", "KeyText"
            };
            foreach (var text in hud.GetComponentsInChildren<Text>(true))
            {
                Undo.RecordObject(text, "Apply themed Korean HUD typography");
                text.font = headingNames.Contains(text.name) ? headingFont : bodyFont;
                text.fontStyle = FontStyle.Normal;
                text.lineSpacing = text.name == "DialogueText" ? 1.12f : 1f;
                if (text.name == "DialogueText") text.fontSize = Mathf.Max(text.fontSize, 46);
                if (text.name == "NameText") text.fontSize = Mathf.Max(text.fontSize, 32);
                EditorUtility.SetDirty(text);
            }
        }

        private static GameObject EnsureUiObject(Transform parent, string name)
        {
            var existing = parent.Find(name)?.gameObject;
            if (existing != null) return existing;
            var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(created, "Create tutorial HUD sprite object");
            created.transform.SetParent(parent, false);
            return created;
        }

        private static Text EnsureText(Transform parent, string name, string value, int fontSize, TextAnchor alignment)
        {
            var existing = parent.Find(name)?.GetComponent<Text>();
            var text = existing;
            if (text == null)
            {
                var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                Undo.RegisterCreatedObjectUndo(created, "Create tutorial HUD text");
                created.transform.SetParent(parent, false);
                text = created.GetComponent<Text>();
            }

            Undo.RecordObject(text, "Configure tutorial HUD text");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.font = FindByName<Text>(parent.root.gameObject, "TutorialStatusText")?.font;
            text.raycastTarget = false;
            var outline = text.GetComponent<Outline>();
            if (outline == null) outline = Undo.AddComponent<Outline>(text.gameObject);
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.25f, -1.25f);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            Undo.RecordObject(rect, "Stretch tutorial HUD element");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Transform FindSceneTransform(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
                if (match != null) return match;
            }
            return null;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var match = root.GetComponentInChildren<T>(true);
                if (match != null) return match;
            }
            return null;
        }

        private static T FindByName<T>(GameObject root, string name) where T : Component =>
            root.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);

        private static void SetMinimumFontSize(GameObject hud, string name, int minimum)
        {
            foreach (var text in hud.GetComponentsInChildren<Text>(true)
                         .Where(item => item != null && item.name == name && item.fontSize < minimum))
            {
                Undo.RecordObject(text, "Improve tutorial font size");
                text.fontSize = minimum;
                EditorUtility.SetDirty(text);
            }
        }

    }
}
