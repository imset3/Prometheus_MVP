using System;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    [InitializeOnLoad]
    public static class TutorialImportedHelteSetup
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string CompletionMarkerName = "H01_연동완료";
        private const string BossQuestId = "QST-TUTO-008";
        private const string ExitSignalId = "TUTORIAL-ENCOUNTER-B-EXIT";

        static TutorialImportedHelteSetup()
        {
            EditorApplication.delayCall += TryAutoApply;
        }

        [MenuItem("sragon000/튜토리얼/선착장 헬테 조우 연동 적용")]
        public static void ApplyFromMenu()
        {
            Apply(false);
        }

        [MenuItem("sragon000/튜토리얼/선착장 헬테 구역 분석")]
        public static void AnalyzeFromMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"[sragon000][H01] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            var hRoot = Require(scene, "선착장");
            var renderers = GetRenderers(hRoot);
            Debug.Log(
                "[sragon000][H01][도형 분석]\n" +
                string.Join(
                    "\n",
                    renderers.Select(renderer =>
                        $"{renderer.name} center={renderer.bounds.center:F2} size={renderer.bounds.size:F2} " +
                        $"top={renderer.bounds.max.y:F2} collider={renderer.GetComponent<Collider2D>() != null} " +
                        $"active={renderer.gameObject.activeSelf}")));
        }

        private static void TryAutoApply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath) return;
            if (Find(scene, CompletionMarkerName) != null)
            {
                ValidateAppliedScene(scene);
                return;
            }
            Apply(true);
        }

        private static void Apply(bool automatic)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                if (!automatic)
                    Debug.LogWarning($"[sragon000][H01] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            try
            {
                var hRoot = Require(scene, "선착장");
                var integration = Require(scene, "H_Helte_Integration");
                var gIntegration = Require(scene, "G_Encounter02_Integration");
                var lowerFloor = FindLowerArenaFloor(hRoot);
                EnsureFloorCollision(lowerFloor);

                var floorTop = lowerFloor.bounds.max.y;
                var arenaCenterX = lowerFloor.bounds.max.x - 14f;
                var bossY = floorTop + 1.1f;
                var dialogueX = arenaCenterX - 13f;
                var startX = arenaCenterX - 9.5f;
                var gateX = arenaCenterX - 10.5f;

                var legacyZone = Require(scene, "Z06_OreStorage_Boss");
                // Resolve from unique Helte-owned descendants instead of assuming the old parent.
                // This keeps the operation safe when a previous run moved the roots and stopped before saving.
                var helte = Require(scene, "TutorialHelte");
                var helteStageAnchors = Require(scene, "HelteStageAnchors");
                var gameplayRoot = helte.transform.parent;
                var anchorsRoot = helteStageAnchors.transform.parent;
                if (gameplayRoot == null || anchorsRoot == null)
                    throw new InvalidOperationException("헬테 GameplayRoot 또는 Anchors 부모를 찾지 못했습니다.");
                ReparentKeepingWorld(gameplayRoot, integration.transform);
                ReparentKeepingWorld(anchorsRoot, integration.transform);

                var technicalGeometry = GetOrCreateChild(integration.transform, "H_보스전기술도형");
                var arenaFloor = Require(scene, "BossArena_Floor_ART_SLOT");
                var entryGate = Require(scene, "BossArena_EntryGate_ART_SLOT");
                var bossMarker = Require(scene, "Storage_BossMarker");
                ReparentKeepingWorld(arenaFloor.transform, technicalGeometry.transform);
                ReparentKeepingWorld(entryGate.transform, technicalGeometry.transform);
                ReparentKeepingWorld(bossMarker.transform, technicalGeometry.transform);
                ConfigureTechnicalGeometry(
                    arenaFloor,
                    entryGate,
                    bossMarker,
                    arenaCenterX,
                    gateX,
                    floorTop);

                helte.transform.position = new Vector3(arenaCenterX, bossY, 0f);

                var bossStart = Require(scene, "BossArena_StartTrigger");
                bossStart.transform.position = new Vector3(startX, floorTop + 3f, 0f);
                var bossStartCollider = RequireComponent<BoxCollider2D>(bossStart);
                bossStartCollider.isTrigger = true;
                bossStartCollider.size = new Vector2(1.5f, 6f);

                ConfigureBossPresentation(scene, arenaCenterX, floorTop);
                var anchors = ConfigureAnchors(scene, arenaCenterX, dialogueX, bossY, floorTop);
                ConfigurePatternObjects(scene, arenaCenterX, floorTop, anchors);

                var dialogueTrigger = ConfigureEncounterDialogue(
                    scene,
                    integration.transform,
                    dialogueX,
                    floorTop,
                    anchors.retryCheckpoint,
                    anchors.arenaEntry);
                var hSpawn = Require(scene, "H01_Spawn_FromG");
                ReparentKeepingWorld(hSpawn.transform, integration.transform);
                ConfigureTransition(scene, integration, gIntegration, hRoot, hSpawn.transform, dialogueTrigger.transform);
                ConfigureNarrative(scene);
                ConfigureEncounterPresentation(scene);
                ConfigureGuidanceAndRestart(scene, hSpawn.transform, dialogueTrigger.transform);

                legacyZone.SetActive(false);
                hRoot.SetActive(false);
                integration.SetActive(false);
                var marker = GetOrCreateChild(integration.transform, CompletionMarkerName);
                marker.SetActive(false);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAppliedScene(scene);
                Debug.Log(
                    $"[sragon000][H01] 선착장 하단 바닥 {lowerFloor.name}에 접근 구간, 테우스 진입 대화, " +
                    $"헬테 조우 대화, 재도전 체크포인트, 보스 FSM을 연결했습니다. " +
                    $"spawn={hSpawn.transform.position:F2}, dialogueX={dialogueX:F2}, boss={helte.transform.position:F2}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void ConfigureTechnicalGeometry(
            GameObject arenaFloor,
            GameObject entryGate,
            GameObject bossMarker,
            float arenaCenterX,
            float gateX,
            float floorTop)
        {
            arenaFloor.transform.position = new Vector3(arenaCenterX, floorTop - 0.5f, 0f);
            arenaFloor.transform.rotation = Quaternion.identity;
            arenaFloor.transform.localScale = new Vector3(22f, 1f, 0.5f);
            RequireComponent<BoxCollider2D>(arenaFloor).enabled = true;
            var floorRenderer = arenaFloor.GetComponent<Renderer>();
            if (floorRenderer != null) floorRenderer.enabled = false;

            entryGate.transform.position = new Vector3(gateX, floorTop + 2.5f, 0f);
            entryGate.transform.rotation = Quaternion.identity;
            entryGate.transform.localScale = new Vector3(1.2f, 5f, 0.5f);

            bossMarker.transform.position = new Vector3(arenaCenterX, floorTop + 0.08f, 0f);
            bossMarker.transform.rotation = Quaternion.identity;
            bossMarker.transform.localScale = new Vector3(4f, 0.12f, 0.5f);
            var markerCollider = bossMarker.GetComponent<Collider2D>();
            if (markerCollider != null) UnityEngine.Object.DestroyImmediate(markerCollider);
        }

        private static void ConfigureBossPresentation(Scene scene, float centerX, float floorTop)
        {
            SetWorldPosition(Require(scene, "BossWarning_ART_SLOT"), centerX, floorTop + 5.5f);
            SetWorldPosition(Require(scene, "BossPatternLane_01_ART_SLOT"), centerX - 6f, floorTop + 0.12f);
            SetWorldPosition(Require(scene, "BossPatternLane_02_ART_SLOT"), centerX, floorTop + 0.12f);
            SetWorldPosition(Require(scene, "BossPatternLane_03_ART_SLOT"), centerX + 6f, floorTop + 0.12f);
        }

        private static AnchorSet ConfigureAnchors(
            Scene scene,
            float centerX,
            float dialogueX,
            float bossY,
            float floorTop)
        {
            var root = Require(scene, "HelteStageAnchors").transform;
            var retry = SetWorldPosition(RequireChild(root, "ApproachCheckpointAnchor").gameObject, dialogueX - 1.5f, bossY);
            var arenaEntry = SetWorldPosition(
                RequireChild(root, "ArenaEntryAnchor").gameObject,
                centerX - 9.5f,
                bossY);
            SetWorldPosition(RequireChild(root, "BossDialogueAnchor").gameObject, dialogueX, bossY);
            var center = SetWorldPosition(RequireChild(root, "BossCenterAnchor").gameObject, centerX, bossY);
            SetWorldPosition(RequireChild(root, "BossCameraFocusAnchor").gameObject, centerX, floorTop + 2f);
            SetWorldPosition(RequireChild(root, "BossDefeatAnchor").gameObject, centerX, bossY);
            var blinkLeft = SetWorldPosition(RequireChild(root, "BossBlinkLeftAnchor").gameObject, centerX - 6f, bossY);
            var blinkRight = SetWorldPosition(RequireChild(root, "BossBlinkRightAnchor").gameObject, centerX + 6f, bossY);
            var swordLeft = SetWorldPosition(RequireChild(root, "SwordSpawn_Left").gameObject, centerX - 2f, floorTop + 5f);
            var swordRight = SetWorldPosition(RequireChild(root, "SwordSpawn_Right").gameObject, centerX + 2f, floorTop + 5f);
            var swordCenter = SetWorldPosition(RequireChild(root, "SwordSpawn_Center").gameObject, centerX, floorTop + 5.8f);
            return new AnchorSet(
                retry,
                arenaEntry,
                center,
                blinkLeft,
                blinkRight,
                swordLeft,
                swordRight,
                swordCenter);
        }

        private static void ConfigurePatternObjects(Scene scene, float centerX, float floorTop, AnchorSet anchors)
        {
            var helte = Require(scene, "TutorialHelte");
            var pattern = RequireComponent<HelteBossPatternHost>(helte);
            var serialized = new SerializedObject(pattern);
            SetObject(serialized, "blinkLeftAnchor", anchors.blinkLeft);
            SetObject(serialized, "blinkRightAnchor", anchors.blinkRight);
            SetObject(serialized, "bossCenterAnchor", anchors.center);
            SetTransformArray(serialized.FindProperty("swordSpawnAnchors"),
                new[] { anchors.swordLeft, anchors.swordRight, anchors.swordCenter });

            var swordVisuals = new[]
            {
                Require(scene, "SwordVisual_Left_ART_SLOT"),
                Require(scene, "SwordVisual_Right_ART_SLOT"),
                Require(scene, "SwordVisual_Center_ART_SLOT")
            };
            var swordAnchors = new[] { anchors.swordLeft, anchors.swordRight, anchors.swordCenter };
            for (var index = 0; index < swordVisuals.Length; index++)
                swordVisuals[index].transform.position = swordAnchors[index].position;
            SetGameObjectArray(serialized.FindProperty("swordVisualSlots"), swordVisuals);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetWorldPosition(Require(scene, "BlinkAfterimage_ART_SLOT"), centerX, floorTop + 1.1f);
            SetWorldPosition(Require(scene, "DashPath_ART_SLOT"), centerX, floorTop + 1.1f);
            SetWorldPosition(Require(scene, "CrossSlashWarning_ART_SLOT"), centerX, floorTop + 1.1f);
            SetWorldPosition(Require(scene, "PhaseTransition_ART_SLOT"), centerX, floorTop + 1.1f);
        }

        private static GameObject ConfigureEncounterDialogue(
            Scene scene,
            Transform parent,
            float x,
            float floorTop,
            Transform retryCheckpoint,
            Transform postDialogueObjective)
        {
            var trigger = GetOrCreateChild(parent, "H01_헬테조우대화_TRIGGER");
            trigger.transform.position = new Vector3(x, floorTop + 3f, 0f);
            trigger.transform.rotation = Quaternion.identity;
            trigger.transform.localScale = Vector3.one;
            var collider = RequireComponent<BoxCollider2D>(trigger);
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 6f);

            var host = trigger.GetComponent<TutorialHelteEncounterDialogueHost>();
            if (host == null) host = trigger.AddComponent<TutorialHelteEncounterDialogueHost>();
            var serialized = new SerializedObject(host);
            SetObject(serialized, "serviceRoot", Require(scene, "StageSystems").GetComponent<Narthex.Core.ServiceRoot>());
            SetObject(serialized, "questSequenceHost", FindComponent<TutorialQuestSequenceHost>(scene));
            SetObject(serialized, "dialoguePresenter", FindComponent<TutorialDialoguePresenter>(scene));
            SetObject(serialized, "restartHost", FindComponent<TutorialRestartHost>(scene));
            SetObject(serialized, "objectiveBeacon", FindComponent<TutorialObjectiveBeaconHost>(scene));
            SetObject(serialized, "playerCollider", Require(scene, "PlayerRoot").GetComponent<Collider2D>());
            SetObject(serialized, "encounterTrigger", collider);
            SetObject(serialized, "retryCheckpoint", retryCheckpoint);
            SetObject(serialized, "postDialogueObjective", postDialogueObjective);
            serialized.FindProperty("questId").stringValue = BossQuestId;
            serialized.FindProperty("stageId").stringValue = "선착장 · 헬테 조우";
            SetStringArray(serialized.FindProperty("lines"), new[]
            {
                "헬테: 아다마스의 아이가 여기까지 들어왔군.",
                "프로메: 길을 비켜 줘. 우리는 판도라 공장으로 가야 해."
            });
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        private static void ConfigureEncounterPresentation(Scene scene)
        {
            var playerInput = Require(scene, "PlayerRoot").GetComponent<PlayerInputHost>();
            var arena = Require(scene, "BossArena_Controller").GetComponent<TutorialBossArenaHost>();
            var arenaSerialized = new SerializedObject(arena);
            SetObject(arenaSerialized, "playerInputHost", playerInput);
            arenaSerialized.FindProperty("introWarningSeconds").floatValue = 1.1f;
            arenaSerialized.ApplyModifiedPropertiesWithoutUndo();

            var dialogue = FindComponent<TutorialDialoguePresenter>(scene);
            var dialogueSerialized = new SerializedObject(dialogue);
            var definitions = dialogueSerialized.FindProperty("introductionDefinitions");
            var helteDefinitionFound = false;
            for (var index = 0; index < definitions.arraySize; index++)
            {
                var definition = definitions.GetArrayElementAtIndex(index);
                var questId = definition.FindPropertyRelative("questId").stringValue;
                if (questId != BossQuestId && questId != BossQuestId + "-HELTE") continue;
                definition.FindPropertyRelative("questId").stringValue = BossQuestId + "-HELTE";
                definition.FindPropertyRelative("description").stringValue =
                    "선착장에서 프로메를 막아선 데미우르고스.\n" +
                    "이도류·블링크·고속 대시를 읽고 근접과 원거리 공격으로 대응한다.";
                definition.FindPropertyRelative("showAfterDialogue").boolValue = true;
                helteDefinitionFound = true;
            }
            if (!helteDefinitionFound)
                throw new InvalidOperationException("헬테 소개 카드 정의를 찾지 못했습니다.");
            dialogueSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureTransition(
            Scene scene,
            GameObject hIntegration,
            GameObject gIntegration,
            GameObject hRoot,
            Transform spawn,
            Transform dialogueTarget)
        {
            var transition = Require(scene, "G01_Exit_ToH").GetComponent<TutorialZoneTransitionHost>();
            if (transition == null) throw new InvalidOperationException("G01_Exit_ToH 전환 Host가 없습니다.");
            var serialized = new SerializedObject(transition);
            SetObject(serialized, "nextZoneRoot", hRoot);
            SetObject(serialized, "destinationSpawn", spawn);
            SetObject(serialized, "destinationObjectiveTarget", dialogueTarget);
            SetGameObjectArray(serialized.FindProperty("activateOnArrival"), new[] { hIntegration });
            SetGameObjectArray(serialized.FindProperty("deactivateOnArrival"), Array.Empty<GameObject>());
            SetGameObjectArray(serialized.FindProperty("deactivateOnCompletion"), new[] { gIntegration });
            serialized.FindProperty("requiredQuestId").stringValue = BossQuestId;
            serialized.FindProperty("portalSignalTargetId").stringValue = ExitSignalId;
            serialized.FindProperty("destinationCheckpointQuestId").stringValue = BossQuestId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNarrative(Scene scene)
        {
            var narrative = FindComponent<TutorialNarrativeSequenceHost>(scene);
            var serialized = new SerializedObject(narrative);
            var beats = serialized.FindProperty("beats");
            for (var index = 0; index < beats.arraySize; index++)
            {
                var beat = beats.GetArrayElementAtIndex(index);
                if (beat.FindPropertyRelative("questId").stringValue != BossQuestId) continue;
                beat.FindPropertyRelative("stageId").stringValue = "선착장 · TUTO_H_01";
                beat.FindPropertyRelative("deferUntilPortalTargetId").stringValue = ExitSignalId;
                SetStringArray(beat.FindPropertyRelative("lines"), new[]
                {
                    "테우스: 광물 저장고에 헬테가 있어. 항로 도면과 판도라 공장 접근 정보를 확인해야 해.",
                    "프로메: 여기까지 와서 돌아갈 수는 없어."
                });
                SetGameObjectArray(beat.FindPropertyRelative("activateOnStart"), Array.Empty<GameObject>());
                SetGameObjectArray(beat.FindPropertyRelative("deactivateOnStart"), Array.Empty<GameObject>());
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGuidanceAndRestart(Scene scene, Transform spawn, Transform dialogueTarget)
        {
            var beacon = FindComponent<TutorialObjectiveBeaconHost>(scene);
            var beaconSerialized = new SerializedObject(beacon);
            var targets = beaconSerialized.FindProperty("targets");
            for (var index = 0; index < targets.arraySize; index++)
            {
                var entry = targets.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("questId").stringValue != BossQuestId) continue;
                entry.FindPropertyRelative("target").objectReferenceValue = dialogueTarget;
            }
            beaconSerialized.ApplyModifiedPropertiesWithoutUndo();

            var restart = FindComponent<TutorialRestartHost>(scene);
            var restartSerialized = new SerializedObject(restart);
            var checkpoints = restartSerialized.FindProperty("questCheckpoints");
            for (var index = 0; index < checkpoints.arraySize; index++)
            {
                var entry = checkpoints.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("questId").stringValue != BossQuestId) continue;
                entry.FindPropertyRelative("spawnPoint").objectReferenceValue = spawn;
            }
            restartSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateAppliedScene(Scene scene)
        {
            var hRoot = Require(scene, "선착장");
            var integration = Require(scene, "H_Helte_Integration");
            var lowerFloor = FindLowerArenaFloor(hRoot);
            var helte = Require(scene, "TutorialHelte");
            var dialogue = Require(scene, "H01_헬테조우대화_TRIGGER")
                .GetComponent<TutorialHelteEncounterDialogueHost>();
            var arena = Require(scene, "BossArena_Controller").GetComponent<TutorialBossArenaHost>();
            var transition = Require(scene, "G01_Exit_ToH").GetComponent<TutorialZoneTransitionHost>();

            if (lowerFloor.GetComponent<Collider2D>() == null)
                throw new InvalidOperationException("선착장 하단 바닥에 충돌이 없습니다.");
            if (dialogue == null || !dialogue.HasValidSetup || dialogue.LineCount != 2)
                throw new InvalidOperationException("헬테 조우 대화와 재도전 체크포인트 참조가 유효하지 않습니다.");
            if (arena == null || !arena.HasValidSetup)
                throw new InvalidOperationException("헬테 보스 아레나 참조가 유효하지 않습니다.");
            if (!Mathf.Approximately(arena.IntroWarningSeconds, 1.1f))
                throw new InvalidOperationException("헬테 보스 경고 시간은 1.1초여야 합니다.");
            if (transition == null || !transition.HasValidSetup)
                throw new InvalidOperationException("G→선착장 전환 참조가 유효하지 않습니다.");
            if (helte.transform.position.y <= lowerFloor.bounds.max.y)
                throw new InvalidOperationException("헬테가 선착장 하단 바닥 위에 배치되지 않았습니다.");
            if (integration.transform.parent == null || integration.transform.parent.name != "GameplayIntegrationRoot")
                throw new InvalidOperationException("H 연동 루트 계층이 변경되었습니다.");

            Debug.Log(
                "[sragon000][H01][검증 통과] 선착장 하단 접근, 테우스 진입 대화, 헬테 조우, " +
                "전투 직전 재도전, 보스 아레나/FSM 연결 정상.");
        }

        private static Renderer FindLowerArenaFloor(GameObject hRoot)
        {
            var candidate = GetRenderers(hRoot)
                .Where(renderer => renderer.bounds.size.x >= 20f && renderer.bounds.size.y <= 2f)
                .OrderBy(renderer => renderer.bounds.center.y)
                .ThenByDescending(renderer => renderer.bounds.size.x)
                .FirstOrDefault();
            if (candidate == null)
                throw new InvalidOperationException("선착장의 하단 일자형 전투 바닥을 찾지 못했습니다.");
            return candidate;
        }

        private static Renderer[] GetRenderers(GameObject root)
        {
            return root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null)
                .OrderBy(renderer => renderer.bounds.center.y)
                .ThenBy(renderer => renderer.bounds.center.x)
                .ToArray();
        }

        private static void EnsureFloorCollision(Renderer floor)
        {
            if (floor.GetComponent<Collider2D>() != null) return;
            var collider = floor.gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
        }

        private static void ReparentKeepingWorld(Transform target, Transform parent)
        {
            target.SetParent(parent, true);
        }

        private static Transform SetWorldPosition(GameObject target, float x, float y)
        {
            target.transform.position = new Vector3(x, y, 0f);
            return target.transform;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child.gameObject;
            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static Transform RequireChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) throw new InvalidOperationException($"{parent.name}/{name}를 찾지 못했습니다.");
            return child;
        }

        private static GameObject Require(Scene scene, string objectName)
        {
            var found = Find(scene, objectName);
            if (found == null)
                throw new InvalidOperationException($"필수 오브젝트 '{objectName}'를 찾지 못했습니다.");
            return found;
        }

        private static GameObject Find(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(candidate => candidate.name == objectName)
                ?.gameObject;
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            var found = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault();
            if (found == null) throw new InvalidOperationException($"{typeof(T).Name}을 찾지 못했습니다.");
            return found;
        }

        private static T RequireComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null) component = target.AddComponent<T>();
            return component;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"{serialized.targetObject.name}.{propertyName} 필드가 없습니다.");
            property.objectReferenceValue = value;
        }

        private static void SetGameObjectArray(SerializedProperty property, GameObject[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void SetTransformArray(SerializedProperty property, Transform[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void SetStringArray(SerializedProperty property, string[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).stringValue = values[index];
        }

        private readonly struct AnchorSet
        {
            public readonly Transform retryCheckpoint;
            public readonly Transform arenaEntry;
            public readonly Transform center;
            public readonly Transform blinkLeft;
            public readonly Transform blinkRight;
            public readonly Transform swordLeft;
            public readonly Transform swordRight;
            public readonly Transform swordCenter;

            public AnchorSet(
                Transform retryCheckpoint,
                Transform arenaEntry,
                Transform center,
                Transform blinkLeft,
                Transform blinkRight,
                Transform swordLeft,
                Transform swordRight,
                Transform swordCenter)
            {
                this.retryCheckpoint = retryCheckpoint;
                this.arenaEntry = arenaEntry;
                this.center = center;
                this.blinkLeft = blinkLeft;
                this.blinkRight = blinkRight;
                this.swordLeft = swordLeft;
                this.swordRight = swordRight;
                this.swordCenter = swordCenter;
            }
        }
    }
}
