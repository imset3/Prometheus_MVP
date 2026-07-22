using System;
using System.Linq;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Presentation;
using Narthex.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Tools
{
    public static class TutorialRequestedFeatureSceneSetup
    {
        [MenuItem("Narthex/Tutorial/Apply Requested Gameplay Features")]
        public static void Apply()
        {
            var sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(candidate => candidate.scene.IsValid())
                .ToArray();

            GameObject Find(string objectName) => sceneObjects.FirstOrDefault(candidate => candidate != null && candidate.name == objectName);

            var player = Require(Find("PlayerRoot"), "PlayerRoot");
            var stageSystems = Require(Find("StageSystems"), "StageSystems");
            var cameraObject = Require(Find("Main Camera"), "Main Camera");
            var attackAnchor = Require(Find("AttackAnchor"), "AttackAnchor");
            var pulseObject = Require(Find("ModulePulseHitbox"), "ModulePulseHitbox");
            var trainingObject = Require(Find("TrainingSpawnController"), "TrainingSpawnController");
            var pickup = Require(Find("CryonBootsPickup"), "CryonBootsPickup");

            var input = player.GetComponent<PlayerInputHost>();
            var melee = player.GetComponent<MeleeAttackHost>();
            var motor = player.GetComponent<PlayerMotorHost>();
            var body = player.GetComponent<Rigidbody2D>();
            SetReference(input, "aimCamera", cameraObject.GetComponent<Camera>());
            SetReference(melee, "attackAnchor", attackAnchor.transform);
            ConfigurePlayerCombat(player, input, melee);

            ConfigureRestart(Find, stageSystems, input, motor);
            ConfigureTraining(Find, stageSystems, trainingObject, player, body, motor);
            ConfigureJumpTraining(Find, stageSystems, trainingObject, player, body, motor);
            ConfigureLadderTransition(Find, stageSystems);
            ConfigurePulse(Find, pulseObject);
            ConfigureDoubleJumpPractice(Find, stageSystems);
            ConfigureDialogueAndText(stageSystems, input);
            ConfigureObjectiveArrow(Find, stageSystems, pickup);
            ConfigureHelteBossStageLayout(Find);
            ConfigureCameraReview(Find, cameraObject, player, body);
            ConfigureBossHealthBar(Find, stageSystems);
            ConfigureCombatReadability(Find);
            ConfigureArtReplacementContracts();
            ConfigureLoreSubtitles(Find, stageSystems, player);
            ConfigureAccessibility(Find, stageSystems, cameraObject);
            ConfigureHudStateCoordinator(Find, stageSystems, Require(Find("TutorialHUD"), "TutorialHUD"));
            TutorialNotionChapter0SceneSetup.Apply();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Applied tutorial gameplay features, including double-jump confirmation and non-blocking Teus lore subtitles.");
        }

        private static void ConfigureCameraReview(
            Func<string, GameObject> find,
            GameObject cameraObject,
            GameObject player,
            Rigidbody2D playerBody)
        {
            var cameraFollow = cameraObject.GetComponent<CameraFollowHost>();
            if (cameraFollow == null) throw new InvalidOperationException("Main Camera is missing CameraFollowHost.");

            SetReference(cameraFollow, "target", player.transform);
            SetReference(cameraFollow, "targetBody", playerBody);
            SetReference(cameraFollow, "controlledCamera", cameraObject.GetComponent<Camera>());
            SetReference(cameraFollow, "bossArenaHost", Require(find("BossArena_Controller"), "BossArena_Controller")
                .GetComponent<TutorialBossArenaHost>());
            SetReference(cameraFollow, "bossFocus", Require(find("TutorialHelte"), "TutorialHelte").transform);
            SetFloat(cameraFollow, "lookAheadDistance", 2f);
            SetFloat(cameraFollow, "verticalFollowSpeed", 8f);
            SetFloat(cameraFollow, "verticalDeadZone", 0.65f);
            SetFloat(cameraFollow, "normalOrthographicSize", 5f);
            SetFloat(cameraFollow, "bossOrthographicSize", 6.25f);
            SetFloat(cameraFollow, "motionIntensity", 0.65f);
        }

        private static void ConfigureCombatReadability(Func<string, GameObject> find)
        {
            foreach (var attackHost in Resources.FindObjectsOfTypeAll<EnemyAttackHost>()
                         .Where(item => item != null && item.gameObject.scene.IsValid()))
            {
                var sourceRenderer = attackHost.GetComponentInChildren<Renderer>(true);
                var warning = CreateOrUpdateCube(
                    attackHost.transform,
                    "AttackWarning_ART_SLOT",
                    attackHost.transform.position + Vector3.up * 1.35f,
                    new Vector3(1.15f, 0.12f, 0.1f),
                    GetOrCreateAttackWarningMaterial(sourceRenderer != null ? sourceRenderer.sharedMaterial : null));
                warning.SetActive(false);
                SetReference(attackHost, "warningVisualSlot", warning);
                SetReference(attackHost, "warningRenderer", warning.GetComponent<Renderer>());
                SetFloat(attackHost, "telegraphSeconds", 0.28f);
            }

            var panel = Require(find("BossHealthBarPanel"), "BossHealthBarPanel");
            var cueTransform = panel.transform.Find("BossCombatCueText");
            var cueObject = cueTransform != null
                ? cueTransform.gameObject
                : new GameObject("BossCombatCueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            cueObject.transform.SetParent(panel.transform, false);
            var cueRect = (RectTransform)cueObject.transform;
            cueRect.anchorMin = new Vector2(0.5f, 1f);
            cueRect.anchorMax = new Vector2(0.5f, 1f);
            cueRect.pivot = new Vector2(0.5f, 0f);
            cueRect.anchoredPosition = new Vector2(0f, 8f);
            cueRect.sizeDelta = new Vector2(620f, 34f);
            var cueText = cueObject.GetComponent<Text>();
            cueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cueText.fontSize = 22;
            cueText.fontStyle = FontStyle.Bold;
            cueText.alignment = TextAnchor.MiddleCenter;
            cueText.text = string.Empty;
            cueObject.SetActive(false);

            var cuePresenter = panel.GetComponent<BossCombatCuePresenter>();
            if (cuePresenter == null) cuePresenter = panel.AddComponent<BossCombatCuePresenter>();
            SetReference(cuePresenter, "arenaHost", Require(find("BossArena_Controller"), "BossArena_Controller")
                .GetComponent<TutorialBossArenaHost>());
            SetReference(cuePresenter, "patternHost", Require(find("TutorialHelte"), "TutorialHelte")
                .GetComponent<HelteBossPatternHost>());
            SetReference(cuePresenter, "cueRoot", cueObject);
            SetReference(cuePresenter, "cueText", cueText);
        }

        private static void ConfigureArtReplacementContracts()
        {
            foreach (var actor in Resources.FindObjectsOfTypeAll<CombatActorHost>()
                         .Where(item => item != null && item.gameObject.scene.IsValid()))
            {
                var bodyCollider = actor.GetComponent<Collider2D>();
                var actorRenderers = actor.GetComponentsInChildren<Renderer>(true)
                    .Where(IsPrimaryArtRenderer)
                    .ToArray();
                var visualRenderer = actorRenderers.FirstOrDefault();
                var attackHitboxes = actor.GetComponentsInChildren<Collider2D>(true)
                    .Where(collider => collider != null && collider != bodyCollider && collider.isTrigger)
                    .ToArray();
                if (bodyCollider == null || visualRenderer == null || attackHitboxes.Length == 0)
                    throw new InvalidOperationException($"{actor.name} cannot create an art replacement contract.");

                var visualBindTransform = actor.transform.Find("Visual_ART_BIND");
                var visualBind = visualBindTransform != null
                    ? visualBindTransform
                    : new GameObject("Visual_ART_BIND").transform;
                visualBind.SetParent(actor.transform, true);
                visualBind.localPosition = Vector3.zero;
                visualBind.localRotation = Quaternion.identity;
                visualBind.localScale = Vector3.one;
                if (visualRenderer.transform.parent != visualBind)
                    visualRenderer.transform.SetParent(visualBind, true);
                var renderers = visualBind.GetComponentsInChildren<Renderer>(true);

                var anchorTransform = actor.transform.Find("FootAnchor_ART_BIND");
                var footAnchor = anchorTransform != null
                    ? anchorTransform
                    : new GameObject("FootAnchor_ART_BIND").transform;
                footAnchor.SetParent(actor.transform, true);
                footAnchor.position = new Vector3(actor.transform.position.x, bodyCollider.bounds.min.y, actor.transform.position.z);

                var contract = actor.GetComponent<ArtReplacementContractHost>();
                if (contract == null) contract = actor.gameObject.AddComponent<ArtReplacementContractHost>();
                SetReference(contract, "actorRoot", actor.transform);
                SetReference(contract, "visualRoot", visualBind);
                SetReference(contract, "footAnchor", footAnchor);
                SetReference(contract, "bodyCollider", bodyCollider);
                SetReferenceArray(contract, "attackHitboxes", attackHitboxes.Cast<UnityEngine.Object>().ToArray());
                SetReferenceArray(contract, "renderers", renderers.Cast<UnityEngine.Object>().ToArray());
                SetString(contract, "expectedSortingLayer", visualRenderer.sortingLayerName);
            }
        }

        private static void ConfigureAccessibility(
            Func<string, GameObject> find,
            GameObject stageSystems,
            GameObject cameraObject)
        {
            var dialogue = Require(find("TutorialDialoguePanel"), "TutorialDialoguePanel");
            var introduction = Require(find("TutorialIntroductionCard"), "TutorialIntroductionCard");
            var lore = Require(find("TutorialLoreSubtitlePanel"), "TutorialLoreSubtitlePanel");
            var readableTexts = dialogue.GetComponentsInChildren<Text>(true)
                .Concat(introduction.GetComponentsInChildren<Text>(true))
                .Concat(lore.GetComponentsInChildren<Text>(true))
                .Distinct()
                .ToArray();
            var contrastPanels = new[]
            {
                dialogue.GetComponent<Image>(), introduction.GetComponent<Image>(), lore.GetComponent<Image>()
            };
            if (readableTexts.Length < 4 || contrastPanels.Any(panel => panel == null))
                throw new InvalidOperationException("Tutorial accessibility profile requires dialogue, introduction, and lore UI references.");

            var accessibility = stageSystems.GetComponent<TutorialAccessibilityHost>();
            if (accessibility == null) accessibility = stageSystems.AddComponent<TutorialAccessibilityHost>();
            SetReference(accessibility, "cameraFollowHost", cameraObject.GetComponent<CameraFollowHost>());
            SetReferenceArray(accessibility, "readableTexts", readableTexts.Cast<UnityEngine.Object>().ToArray());
            SetReferenceArray(accessibility, "contrastPanels", contrastPanels.Cast<UnityEngine.Object>().ToArray());
            var enemyAttackHosts = Resources.FindObjectsOfTypeAll<EnemyAttackHost>()
                .Where(item => item != null && item.gameObject.scene.IsValid())
                .ToArray();
            SetReferenceArray(accessibility, "enemyAttackHosts", enemyAttackHosts.Cast<UnityEngine.Object>().ToArray());
            SetFloat(accessibility, "motionIntensity", 0.65f);
            SetFloat(accessibility, "flashIntensity", 0.45f);
            SetFloat(accessibility, "minimumPanelAlpha", 0.88f);
            SetInteger(accessibility, "minimumSubtitleFontSize", 20);
        }

        private static bool IsPrimaryArtRenderer(Renderer renderer)
        {
            if (renderer == null) return false;
            return renderer.name == "PlayerVisual" || renderer.name == "EnemyVisual" || renderer.name == "BossVisual";
        }

        private static Material GetOrCreateAttackWarningMaterial(Material sourceMaterial)
        {
            const string path = "Assets/_Project/Art/Materials/TutorialAttackWarning.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art"))
                AssetDatabase.CreateFolder("Assets/_Project", "Art");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Materials"))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");

            var shader = sourceMaterial != null ? sourceMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = sourceMaterial != null ? new Material(sourceMaterial) : new Material(shader);
            material.name = "TutorialAttackWarning";
            var warningColor = new Color(1f, 0.18f, 0.04f, 0.9f);
            if (material.HasProperty("_Color")) material.SetColor("_Color", warningColor);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", warningColor);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ConfigureHudStateCoordinator(
            Func<string, GameObject> find,
            GameObject stageSystems,
            GameObject hud)
        {
            GameObject HudChild(string childName)
            {
                var child = hud.transform.Find(childName);
                if (child == null) throw new InvalidOperationException($"TutorialHUD is missing {childName}.");
                return child.gameObject;
            }

            var coordinator = stageSystems.GetComponent<TutorialHudStateCoordinator>();
            if (coordinator == null) coordinator = stageSystems.AddComponent<TutorialHudStateCoordinator>();
            SetReference(coordinator, "serviceRoot", stageSystems.GetComponent<ServiceRoot>());
            SetReference(coordinator, "bossArenaHost", Require(
                Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(item => item.scene.IsValid() && item.name == "BossArena_Controller"),
                "BossArena_Controller").GetComponent<TutorialBossArenaHost>());
            SetReference(coordinator, "resultOverlay", HudChild("TutorialResultOverlay"));
            SetReference(coordinator, "dialoguePanel", HudChild("TutorialDialoguePanel"));
            SetReference(coordinator, "introductionCard", HudChild("TutorialIntroductionCard"));
            var inventoryOpenButton = HudChild("InventoryOpenButton");
            if (inventoryOpenButton.GetComponent<CanvasGroup>() == null)
                inventoryOpenButton.AddComponent<CanvasGroup>();

            var objectiveBeacon = Require(find("TutorialObjectiveBeacon"), "TutorialObjectiveBeacon");
            var objectiveBeaconVisualTransform = objectiveBeacon.transform.Find("Visual");
            if (objectiveBeaconVisualTransform == null)
                throw new InvalidOperationException("TutorialObjectiveBeacon is missing Visual.");
            var objectiveBeaconVisual = objectiveBeaconVisualTransform.gameObject;

            var dialogueSuppression = new[]
            {
                HudChild("TutorialObjectivePanel"), HudChild("TutorialObjectiveDivider"),
                HudChild("TutorialStatusText"), HudChild("TutorialKeyPromptText"),
                HudChild("TutorialInteractionPromptPanel"), HudChild("PlayerHealthText"),
                HudChild("EnemyHealthText"), inventoryOpenButton,
                HudChild("TutorialStageCaptionText"), objectiveBeaconVisual
            };
            var bossSuppression = new[]
            {
                HudChild("TutorialObjectivePanel"), HudChild("TutorialObjectiveDivider"),
                HudChild("TutorialStatusText"), HudChild("TutorialKeyPromptText"),
                HudChild("TutorialInteractionPromptPanel"), HudChild("EnemyHealthText"),
                inventoryOpenButton, objectiveBeaconVisual
            };
            var resultSuppression = new[]
            {
                HudChild("TutorialObjectivePanel"), HudChild("TutorialObjectiveDivider"),
                HudChild("TutorialStatusText"), HudChild("TutorialKeyPromptText"),
                HudChild("TutorialInteractionPromptPanel"), HudChild("PlayerHealthText"),
                HudChild("EnemyHealthText"), inventoryOpenButton,
                HudChild("TutorialStageCaptionText"), HudChild("TutorialDialoguePanel"),
                HudChild("TutorialIntroductionCard"), HudChild("InventoryPanel"),
                HudChild("ModuleTreePanel"), HudChild("TutorialLoreSubtitlePanel"),
                HudChild("BossHealthBarPanel"), objectiveBeaconVisual
            };

            SetObjectArray(coordinator, "suppressDuringDialogue", dialogueSuppression);
            SetObjectArray(coordinator, "suppressDuringBossCombat", bossSuppression);
            SetObjectArray(coordinator, "suppressDuringResult", resultSuppression);
        }

        private static void ConfigureBossHealthBar(Func<string, GameObject> find, GameObject stageSystems)
        {
            var hud = Require(find("TutorialHUD"), "TutorialHUD");
            var panelTransform = hud.transform.Find("BossHealthBarPanel");
            var panel = panelTransform != null
                ? panelTransform.gameObject
                : new GameObject("BossHealthBarPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(hud.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 58f);
            panelRect.sizeDelta = new Vector2(980f, 82f);
            panel.GetComponent<Image>().color = new Color(0.025f, 0.035f, 0.055f, 0.92f);

            var trackTransform = panel.transform.Find("BossHealthBarTrack");
            var track = trackTransform != null
                ? trackTransform.gameObject
                : new GameObject("BossHealthBarTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            track.transform.SetParent(panel.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0.5f, 0.5f);
            trackRect.anchorMax = new Vector2(0.5f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.anchoredPosition = new Vector2(0f, -16f);
            trackRect.sizeDelta = new Vector2(900f, 22f);
            track.GetComponent<Image>().color = new Color(0.11f, 0.07f, 0.09f, 1f);

            var fillTransform = track.transform.Find("BossHealthBarFill_ART_SLOT");
            var fillObject = fillTransform != null
                ? fillTransform.gameObject
                : new GameObject("BossHealthBarFill_ART_SLOT", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillObject.transform.SetParent(track.transform, false);
            var fillRect = (RectTransform)fillObject.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            var fillImage = fillObject.GetComponent<Image>();
            fillImage.color = new Color(0.82f, 0.16f, 0.24f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;

            var dividerTransform = track.transform.Find("PhaseDivider_ART_SLOT");
            var divider = dividerTransform != null
                ? dividerTransform.gameObject
                : new GameObject("PhaseDivider_ART_SLOT", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divider.transform.SetParent(track.transform, false);
            var dividerRect = (RectTransform)divider.transform;
            dividerRect.anchorMin = new Vector2(0.5f, 0f);
            dividerRect.anchorMax = new Vector2(0.5f, 1f);
            dividerRect.pivot = new Vector2(0.5f, 0.5f);
            dividerRect.anchoredPosition = Vector2.zero;
            dividerRect.sizeDelta = new Vector2(3f, 0f);
            divider.GetComponent<Image>().color = new Color(0.92f, 0.78f, 0.8f, 0.9f);
            divider.transform.SetAsLastSibling();

            var labelTransform = panel.transform.Find("BossHealthValueText");
            var labelObject = labelTransform != null
                ? labelTransform.gameObject
                : new GameObject("BossHealthValueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(panel.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -8f);
            labelRect.sizeDelta = new Vector2(-70f, 34f);
            var labelText = labelObject.GetComponent<Text>();
            var sourceText = find("TutorialStatusText")?.GetComponent<Text>();
            if (sourceText != null) labelText.font = sourceText.font;
            labelText.fontSize = 24;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.96f, 0.91f, 0.92f, 1f);
            labelText.text = "헬테  100 / 100";

            var presenter = panel.GetComponent<BossHealthBarPresenter>();
            if (presenter == null) presenter = panel.AddComponent<BossHealthBarPresenter>();
            SetReference(presenter, "arenaHost", Require(find("BossArena_Controller"), "BossArena_Controller").GetComponent<TutorialBossArenaHost>());
            SetReference(presenter, "bossActor", Require(find("TutorialHelte"), "TutorialHelte").GetComponent<CombatActorHost>());
            SetReference(presenter, "canvasGroup", panel.GetComponent<CanvasGroup>());
            SetReference(presenter, "fillImage", fillImage);
            SetReference(presenter, "healthValueText", labelText);
            panel.GetComponent<CanvasGroup>().alpha = 0f;
            panel.GetComponent<CanvasGroup>().interactable = false;
            panel.GetComponent<CanvasGroup>().blocksRaycasts = false;

            var compactEnemyHealth = find("EnemyHealthText")?.GetComponent<CombatHealthTextPresenter>();
            if (compactEnemyHealth != null)
            {
                var compactHealthSerialized = new SerializedObject(compactEnemyHealth);
                compactHealthSerialized.FindProperty("hideBossActors").boolValue = true;
                compactHealthSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var completion = stageSystems.GetComponent<TutorialCompletionFlowHost>();
            var completionSerialized = new SerializedObject(completion);
            var hudObjects = completionSerialized.FindProperty("gameplayHudObjects");
            var containsPanel = false;
            for (var index = 0; index < hudObjects.arraySize; index++)
                containsPanel |= hudObjects.GetArrayElementAtIndex(index).objectReferenceValue == panel;
            if (!containsPanel)
            {
                var index = hudObjects.arraySize;
                hudObjects.InsertArrayElementAtIndex(index);
                hudObjects.GetArrayElementAtIndex(index).objectReferenceValue = panel;
            }
            completionSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePlayerCombat(GameObject player, PlayerInputHost input, MeleeAttackHost melee)
        {
            var meleeSerialized = new SerializedObject(melee);
            meleeSerialized.FindProperty("comboWindowSeconds").floatValue = 0.5f;
            meleeSerialized.ApplyModifiedPropertiesWithoutUndo();

            var visualMotion = player.GetComponent<CombatVisualMotionHost>();
            SetReference(visualMotion, "meleeAttackHost", melee);

            var rangedRootTransform = player.transform.Find("RangedAttackRoot");
            var rangedRoot = rangedRootTransform != null ? rangedRootTransform.gameObject : new GameObject("RangedAttackRoot");
            rangedRoot.transform.SetParent(player.transform, false);
            rangedRoot.transform.localPosition = Vector3.zero;
            rangedRoot.transform.localRotation = Quaternion.identity;
            rangedRoot.transform.localScale = Vector3.one;

            var projectileCollider = rangedRoot.GetComponent<BoxCollider2D>();
            if (projectileCollider == null) projectileCollider = rangedRoot.AddComponent<BoxCollider2D>();
            projectileCollider.isTrigger = true;
            projectileCollider.size = new Vector2(0.85f, 0.34f);
            projectileCollider.enabled = false;

            var visualTransform = rangedRoot.transform.Find("RangedProjectileVisual_ART_SLOT");
            var visual = visualTransform != null ? visualTransform.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "RangedProjectileVisual_ART_SLOT";
            visual.transform.SetParent(rangedRoot.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.85f, 0.34f, 0.18f);
            var primitiveCollider = visual.GetComponent<Collider>();
            if (primitiveCollider != null) UnityEngine.Object.DestroyImmediate(primitiveCollider);
            var sourceRenderer = player.transform.Find("PlayerAttackEffect")?.GetComponentInChildren<Renderer>(true);
            var projectileRenderer = visual.GetComponent<Renderer>();
            if (sourceRenderer != null && projectileRenderer != null)
                projectileRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            visual.SetActive(false);

            var rangedHost = rangedRoot.GetComponent<PlayerRangedAttackHost>();
            if (rangedHost == null) rangedHost = rangedRoot.AddComponent<PlayerRangedAttackHost>();
            SetReference(rangedHost, "inputHost", input);
            SetReference(rangedHost, "sourceActor", player.GetComponent<CombatActorHost>());
            SetReference(rangedHost, "projectileHitbox", projectileCollider);
            SetReference(rangedHost, "projectileVisualSlot", visual);
        }

        private static void ConfigureDoubleJumpPractice(Func<string, GameObject> find, GameObject stageSystems)
        {
            const string conditionPath = "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-006-DOUBLE-JUMP.asset";
            const string questPath = "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Quests/QST-TUTO-006.asset";
            var condition = AssetDatabase.LoadAssetAtPath<QuestConditionDefinition>(conditionPath);
            if (condition == null)
            {
                condition = ScriptableObject.CreateInstance<QuestConditionDefinition>();
                condition.ConfigureIdentity("COND-TUTO-006-DOUBLE-JUMP");
                AssetDatabase.CreateAsset(condition, conditionPath);
            }
            condition.SignalType = QuestSignalType.DoubleJumpPerformed;
            condition.TargetId = "PLAYER-001";
            condition.RequiredAmount = 1;
            EditorUtility.SetDirty(condition);

            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(questPath);
            if (quest == null) throw new InvalidOperationException("QST-TUTO-006 asset is missing.");
            if (quest.Conditions == null || !quest.Conditions.Contains(condition))
                quest.Conditions = (quest.Conditions ?? Array.Empty<QuestConditionDefinition>()).Concat(new[] { condition }).ToArray();
            EditorUtility.SetDirty(quest);

            var sequence = stageSystems.GetComponent<TutorialQuestSequenceHost>();
            var sequenceSerialized = new SerializedObject(sequence);
            var questAssets = sequenceSerialized.FindProperty("questSequence");
            var objectiveTexts = sequenceSerialized.FindProperty("objectiveTexts");
            for (var index = 0; index < questAssets.arraySize; index++)
            {
                var definition = questAssets.GetArrayElementAtIndex(index).objectReferenceValue as QuestDefinition;
                if (definition != null && definition.StableId == "QST-TUTO-006")
                    objectiveTexts.GetArrayElementAtIndex(index).stringValue = "장비 패키지를 받고, 더블 점프를 1회 사용한 뒤 I로 모듈 트리 열기";
            }
            sequenceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var zone = Require(find("Z03_CryonReward"), "Z03_CryonReward");
            var geometry = zone.transform.Find("GeometryRoot");
            if (geometry == null) throw new InvalidOperationException("Z03 GeometryRoot is missing.");
            var practiceTransform = geometry.Find("DoubleJumpPracticeRoot");
            var practice = practiceTransform != null ? practiceTransform.gameObject : new GameObject("DoubleJumpPracticeRoot");
            practice.transform.SetParent(geometry, true);
            practice.transform.position = Vector3.zero;
            var material = find("Reward_RelayPedestal")?.GetComponent<Renderer>()?.sharedMaterial;
            CreatePracticePlatform(practice.transform, "DoubleJumpPlatform_Low_ART_SLOT", new Vector3(399f, 1.15f, 0f), new Vector3(4.2f, 0.42f, 0.5f), material);
            CreatePracticePlatform(practice.transform, "DoubleJumpPlatform_High_ART_SLOT", new Vector3(407f, 4.05f, 0f), new Vector3(4.2f, 0.42f, 0.5f), material);
            CreatePracticePlatform(practice.transform, "DoubleJumpPlatform_Landing_ART_SLOT", new Vector3(412.5f, 1.45f, 0f), new Vector3(3f, 0.42f, 0.5f), material);
        }

        private static GameObject CreatePracticePlatform(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var platform = CreateOrUpdateCube(parent, name, position, scale, material);
            var collider = platform.GetComponent<BoxCollider2D>();
            if (collider == null) collider = platform.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = Vector2.one;
            return platform;
        }

        private static void ConfigureLoreSubtitles(Func<string, GameObject> find, GameObject stageSystems, GameObject player)
        {
            var hud = Require(find("TutorialHUD"), "TutorialHUD");
            var panelTransform = hud.transform.Find("TutorialLoreSubtitlePanel");
            var panel = panelTransform != null
                ? panelTransform.gameObject
                : new GameObject("TutorialLoreSubtitlePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(hud.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 260f);
            panelRect.sizeDelta = new Vector2(1080f, 86f);
            panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.06f, 0.88f);

            var textTransform = panel.transform.Find("SubtitleText");
            var textObject = textTransform != null
                ? textTransform.gameObject
                : new GameObject("SubtitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(panel.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(28f, 10f);
            textRect.offsetMax = new Vector2(-28f, -10f);
            var subtitleText = textObject.GetComponent<Text>();
            var sourceText = find("TutorialStatusText")?.GetComponent<Text>();
            if (sourceText != null) subtitleText.font = sourceText.font;
            subtitleText.fontSize = 25;
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            subtitleText.verticalOverflow = VerticalWrapMode.Overflow;
            subtitleText.color = new Color(0.91f, 0.97f, 1f, 1f);
            subtitleText.text = string.Empty;

            var presenter = panel.GetComponent<TutorialLoreSubtitlePresenter>();
            if (presenter == null) presenter = panel.AddComponent<TutorialLoreSubtitlePresenter>();
            SetReference(presenter, "canvasGroup", panel.GetComponent<CanvasGroup>());
            SetReference(presenter, "subtitleText", subtitleText);
            SetReference(presenter, "dialoguePresenter", stageSystems.GetComponent<TutorialDialoguePresenter>());

            var questSequence = stageSystems.GetComponent<TutorialQuestSequenceHost>();
            CreateLoreTrigger(find, "Z03_CryonReward", "LoreTrigger_01_Demiurgos", new Vector3(405f, 3f, 0f), "QST-TUTO-007",
                "테우스: 데미우르고스는 제니스를 다스리는 다섯 명이야. 헬테도 그중 한 명이고, 우리와는 비교할 수 없을 만큼 강해.", presenter, questSequence, player.transform);
            CreateLoreTrigger(find, "Z03_CryonReward", "LoreTrigger_02_Blueprint", new Vector3(412f, 3f, 0f), "QST-TUTO-007",
                "테우스: 데미우르고스의 도면에는 그들의 기술이 기록돼 있어. 손에 넣는다면 나르텍스 모듈을 확장할 단서가 될 거야.", presenter, questSequence, player.transform);
            CreateLoreTrigger(find, "Z04_ExteriorCombat_A", "LoreTrigger_03_Zenith", new Vector3(570f, 4f, 0f), "QST-TUTO-007-A",
                "테우스: 저 위 공중섬이 제니스야. 판도라의 메인 공장과 데미우르고스가 있는 곳이지.", presenter, questSequence, player.transform);
            CreateLoreTrigger(find, "Z05_ExteriorCombat_B", "LoreTrigger_04_AetherHistory", new Vector3(765f, 4f, 0f), "QST-TUTO-007-B",
                "테우스: 전설에는 아에테르에 나디르 말고도 여러 나라가 있었다고 해. 지금은 모두 역사 속에서 사라졌지만.", presenter, questSequence, player.transform);
            CreateLoreTrigger(find, "Z06_OreStorage_Boss", "LoreTrigger_05_RelayTower", new Vector3(957f, 3f, 0f), "QST-TUTO-008",
                "테우스: 송신탑은 판도라 로봇에 명령을 보내고 보호막을 충전해. 나디르를 통제하는 핵심 시설인 셈이지.", presenter, questSequence, player.transform);

            var completion = stageSystems.GetComponent<TutorialCompletionFlowHost>();
            var completionSerialized = new SerializedObject(completion);
            var hudObjects = completionSerialized.FindProperty("gameplayHudObjects");
            var containsPanel = false;
            for (var index = 0; index < hudObjects.arraySize; index++)
                containsPanel |= hudObjects.GetArrayElementAtIndex(index).objectReferenceValue == panel;
            if (!containsPanel)
            {
                var index = hudObjects.arraySize;
                hudObjects.InsertArrayElementAtIndex(index);
                hudObjects.GetArrayElementAtIndex(index).objectReferenceValue = panel;
            }
            completionSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureHelteBossStageLayout(Func<string, GameObject> find)
        {
            var zone = Require(find("Z06_OreStorage_Boss"), "Z06_OreStorage_Boss");
            var geometry = zone.transform.Find("GeometryRoot");
            var gameplay = zone.transform.Find("GameplayRoot");
            var anchors = zone.transform.Find("Anchors");
            if (geometry == null || gameplay == null || anchors == null)
                throw new InvalidOperationException("Z06 requires GeometryRoot, GameplayRoot, and Anchors.");

            var sourceFloor = Require(find("Storage_Floor"), "Storage_Floor");
            var material = sourceFloor.GetComponent<Renderer>()?.sharedMaterial;

            // Separate the long, safe approach from a compact flat arena that keeps Helte readable.
            CreatePracticePlatform(geometry, "Storage_Floor", new Vector3(963.5f, -0.5f, 0f), new Vector3(47f, 1f, 0.5f), material);
            CreatePracticePlatform(geometry, "BossArena_Floor_ART_SLOT", new Vector3(998f, -0.5f, 0f), new Vector3(22f, 1f, 0.5f), material);
            CreatePracticePlatform(geometry, "Storage_BoundaryLeft", new Vector3(940.5f, 5f, 0f), new Vector3(1f, 11f, 0.5f), material);
            CreatePracticePlatform(geometry, "Storage_BoundaryRight", new Vector3(1009.5f, 5f, 0f), new Vector3(1f, 11f, 0.5f), material);

            CreateOrUpdateCube(geometry, "Storage_EntranceGateTop", new Vector3(982f, 5f, 0f), new Vector3(8f, 1f, 0.5f), material);
            CreateOrUpdateCube(geometry, "Storage_EntranceGateLeft", new Vector3(978f, 2.5f, 0f), new Vector3(1f, 5f, 0.5f), material);
            CreateOrUpdateCube(geometry, "Storage_EntranceGateRight", new Vector3(986f, 2.5f, 0f), new Vector3(1f, 5f, 0.5f), material);

            var centerMarker = CreateOrUpdateCube(geometry, "Storage_BossMarker", new Vector3(998f, 0.08f, 0f), new Vector3(4f, 0.12f, 0.5f), material);
            var centerMarkerCollider = centerMarker.GetComponent<BoxCollider2D>();
            if (centerMarkerCollider != null) UnityEngine.Object.DestroyImmediate(centerMarkerCollider);

            var entryGate = CreatePracticePlatform(geometry, "BossArena_EntryGate_ART_SLOT", new Vector3(987f, 2.5f, 0f), new Vector3(1.2f, 5f, 0.5f), material);
            entryGate.GetComponent<Renderer>().enabled = false;

            var helte = Require(find("TutorialHelte"), "TutorialHelte");
            helte.transform.position = new Vector3(998f, 1.1f, 0f);

            var startTrigger = Require(find("BossArena_StartTrigger"), "BossArena_StartTrigger");
            startTrigger.transform.position = new Vector3(990f, 2.5f, 0f);
            var startCollider = startTrigger.GetComponent<BoxCollider2D>();
            startCollider.isTrigger = true;
            startCollider.size = new Vector2(2f, 6f);

            CreateOrUpdateCube(gameplay, "BossWarning_ART_SLOT", new Vector3(998f, 5.5f, 0f), new Vector3(22f, 0.35f, 0.5f), material);
            CreateOrUpdateCube(gameplay, "BossPatternLane_01_ART_SLOT", new Vector3(992f, 0.12f, 0f), new Vector3(6f, 0.18f, 0.5f), material);
            CreateOrUpdateCube(gameplay, "BossPatternLane_02_ART_SLOT", new Vector3(998f, 0.12f, 0f), new Vector3(6f, 0.18f, 0.5f), material);
            CreateOrUpdateCube(gameplay, "BossPatternLane_03_ART_SLOT", new Vector3(1004f, 0.12f, 0f), new Vector3(6f, 0.18f, 0.5f), material);

            GetOrCreateAnchor(anchors, "EntrySpawn", new Vector3(948f, 1.1f, 0f));
            GetOrCreateAnchor(anchors, "GuideAnchor", new Vector3(982f, 1.5f, 0f));
            GetOrCreateAnchor(anchors, "ObjectiveAnchor", new Vector3(998f, 2f, 0f));
            GetOrCreateAnchor(anchors, "EncounterSpawnRoot", new Vector3(998f, 1.1f, 0f));
            GetOrCreateAnchor(anchors, "EncounterExitGate", new Vector3(987f, 2.5f, 0f));
            var cameraBounds = GetOrCreateAnchor(anchors, "CameraBounds", new Vector3(975f, 1.5f, 0f));
            cameraBounds.localScale = new Vector3(50f, 10f, 1f);

            var fsmRootTransform = anchors.Find("HelteStageAnchors");
            var fsmRoot = fsmRootTransform != null ? fsmRootTransform : new GameObject("HelteStageAnchors").transform;
            fsmRoot.SetParent(anchors, true);
            fsmRoot.position = zone.transform.position;
            fsmRoot.rotation = Quaternion.identity;
            fsmRoot.localScale = Vector3.one;

            GetOrCreateAnchor(fsmRoot, "ApproachCheckpointAnchor", new Vector3(984f, 1.1f, 0f));
            GetOrCreateAnchor(fsmRoot, "ArenaEntryAnchor", new Vector3(990f, 1.1f, 0f));
            GetOrCreateAnchor(fsmRoot, "BossDialogueAnchor", new Vector3(986f, 1.1f, 0f));
            GetOrCreateAnchor(fsmRoot, "BossCenterAnchor", new Vector3(998f, 1.1f, 0f));
            GetOrCreateAnchor(fsmRoot, "BossCameraFocusAnchor", new Vector3(998f, 2f, 0f));
            GetOrCreateAnchor(fsmRoot, "BossDefeatAnchor", new Vector3(998f, 1.1f, 0f));
            var blinkLeft = GetOrCreateAnchor(fsmRoot, "BossBlinkLeftAnchor", new Vector3(992f, 1.1f, 0f));
            var blinkRight = GetOrCreateAnchor(fsmRoot, "BossBlinkRightAnchor", new Vector3(1004f, 1.1f, 0f));
            var swordLeft = GetOrCreateAnchor(fsmRoot, "SwordSpawn_Left", new Vector3(996f, 5f, 0f));
            var swordRight = GetOrCreateAnchor(fsmRoot, "SwordSpawn_Right", new Vector3(1000f, 5f, 0f));
            var swordCenter = GetOrCreateAnchor(fsmRoot, "SwordSpawn_Center", new Vector3(998f, 5.8f, 0f));

            // Pre-placed, replaceable combat presentation. Runtime FSM only moves/toggles these slots.
            var presentationRootTransform = gameplay.Find("HelteCombatPresentation_ART_SLOTS");
            var presentationRoot = presentationRootTransform != null
                ? presentationRootTransform
                : new GameObject("HelteCombatPresentation_ART_SLOTS").transform;
            presentationRoot.SetParent(gameplay, true);
            presentationRoot.position = Vector3.zero;
            presentationRoot.rotation = Quaternion.identity;
            presentationRoot.localScale = Vector3.one;

            var blinkAfterimage = CreateOrUpdateCube(presentationRoot, "BlinkAfterimage_ART_SLOT", helte.transform.position,
                new Vector3(0.8f, 1.8f, 0.18f), material);
            var dashPath = CreateOrUpdateCube(presentationRoot, "DashPath_ART_SLOT", helte.transform.position,
                new Vector3(4f, 0.16f, 0.18f), material);
            var crossWarning = CreateOrUpdateCube(presentationRoot, "CrossSlashWarning_ART_SLOT", helte.transform.position,
                new Vector3(2.4f, 0.16f, 0.18f), material);
            crossWarning.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            var phaseTransition = CreateOrUpdateCube(presentationRoot, "PhaseTransition_ART_SLOT", helte.transform.position,
                new Vector3(4f, 0.2f, 0.18f), material);
            var swordVisualLeft = CreateOrUpdateCube(presentationRoot, "SwordVisual_Left_ART_SLOT", swordLeft.position,
                new Vector3(0.16f, 1.25f, 0.18f), material);
            var swordVisualRight = CreateOrUpdateCube(presentationRoot, "SwordVisual_Right_ART_SLOT", swordRight.position,
                new Vector3(0.16f, 1.25f, 0.18f), material);
            var swordVisualCenter = CreateOrUpdateCube(presentationRoot, "SwordVisual_Center_ART_SLOT", swordCenter.position,
                new Vector3(0.16f, 1.25f, 0.18f), material);
            blinkAfterimage.SetActive(false);
            dashPath.SetActive(false);
            crossWarning.SetActive(false);
            phaseTransition.SetActive(false);
            swordVisualLeft.SetActive(false);
            swordVisualRight.SetActive(false);
            swordVisualCenter.SetActive(false);

            var patternHost = helte.GetComponent<HelteBossPatternHost>();
            SetReference(patternHost, "blinkLeftAnchor", blinkLeft);
            SetReference(patternHost, "blinkRightAnchor", blinkRight);
            SetReference(patternHost, "bossCenterAnchor", fsmRoot.Find("BossCenterAnchor"));
            SetReference(patternHost, "bossBodyCollider", helte.GetComponent<Collider2D>());
            SetReference(patternHost, "bossVisualSlot", Require(find("BossVisual"), "BossVisual"));
            SetReference(patternHost, "blinkAfterimageSlot", blinkAfterimage);
            SetReference(patternHost, "dashPathSlot", dashPath);
            SetReference(patternHost, "crossSlashWarningSlot", crossWarning);
            SetReference(patternHost, "phaseTransitionSlot", phaseTransition);
            var legacyBlinkLeft = helte.transform.Find("BlinkLeftAnchor");
            var legacyBlinkRight = helte.transform.Find("BlinkRightAnchor");
            if (legacyBlinkLeft != null) UnityEngine.Object.DestroyImmediate(legacyBlinkLeft.gameObject);
            if (legacyBlinkRight != null) UnityEngine.Object.DestroyImmediate(legacyBlinkRight.gameObject);
            var patternSerialized = new SerializedObject(patternHost);
            var swordHitboxes = patternSerialized.FindProperty("swordHitboxes");
            if (swordHitboxes.arraySize == 3)
            {
                var swordSpawns = new[] { swordLeft, swordRight, swordCenter };
                var swordVisuals = new[] { swordVisualLeft, swordVisualRight, swordVisualCenter };
                var swordSpawnProperties = patternSerialized.FindProperty("swordSpawnAnchors");
                var swordVisualProperties = patternSerialized.FindProperty("swordVisualSlots");
                swordSpawnProperties.arraySize = 3;
                swordVisualProperties.arraySize = 3;
                for (var index = 0; index < swordHitboxes.arraySize; index++)
                {
                    var hitbox = swordHitboxes.GetArrayElementAtIndex(index).objectReferenceValue as Collider2D;
                    if (hitbox != null) hitbox.transform.position = swordSpawns[index].position;
                    swordSpawnProperties.GetArrayElementAtIndex(index).objectReferenceValue = swordSpawns[index];
                    swordVisualProperties.GetArrayElementAtIndex(index).objectReferenceValue = swordVisuals[index];
                }
                patternSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var transition in Resources.FindObjectsOfTypeAll<TutorialZoneTransitionHost>())
            {
                if (transition == null || !transition.gameObject.scene.IsValid()) continue;
                var serialized = new SerializedObject(transition);
                if (serialized.FindProperty("nextZoneRoot").objectReferenceValue != zone) continue;
                serialized.FindProperty("destinationCameraMinX").floatValue = 950f;
                serialized.FindProperty("destinationCameraMaxX").floatValue = 1000f;
                serialized.FindProperty("destinationCameraFixedY").floatValue = 1.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CreateLoreTrigger(
            Func<string, GameObject> find, string zoneName, string triggerName, Vector3 position, string questId,
            string line, TutorialLoreSubtitlePresenter presenter, TutorialQuestSequenceHost questSequence, Transform player)
        {
            var zone = Require(find(zoneName), zoneName);
            var rootTransform = zone.transform.Find("LoreSubtitleTriggers");
            var root = rootTransform != null ? rootTransform.gameObject : new GameObject("LoreSubtitleTriggers");
            root.transform.SetParent(zone.transform, true);
            root.transform.position = Vector3.zero;
            var triggerTransform = root.transform.Find(triggerName);
            var triggerObject = triggerTransform != null
                ? triggerTransform.gameObject
                : find(triggerName) ?? new GameObject(triggerName);
            triggerObject.transform.SetParent(root.transform, true);
            triggerObject.transform.position = position;
            triggerObject.transform.rotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;
            var collider = triggerObject.GetComponent<BoxCollider2D>();
            if (collider == null) collider = triggerObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2f, 8f);
            var host = triggerObject.GetComponent<TutorialLoreSubtitleTriggerHost>();
            if (host == null) host = triggerObject.AddComponent<TutorialLoreSubtitleTriggerHost>();
            SetReference(host, "presenter", presenter);
            SetReference(host, "questSequenceHost", questSequence);
            SetReference(host, "player", player);
            var serialized = new SerializedObject(host);
            serialized.FindProperty("requiredQuestId").stringValue = questId;
            serialized.FindProperty("subtitleText").stringValue = line;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureJumpTraining(
            Func<string, GameObject> find,
            GameObject stageSystems,
            GameObject trainingObject,
            GameObject player,
            Rigidbody2D playerBody,
            PlayerMotorHost playerMotor)
        {
            var controllerTransform = trainingObject.transform.Find("JumpProjectileController");
            var controller = controllerTransform != null ? controllerTransform.gameObject : new GameObject("JumpProjectileController");
            controller.transform.SetParent(trainingObject.transform, true);
            controller.transform.position = trainingObject.transform.position;

            var restartPoint = GetOrCreateAnchor(controller.transform, "JumpTrainingRestartPoint", new Vector3(198.5f, -3.4f, 0f));
            var launchPoint = GetOrCreateAnchor(controller.transform, "JumpProjectileLaunchPoint", new Vector3(216f, -2.85f, 0f));
            var endPoint = GetOrCreateAnchor(controller.transform, "JumpProjectileEndPoint", new Vector3(198f, -2.85f, 0f));

            var projectileTransform = controller.transform.Find("ART_SLOT_JumpProjectile");
            var projectile = projectileTransform != null ? projectileTransform.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            projectile.name = "ART_SLOT_JumpProjectile";
            projectile.transform.SetParent(controller.transform, true);
            projectile.transform.position = launchPoint.position;
            projectile.transform.localRotation = Quaternion.identity;
            projectile.transform.localScale = new Vector3(0.85f, 0.34f, 0.18f);
            var primitiveCollider = projectile.GetComponent<Collider>();
            if (primitiveCollider != null) UnityEngine.Object.DestroyImmediate(primitiveCollider);
            var hitbox = projectile.GetComponent<BoxCollider2D>();
            if (hitbox == null) hitbox = projectile.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            var projectileBody = projectile.GetComponent<Rigidbody2D>();
            if (projectileBody == null) projectileBody = projectile.AddComponent<Rigidbody2D>();
            projectileBody.bodyType = RigidbodyType2D.Kinematic;
            projectileBody.gravityScale = 0f;
            projectileBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            var renderer = projectile.GetComponent<Renderer>();
            var sourceRenderer = find("PlayerAttackEffect")?.GetComponentInChildren<Renderer>(true);
            if (renderer != null && sourceRenderer != null) renderer.sharedMaterial = sourceRenderer.sharedMaterial;

            var training = controller.GetComponent<TutorialJumpTrainingHost>();
            if (training == null) training = controller.AddComponent<TutorialJumpTrainingHost>();
            var hazard = projectile.GetComponent<TutorialJumpProjectileHazardHost>();
            if (hazard == null) hazard = projectile.AddComponent<TutorialJumpProjectileHazardHost>();
            SetReference(hazard, "trainingHost", training);
            SetReference(training, "serviceRoot", stageSystems.GetComponent<ServiceRoot>());
            SetReference(training, "questSequenceHost", stageSystems.GetComponent<TutorialQuestSequenceHost>());
            SetReference(training, "questManagerHost", stageSystems.GetComponent<QuestManagerHost>());
            SetReference(training, "player", player.transform);
            SetReference(training, "playerBody", playerBody);
            SetReference(training, "playerMotor", playerMotor);
            SetReference(training, "restartPoint", restartPoint);
            SetReference(training, "launchPoint", launchPoint);
            SetReference(training, "endPoint", endPoint);
            SetReference(training, "projectile", projectile);
            SetReference(training, "projectileBody", projectileBody);
            SetReference(training, "projectileHazard", hazard);
            var serialized = new SerializedObject(training);
            serialized.FindProperty("initialDelay").floatValue = 0.45f;
            serialized.FindProperty("travelDuration").floatValue = 1.55f;
            serialized.FindProperty("repeatDelay").floatValue = 0.5f;
            serialized.FindProperty("restartDelay").floatValue = 0.4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            projectile.SetActive(false);
        }

        private static void ConfigureLadderTransition(Func<string, GameObject> find, GameObject stageSystems)
        {
            var hq = Require(find("Z01_HQ_Prologue"), "Z01_HQ_Prologue");
            var geometry = hq.transform.Find("GeometryRoot");
            if (geometry == null) throw new InvalidOperationException("HQ GeometryRoot is missing.");
            var ladderTransform = geometry.Find("HQ_LadderTransition_ART_SLOT");
            var ladder = ladderTransform != null ? ladderTransform.gameObject : new GameObject("HQ_LadderTransition_ART_SLOT");
            ladder.transform.SetParent(geometry, true);
            ladder.transform.position = Vector3.zero;

            var sourceRenderer = find("HQ_ExitGate_Left")?.GetComponent<Renderer>();
            var material = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            CreateOrUpdateCube(ladder.transform, "LadderRail_Left", new Vector3(35.15f, 0.05f, 0f), new Vector3(0.14f, 4.5f, 0.18f), material);
            CreateOrUpdateCube(ladder.transform, "LadderRail_Right", new Vector3(35.85f, 0.05f, 0f), new Vector3(0.14f, 4.5f, 0.18f), material);
            for (var index = 0; index < 9; index++)
            {
                var y = 2.05f - (index * 0.5f);
                CreateOrUpdateCube(ladder.transform, $"LadderRung_{index + 1:00}", new Vector3(35.5f, y, -0.02f), new Vector3(0.82f, 0.1f, 0.2f), material);
            }

            var entry = GetOrCreateAnchor(ladder.transform, "LadderEntry", new Vector3(35.5f, 2.1f, 0f));
            var exit = GetOrCreateAnchor(ladder.transform, "LadderExit", new Vector3(35.5f, -2f, 0f));
            var exitTrigger = hq.transform.Find("Anchors/ExitTrigger");
            if (exitTrigger == null) throw new InvalidOperationException("HQ ExitTrigger is missing.");
            var transition = exitTrigger.GetComponent<TutorialZoneTransitionHost>();
            SetReference(transition, "ladderEntry", entry);
            SetReference(transition, "ladderExit", exit);
            SetReference(transition, "ladderVisual", ladder);
            var serialized = new SerializedObject(transition);
            serialized.FindProperty("useLadderSequence").boolValue = true;
            serialized.FindProperty("ladderMoveDuration").floatValue = 1.15f;
            serialized.FindProperty("ladderExitHoldDuration").floatValue = 0.12f;
            serialized.FindProperty("ladderStepSway").floatValue = 0.07f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRestart(
            Func<string, GameObject> find,
            GameObject stageSystems,
            PlayerInputHost input,
            PlayerMotorHost motor)
        {
            var restart = stageSystems.GetComponent<TutorialRestartHost>();
            SetReference(restart, "playerInputHost", input);
            SetReference(restart, "playerMotorHost", motor);
            SetReference(restart, "questSequenceHost", stageSystems.GetComponent<TutorialQuestSequenceHost>());
            SetReference(restart, "bossArenaHost", Require(find("BossArena_Controller"), "BossArena_Controller")
                .GetComponent<TutorialBossArenaHost>());
            var fade = Require(find("TutorialHUD"), "TutorialHUD").transform.Find("TutorialZoneFadeOverlay");
            if (fade == null) throw new InvalidOperationException("TutorialZoneFadeOverlay is missing.");
            SetReference(restart, "fadeCanvasGroup", fade.GetComponent<CanvasGroup>());

            Transform Entry(string zoneName)
            {
                var zone = Require(find(zoneName), zoneName);
                var entry = zone.transform.Find("Anchors/EntrySpawn");
                if (entry == null) throw new InvalidOperationException($"{zoneName} is missing Anchors/EntrySpawn.");
                return entry;
            }

            var hq = Entry("Z01_HQ_Prologue");
            var training = Entry("Z02_TrainingRoom");
            var reward = Entry("Z03_CryonReward");
            var combatA = Entry("Z04_ExteriorCombat_A");
            var combatB = Entry("Z05_ExteriorCombat_B");
            var boss = Entry("Z06_OreStorage_Boss");
            SetReference(restart, "relayCheckpointSpawnPoint", combatA);

            var serialized = new SerializedObject(restart);
            serialized.FindProperty("restartSceneOnDeath").boolValue = false;
            serialized.FindProperty("restartDelay").floatValue = 0.35f;
            serialized.FindProperty("fadeDuration").floatValue = 0.25f;
            var checkpoints = serialized.FindProperty("questCheckpoints");
            var definitions = new (string questId, Transform spawn)[]
            {
                ("QST-TUTO-001", hq),
                ("QST-TUTO-004", training),
                ("QST-TUTO-002", training),
                ("QST-TUTO-003", training),
                ("QST-TUTO-005", training),
                ("QST-TUTO-006", reward),
                ("QST-TUTO-007", reward),
                ("QST-TUTO-007-A", combatA),
                ("QST-TUTO-007-B", combatB),
                ("QST-TUTO-008", boss)
            };
            checkpoints.arraySize = definitions.Length;
            for (var index = 0; index < definitions.Length; index++)
            {
                var checkpoint = checkpoints.GetArrayElementAtIndex(index);
                checkpoint.FindPropertyRelative("questId").stringValue = definitions[index].questId;
                checkpoint.FindPropertyRelative("spawnPoint").objectReferenceValue = definitions[index].spawn;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTraining(
            Func<string, GameObject> find,
            GameObject stageSystems,
            GameObject trainingObject,
            GameObject player,
            Rigidbody2D playerBody,
            PlayerMotorHost playerMotor)
        {
            var dashRestart = find("DashTrainingRestartPoint");
            if (dashRestart == null)
            {
                dashRestart = new GameObject("DashTrainingRestartPoint");
                dashRestart.transform.SetParent(trainingObject.transform, true);
            }
            dashRestart.transform.position = new Vector3(176f, -3.4f, 0f);

            var training = trainingObject.GetComponent<TutorialTrainingSpawnHost>();
            SetReference(training, "questManagerHost", stageSystems.GetComponent<QuestManagerHost>());
            SetReference(training, "playerInputHost", player.GetComponent<PlayerInputHost>());
            SetReference(training, "dashRestartPoint", dashRestart.transform);
            SetReference(training, "player", player.transform);
            SetReference(training, "playerBody", playerBody);
            SetReference(training, "playerMotor", playerMotor);
            var serialized = new SerializedObject(training);
            serialized.FindProperty("fallingStartDelay").floatValue = 0.25f;
            serialized.FindProperty("fallingWarningDuration").floatValue = 0.45f;
            serialized.FindProperty("fallingDuration").floatValue = 1.25f;
            serialized.FindProperty("fallingStagger").floatValue = 0.55f;
            serialized.FindProperty("fallingWaveDelay").floatValue = 0.8f;
            serialized.FindProperty("postDialogueStartDelay").floatValue = 0.35f;
            serialized.FindProperty("dashRestartDelay").floatValue = 0.45f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var warningMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/TutorialAttackWarning.mat");
            var landingPoints = new SerializedObject(training).FindProperty("fallingLandingPoints");
            var fallingWarnings = new GameObject[landingPoints.arraySize];
            for (var index = 1; index <= 3; index++)
            {
                var crate = Require(find($"ART_SLOT_FallingCrate_0{index}"), $"falling crate {index}");
                crate.transform.localScale = new Vector3(1.2f, 1.2f, 0.4f);
                var hitbox = crate.GetComponent<BoxCollider2D>();
                if (hitbox == null) hitbox = crate.AddComponent<BoxCollider2D>();
                hitbox.isTrigger = true;
                hitbox.size = new Vector2(1.05f, 1.05f);
                var body = crate.GetComponent<Rigidbody2D>();
                if (body == null) body = crate.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.simulated = true;
                var hazard = crate.GetComponent<TutorialFallingHazardHost>();
                if (hazard == null) hazard = crate.AddComponent<TutorialFallingHazardHost>();
                SetReference(hazard, "trainingHost", training);

                var warning = find($"ART_SLOT_FallingWarning_0{index}");
                if (warning == null) warning = GameObject.CreatePrimitive(PrimitiveType.Cube);
                warning.name = $"ART_SLOT_FallingWarning_0{index}";
                warning.transform.SetParent(trainingObject.transform, true);
                var landingPoint = (Transform)landingPoints.GetArrayElementAtIndex(index - 1).objectReferenceValue;
                warning.transform.position = landingPoint.position + Vector3.up * 0.08f;
                warning.transform.rotation = Quaternion.identity;
                warning.transform.localScale = new Vector3(1.9f, 0.12f, 0.22f);
                var warningCollider = warning.GetComponent<Collider>();
                if (warningCollider != null) UnityEngine.Object.DestroyImmediate(warningCollider);
                var warningRenderer = warning.GetComponent<Renderer>();
                if (warningRenderer != null && warningMaterial != null)
                    warningRenderer.sharedMaterial = warningMaterial;
                warning.SetActive(false);
                fallingWarnings[index - 1] = warning;
            }

            SetObjectArray(training, "fallingWarnings", fallingWarnings);
        }

        private static void ConfigurePulse(Func<string, GameObject> find, GameObject pulseObject)
        {
            var pulse = pulseObject.GetComponent<ModulePulseHost>();
            var projectile = pulseObject.transform.Find("PulseProjectileVisual_ART_SLOT")?.gameObject;
            if (projectile == null)
            {
                projectile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                projectile.name = "PulseProjectileVisual_ART_SLOT";
                projectile.transform.SetParent(pulseObject.transform, false);
                var primitiveCollider = projectile.GetComponent<Collider>();
                if (primitiveCollider != null) UnityEngine.Object.DestroyImmediate(primitiveCollider);
            }

            projectile.transform.localPosition = Vector3.zero;
            projectile.transform.localRotation = Quaternion.identity;
            projectile.transform.localScale = new Vector3(0.9f, 0.32f, 0.18f);
            var sourceRenderer = find("PlayerAttackEffect")?.GetComponentInChildren<Renderer>(true);
            var projectileRenderer = projectile.GetComponent<Renderer>();
            if (sourceRenderer != null && projectileRenderer != null)
                projectileRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            projectile.SetActive(false);

            SetReference(pulse, "projectileVisual", projectile);
            var serialized = new SerializedObject(pulse);
            serialized.FindProperty("travelDistance").floatValue = 8f;
            serialized.FindProperty("travelSeconds").floatValue = 0.45f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureDialogueAndText(GameObject stageSystems, PlayerInputHost input)
        {
            var dialogue = stageSystems.GetComponent<TutorialDialoguePresenter>();
            var dialogueSerialized = new SerializedObject(dialogue);
            dialogueSerialized.FindProperty("continuePrompt").stringValue = "SPACE: 다음";
            dialogueSerialized.FindProperty("closePrompt").stringValue = "SPACE: 닫기";
            dialogueSerialized.ApplyModifiedPropertiesWithoutUndo();

            var statusPresenter = Resources.FindObjectsOfTypeAll<TutorialStatusPresenter>()
                .FirstOrDefault(item => item != null && item.gameObject.scene.IsValid());
            if (statusPresenter == null) throw new InvalidOperationException("TutorialStatusPresenter is missing.");
            SetReference(statusPresenter, "playerInputHost", input);

            var sequence = stageSystems.GetComponent<TutorialQuestSequenceHost>();
            var questSerialized = new SerializedObject(sequence);
            var questAssets = questSerialized.FindProperty("questSequence");
            var objectiveTexts = questSerialized.FindProperty("objectiveTexts");
            for (var index = 0; index < questAssets.arraySize; index++)
            {
                var definition = questAssets.GetArrayElementAtIndex(index).objectReferenceValue as QuestDefinition;
                if (definition != null && definition.StableId == "QST-TUTO-003")
                    objectiveTexts.GetArrayElementAtIndex(index).stringValue = "마우스 왼쪽 버튼으로 커서 방향을 향해 기본 공격 3회";
            }
            questSerialized.ApplyModifiedPropertiesWithoutUndo();

            var narrative = stageSystems.GetComponent<TutorialNarrativeSequenceHost>();
            var narrativeSerialized = new SerializedObject(narrative);
            var beats = narrativeSerialized.FindProperty("beats");
            for (var beatIndex = 0; beatIndex < beats.arraySize; beatIndex++)
            {
                var beat = beats.GetArrayElementAtIndex(beatIndex);
                var questId = beat.FindPropertyRelative("questId").stringValue;
                var lines = beat.FindPropertyRelative("lines");
                if (questId == "QST-TUTO-001")
                {
                    var hasLadderLine = false;
                    for (var lineIndex = 0; lineIndex < lines.arraySize; lineIndex++)
                        hasLadderLine |= lines.GetArrayElementAtIndex(lineIndex).stringValue.Contains("사다리");
                    if (!hasLadderLine)
                    {
                        var lineIndex = lines.arraySize;
                        lines.InsertArrayElementAtIndex(lineIndex);
                        lines.GetArrayElementAtIndex(lineIndex).stringValue = "테우스: 출구의 사다리를 타고 아래로 내려가면 훈련장이야. 내가 먼저 안내할게!";
                    }
                }
                else if (questId == "QST-TUTO-002")
                {
                    lines.arraySize = 3;
                    lines.GetArrayElementAtIndex(0).stringValue = "테우스: 좋아, 몸놀림이 가볍네!";
                    lines.GetArrayElementAtIndex(1).stringValue = "테우스: 다음은 점프 회피 훈련이야. 앞에서 날아오는 낮은 투사체를 뛰어넘어 봐.";
                    lines.GetArrayElementAtIndex(2).stringValue = "테우스: SPACE로 점프해 3번 피해야 해. 투사체에 맞으면 이 구간을 처음부터 다시 시작할 거야!";
                }
                else if (questId == "QST-TUTO-003")
                {
                    for (var lineIndex = 0; lineIndex < lines.arraySize; lineIndex++)
                    {
                        var line = lines.GetArrayElementAtIndex(lineIndex);
                        if (line.stringValue.Contains("ENTER"))
                            line.stringValue = "테우스: 마우스 왼쪽 버튼으로 공격해 봐. 커서가 있는 방향으로 몸을 돌려 3번 이어서 사용하면 돼!";
                    }
                }
                else if (questId == "QST-TUTO-005" && lines.arraySize >= 3)
                {
                    lines.GetArrayElementAtIndex(1).stringValue = "테우스: 마지막은 훈련용 나르텍스 펄스야. 2번 키를 누르면 전방으로 에너지 도형을 발사할 수 있어.";
                    lines.GetArrayElementAtIndex(2).stringValue = "테우스: 소환된 표적을 향해 펄스를 발사해 봐!";
                }
                else if (questId == "QST-TUTO-006")
                {
                    var equipmentLines = new[]
                    {
                        "크리온: 프로메!",
                        "크리온: 언니... 이거 챙겨 가.",
                        "프로메: 이게 뭐야?",
                        "테우스: 스캔 완료. 더블 점프 부츠와 나르텍스 펄스의 실전 운용 모듈이 들어 있어.",
                        "테우스: 훈련장에서 임시로 열었던 펄스 권한을 실전 장착 상태로 전환했어. 2번 키 사용법은 그대로야.",
                        "테우스: 부츠도 장착됐어. 공중에서 한 번 더 점프해 높은 발판까지 올라가 봐.",
                        "테우스: 앞으로 모험 중 쓸 만한 물건을 찾으면 내가 바로 알려줄게.",
                        "프로메: 고마워, 크링. 다녀와서 같이 또 놀자.",
                        "크리온: 응... 잘 다녀와!"
                    };
                    lines.arraySize = equipmentLines.Length;
                    for (var lineIndex = 0; lineIndex < equipmentLines.Length; lineIndex++)
                        lines.GetArrayElementAtIndex(lineIndex).stringValue = equipmentLines[lineIndex];
                }
            }
            narrativeSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureObjectiveArrow(Func<string, GameObject> find, GameObject stageSystems, GameObject pickup)
        {
            var beaconObject = Require(find("TutorialObjectiveBeacon"), "TutorialObjectiveBeacon");
            var beacon = beaconObject.GetComponent<TutorialObjectiveBeaconHost>();
            var serialized = new SerializedObject(beacon);
            SetReference(beacon, "questManagerHost", stageSystems.GetComponent<QuestManagerHost>());
            SetReference(beacon, "equipmentPickupHost", pickup.GetComponent<TutorialBootsPickupHost>());
            SetReference(beacon, "equipmentPickupTarget", pickup.transform);
            var highPlatform = Require(find("Z03_CryonReward"), "Z03_CryonReward")
                .transform.Find("GeometryRoot/DoubleJumpPracticeRoot/DoubleJumpPlatform_High_ART_SLOT");
            if (highPlatform == null) throw new InvalidOperationException("Double-jump target platform is missing.");
            SetReference(beacon, "equipmentDoubleJumpTarget", highPlatform);
            serialized.FindProperty("visualOffset").vector3Value = new Vector3(0f, 2.2f, 0f);
            serialized.FindProperty("hideDistance").floatValue = 2.25f;
            var targets = serialized.FindProperty("targets");
            var hasPickupTarget = false;
            for (var index = 0; index < targets.arraySize; index++)
            {
                var entry = targets.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("questId").stringValue != "QST-TUTO-006") continue;
                entry.FindPropertyRelative("target").objectReferenceValue = pickup.transform;
                hasPickupTarget = true;
            }
            if (!hasPickupTarget)
            {
                var index = targets.arraySize;
                targets.InsertArrayElementAtIndex(index);
                var entry = targets.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("questId").stringValue = "QST-TUTO-006";
                entry.FindPropertyRelative("target").objectReferenceValue = pickup.transform;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var modelSlot = beaconObject.transform.Find("Visual/ModelSlot");
            var lineRenderer = modelSlot != null ? modelSlot.GetComponent<LineRenderer>() : null;
            if (lineRenderer == null) throw new InvalidOperationException("Objective beacon LineRenderer is missing.");
            lineRenderer.loop = true;
            lineRenderer.positionCount = 7;
            lineRenderer.SetPositions(new[]
            {
                new Vector3(-0.5f, 0.2f), new Vector3(0.05f, 0.2f),
                new Vector3(0.05f, 0.48f), new Vector3(0.58f, 0f),
                new Vector3(0.05f, -0.48f), new Vector3(0.05f, -0.2f),
                new Vector3(-0.5f, -0.2f)
            });
            lineRenderer.startWidth = 0.12f;
            lineRenderer.endWidth = 0.12f;
            lineRenderer.useWorldSpace = false;
        }

        private static GameObject Require(GameObject target, string label)
        {
            if (target == null) throw new InvalidOperationException($"Missing tutorial object: {label}");
            return target;
        }

        private static Transform GetOrCreateAnchor(Transform parent, string objectName, Vector3 worldPosition)
        {
            var existing = parent.Find(objectName);
            if (existing == null)
            {
                var anchor = new GameObject(objectName);
                existing = anchor.transform;
                existing.SetParent(parent, true);
            }
            existing.position = worldPosition;
            existing.localRotation = Quaternion.identity;
            existing.localScale = Vector3.one;
            return existing;
        }

        private static GameObject CreateOrUpdateCube(
            Transform parent,
            string objectName,
            Vector3 worldPosition,
            Vector3 worldScale,
            Material material)
        {
            var existing = parent.Find(objectName);
            var cube = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(parent, true);
            cube.transform.position = worldPosition;
            cube.transform.rotation = Quaternion.identity;
            cube.transform.localScale = worldScale;
            var collider = cube.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return cube;
        }

        private static void SetReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            if (target == null || value == null) throw new InvalidOperationException($"Cannot assign {propertyName}.");
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"{target.name} is missing serialized field {propertyName}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(UnityEngine.Object target, string propertyName, GameObject[] values)
        {
            if (target == null || values == null) throw new InvalidOperationException($"Cannot assign {propertyName}.");
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
                throw new InvalidOperationException($"{target.name} is missing array field {propertyName}.");
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"{target.name} is missing float field {propertyName}.");
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"{target.name} is missing string field {propertyName}.");
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInteger(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"{target.name} is missing integer field {propertyName}.");
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReferenceArray(UnityEngine.Object target, string propertyName, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
                throw new InvalidOperationException($"{target.name} is missing reference array {propertyName}.");
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
