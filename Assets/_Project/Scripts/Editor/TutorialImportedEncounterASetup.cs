using System;
using System.Linq;
using Narthex.Content;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    [InitializeOnLoad]
    public static class TutorialImportedEncounterASetup
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string CompletionMarkerName = "F03_연속상승기류수직카메라완료";
        private const string TravelQuestId = "QST-TUTO-007";
        private const string EncounterQuestId = "QST-TUTO-007-A";
        private const string TravelSignalId = "TUTORIAL-EXTERIOR-TO-ENCOUNTER-A";

        static TutorialImportedEncounterASetup()
        {
            EditorApplication.delayCall += TryAutoApply;
        }

        [MenuItem("sragon000/튜토리얼/E 외부에서 F 전투 스테이지 연동 적용")]
        public static void ApplyFromMenu()
        {
            Apply(false);
        }

        private static void TryAutoApply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath) return;
            if (FindSceneObject(scene, CompletionMarkerName) != null)
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
                    Debug.LogWarning($"[sragon000][F01] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            try
            {
                var fIntegration = Require(scene, "F_Encounter01_Integration");
                var fRoot = Require(scene, "F스테이지");
                DisableLegacyEncounters(scene);
                ConfigureQuestAndNarrative(scene);

                var geometry = AnalyzeGeometry(fRoot);
                var spawn = GetOrCreateFunctionalMarker(
                    fIntegration.transform,
                    "F01_Spawn_ExteriorSide",
                    new Vector3(geometry.minX + 3f, -3.9f, 0f),
                    TutorialFunctionMarkerKind.Checkpoint);
                var exitTarget = GetOrCreateFunctionalMarker(
                    fIntegration.transform,
                    "F01_Exit_ToG",
                    new Vector3(geometry.maxX - 1.5f, -3.9f, 0f),
                    TutorialFunctionMarkerKind.Objective);
                var gate = ConfigureCollisionAndGate(fRoot, fIntegration.transform, geometry.exitGateRenderer);
                ConfigureOpeningWind(fIntegration.transform, fRoot, Require(scene, "PlayerRoot"), spawn, geometry);
                ConfigureFallRecovery(scene, fIntegration.transform, geometry);
                var enemies = ConfigureEnemies(scene, fIntegration.transform, geometry);
                ConfigureEncounter(scene, fIntegration.transform, enemies.actors, enemies.spawns, gate);
                ConfigureExteriorTransition(scene, spawn, exitTarget, geometry);
                ConfigureQuestGuidanceAndRestart(scene, spawn, exitTarget);

                var marker = GetOrCreateChild(fIntegration.transform, CompletionMarkerName);
                marker.transform.localPosition = Vector3.zero;
                marker.SetActive(false);
                fRoot.SetActive(false);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAppliedScene(scene);
                Debug.Log(
                    "[sragon000][F01] E→F 전환, 적 3기 동시 활성화·추적, " +
                    "전멸 게이트, F 구역 재시작을 연결했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void DisableLegacyEncounters(Scene scene)
        {
            var oldSequential = FindSceneComponent<TutorialSequentialEncounterHost>(scene);
            if (oldSequential != null) oldSequential.enabled = false;
            foreach (var wave in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<TutorialWaveEncounterHost>(true)))
            {
                if (wave.gameObject.name == "G01_EncounterController") continue;
                wave.enabled = false;
            }
        }

        private static void ConfigureQuestAndNarrative(Scene scene)
        {
            var travelCondition = RequireAsset<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-007-RELAY.asset");
            travelCondition.SignalType = QuestSignalType.PortalUsed;
            travelCondition.TargetId = TravelSignalId;
            travelCondition.RequiredAmount = 1;
            EditorUtility.SetDirty(travelCondition);

            var narrative = FindSceneComponent<TutorialNarrativeSequenceHost>(scene);
            if (narrative == null) throw new InvalidOperationException("TutorialNarrativeSequenceHost를 찾지 못했습니다.");
            var serialized = new SerializedObject(narrative);
            var beats = serialized.FindProperty("beats");
            for (var index = 0; index < beats.arraySize; index++)
            {
                var beat = beats.GetArrayElementAtIndex(index);
                if (beat.FindPropertyRelative("questId").stringValue != EncounterQuestId) continue;
                beat.FindPropertyRelative("stageId").stringValue = "외부 전투 스테이지 1 · TUTO_F_01";
                beat.FindPropertyRelative("deferUntilPortalTargetId").stringValue = TravelSignalId;
                SetStringArray(beat.FindPropertyRelative("lines"), new[]
                {
                    "테우스: 첫 번째 방어선이야. 판도라 유닛 세 기가 동시에 움직이기 시작했어.",
                    "테우스: 이 구역의 적을 모두 쓰러뜨리기 전에는 출구가 열리지 않아.",
                    "프로메: 한꺼번에 정리하고 다음 구역으로 갈게."
                });
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GeometryAnalysis AnalyzeGeometry(GameObject fRoot)
        {
            var renderers = fRoot.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && renderer.bounds.size.x > 0.02f &&
                                   renderer.bounds.size.y > 0.02f)
                .ToArray();
            if (renderers.Length == 0)
                throw new InvalidOperationException("F스테이지에 분석할 도형 Renderer가 없습니다.");

            var minX = renderers.Min(renderer => renderer.bounds.min.x);
            var maxX = renderers.Max(renderer => renderer.bounds.max.x);
            var minY = renderers.Min(renderer => renderer.bounds.min.y);
            var maxY = renderers.Max(renderer => renderer.bounds.max.y);
            var gate = renderers
                .Where(IsWhiteBlockout)
                .Where(renderer => renderer.bounds.size.x <= 1.5f && renderer.bounds.size.y >= 2.2f)
                .OrderByDescending(renderer => renderer.bounds.center.x)
                .FirstOrDefault();
            if (gate == null)
                throw new InvalidOperationException("F스테이지 오른쪽의 얇은 출구 문 도형을 찾지 못했습니다.");

            return new GeometryAnalysis(minX, maxX, minY, maxY, gate);
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null)
                .ToArray();
            if (renderers.Length == 0)
                throw new InvalidOperationException($"{root.name}에 Renderer가 없습니다.");
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static GameObject ConfigureCollisionAndGate(
            GameObject fRoot,
            Transform fIntegration,
            Renderer gateRenderer)
        {
            var proxyRoot = GetOrCreateChild(fIntegration, "F 스테이지 충돌체");
            proxyRoot.transform.localPosition = Vector3.zero;
            proxyRoot.transform.localRotation = Quaternion.identity;
            proxyRoot.transform.localScale = Vector3.one;
            foreach (var collider in proxyRoot.GetComponentsInChildren<BoxCollider2D>(true))
                UnityEngine.Object.DestroyImmediate(collider.gameObject);

            var renderers = fRoot.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && renderer != gateRenderer &&
                                   renderer.bounds.size.x > 0.02f && renderer.bounds.size.y > 0.02f &&
                                   IsWhiteBlockout(renderer))
                .OrderBy(renderer => renderer.transform.GetSiblingIndex())
                .ThenBy(renderer => renderer.name, StringComparer.Ordinal)
                .ToArray();
            for (var index = 0; index < renderers.Length; index++)
            {
                var bounds = renderers[index].bounds;
                var proxy = new GameObject($"충돌체_{index + 1:00}_{renderers[index].name}");
                proxy.transform.SetParent(proxyRoot.transform, true);
                proxy.transform.SetPositionAndRotation(
                    new Vector3(bounds.center.x, bounds.center.y, 0f),
                    Quaternion.identity);
                proxy.transform.localScale = Vector3.one;
                var collider = proxy.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(bounds.size.x, bounds.size.y);
            }

            var gate = GetOrCreateChild(fIntegration, "F01_출구잠금문_PROXY");
            var gateBounds = gateRenderer.bounds;
            gate.transform.SetPositionAndRotation(
                new Vector3(gateBounds.center.x, gateBounds.center.y, 0f),
                Quaternion.identity);
            gate.transform.localScale = Vector3.one;
            var gateCollider = gate.GetComponent<BoxCollider2D>();
            if (gateCollider == null) gateCollider = gate.AddComponent<BoxCollider2D>();
            gateCollider.isTrigger = false;
            gateCollider.size = new Vector2(gateBounds.size.x, gateBounds.size.y);

            var binding = gate.GetComponent<TutorialGateVisualBindingHost>();
            if (binding == null) binding = gate.AddComponent<TutorialGateVisualBindingHost>();
            var serialized = new SerializedObject(binding);
            SetObject(serialized, "boundRenderer", gateRenderer);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return gate;
        }

        private static void ConfigureOpeningWind(
            Transform integration,
            GameObject stageRoot,
            GameObject player,
            Transform stageSpawn,
            GeometryAnalysis geometry)
        {
            var blueSource = stageRoot.GetComponentsInChildren<Renderer>(true)
                .Where(IsBlueBlockout)
                .OrderBy(renderer => renderer.bounds.center.x)
                .FirstOrDefault();
            var markerRoot = GetOrCreateChild(integration, "F01_기능마커");
            var existing = markerRoot.transform.Find("F01_시작활공바람_MARKER");
            var marker = existing != null
                ? existing.gameObject
                : GetOrCreateChild(markerRoot.transform, "F01_시작활공바람_MARKER");
            var collider = marker.GetComponent<BoxCollider2D>();
            if (collider == null) collider = marker.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            var bottom = blueSource != null ? blueSource.bounds.min.y : stageSpawn.position.y - 1f;
            var requiredTop = Mathf.Max(geometry.maxY + 1f, bottom + 12f);
            var height = requiredTop - bottom;
            var width = blueSource != null ? Mathf.Max(blueSource.bounds.size.x, 3f) : 3f;
            var centerX = blueSource != null ? blueSource.bounds.center.x : marker.transform.position.x;
            marker.transform.SetPositionAndRotation(
                new Vector3(centerX, bottom + height * 0.5f, 0f),
                blueSource != null ? blueSource.transform.rotation : Quaternion.identity);
            marker.transform.localScale = Vector3.one;
            collider.offset = Vector2.zero;
            collider.size = new Vector2(width, height);

            var functionMarker = marker.GetComponent<TutorialFunctionMarkerHost>();
            if (functionMarker == null) functionMarker = marker.AddComponent<TutorialFunctionMarkerHost>();
            var markerSerialized = new SerializedObject(functionMarker);
            markerSerialized.FindProperty("markerId").stringValue = "F01-OPENING-WIND";
            markerSerialized.FindProperty("kind").enumValueIndex = (int)TutorialFunctionMarkerKind.Wind;
            markerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var wind = marker.GetComponent<TutorialWindHazardHost>();
            if (wind == null) wind = marker.AddComponent<TutorialWindHazardHost>();
            var serialized = new SerializedObject(wind);
            SetObject(serialized, "playerBody", player.GetComponent<Rigidbody2D>());
            SetObject(serialized, "player", player.transform);
            SetObject(serialized, "playerMotor", player.GetComponent<PlayerMotorHost>());
            serialized.FindProperty("liftAcceleration").floatValue = 32f;
            serialized.FindProperty("maximumRiseSpeed").floatValue = 12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static (CombatActorHost[] actors, Transform[] spawns) ConfigureEnemies(
            Scene scene,
            Transform fIntegration,
            GeometryAnalysis geometry)
        {
            var player = Require(scene, "PlayerRoot").transform;
            var enemies = new[]
            {
                Require(scene, "ExteriorA_Enemy_01_ART_SLOT"),
                Require(scene, "ExteriorA_Enemy_02_ART_SLOT"),
                Require(scene, "ExteriorA_Enemy_03_ART_SLOT")
            };
            var positions = new[]
            {
                new Vector3(Mathf.Lerp(geometry.minX, geometry.maxX, 0.38f), -3.2f, 0f),
                new Vector3(Mathf.Lerp(geometry.minX, geometry.maxX, 0.58f), -3.2f, 0f),
                new Vector3(Mathf.Lerp(geometry.minX, geometry.maxX, 0.76f), -3.2f, 0f)
            };

            var actors = new CombatActorHost[enemies.Length];
            var spawns = new Transform[enemies.Length];
            var enemyRoot = GetOrCreateChild(fIntegration, "F01_EnemySlots");
            var spawnRoot = GetOrCreateChild(fIntegration, "F01_EnemySpawns");
            for (var index = 0; index < enemies.Length; index++)
            {
                var enemy = enemies[index];
                enemy.transform.SetParent(enemyRoot.transform, true);
                actors[index] = enemy.GetComponent<CombatActorHost>();
                if (actors[index] == null)
                    throw new InvalidOperationException($"{enemy.name}에 CombatActorHost가 없습니다.");

                var pursuit = enemy.GetComponent<TutorialEnemyPursuitHost>();
                if (pursuit == null) pursuit = enemy.AddComponent<TutorialEnemyPursuitHost>();
                var pursuitSerialized = new SerializedObject(pursuit);
                SetObject(pursuitSerialized, "actor", actors[index]);
                SetObject(pursuitSerialized, "target", player);
                pursuitSerialized.FindProperty("moveSpeed").floatValue = 1.8f;
                pursuitSerialized.FindProperty("stopDistance").floatValue = 1.15f;
                pursuitSerialized.ApplyModifiedPropertiesWithoutUndo();

                spawns[index] = GetOrCreateFunctionalMarker(
                    spawnRoot.transform,
                    $"F01_EnemySpawn_{index + 1:00}",
                    positions[index],
                    TutorialFunctionMarkerKind.EnemySpawn);
                enemy.transform.SetPositionAndRotation(spawns[index].position, spawns[index].rotation);
                enemy.transform.localScale = Vector3.one;
                enemy.SetActive(false);
            }
            return (actors, spawns);
        }

        private static void ConfigureFallRecovery(
            Scene scene,
            Transform integration,
            GeometryAnalysis geometry)
        {
            var marker = GetOrCreateChild(integration, "F01_낙사복귀_MARKER");
            marker.transform.SetPositionAndRotation(
                new Vector3((geometry.minX + geometry.maxX) * 0.5f, geometry.minY - 6f, 0f),
                Quaternion.identity);
            marker.transform.localScale = Vector3.one;

            var collider = marker.GetComponent<BoxCollider2D>();
            if (collider == null) collider = marker.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.offset = Vector2.zero;
            collider.size = new Vector2(geometry.maxX - geometry.minX + 4f, 2f);

            var functionMarker = marker.GetComponent<TutorialFunctionMarkerHost>();
            if (functionMarker == null) functionMarker = marker.AddComponent<TutorialFunctionMarkerHost>();
            var markerSerialized = new SerializedObject(functionMarker);
            markerSerialized.FindProperty("markerId").stringValue = "F01-FALL-RECOVERY";
            markerSerialized.FindProperty("kind").enumValueIndex =
                (int)TutorialFunctionMarkerKind.FallRecovery;
            markerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var recovery = marker.GetComponent<TutorialFallRestartHost>();
            if (recovery == null) recovery = marker.AddComponent<TutorialFallRestartHost>();
            var recoverySerialized = new SerializedObject(recovery);
            SetObject(recoverySerialized, "restartHost", FindSceneComponent<TutorialRestartHost>(scene));
            SetObject(recoverySerialized, "questSequenceHost",
                FindSceneComponent<TutorialQuestSequenceHost>(scene));
            SetObject(recoverySerialized, "player", Require(scene, "PlayerRoot").transform);
            recoverySerialized.FindProperty("requiredQuestId").stringValue = EncounterQuestId;
            recoverySerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureEncounter(
            Scene scene,
            Transform fIntegration,
            CombatActorHost[] enemies,
            Transform[] spawns,
            GameObject gate)
        {
            var controller = GetOrCreateChild(fIntegration, "F01_EncounterController");
            var host = controller.GetComponent<TutorialSimultaneousEncounterHost>();
            if (host == null) host = controller.AddComponent<TutorialSimultaneousEncounterHost>();
            var serialized = new SerializedObject(host);
            SetObject(serialized, "serviceRoot", Require(scene, "StageSystems").GetComponent<ServiceRoot>());
            SetObject(serialized, "combatSystemHost", Require(scene, "StageSystems").GetComponent<CombatSystemHost>());
            SetObject(serialized, "questSequenceHost", FindSceneComponent<TutorialQuestSequenceHost>(scene));
            serialized.FindProperty("encounterQuestId").stringValue = EncounterQuestId;
            serialized.FindProperty("clearSignalTargetId").stringValue = "ENCOUNTER-A-CLEAR";
            SetObjectArray(serialized.FindProperty("enemies"), enemies);
            SetObjectArray(serialized.FindProperty("spawnPoints"), spawns);
            SetObject(serialized, "exitGateCollider", gate.GetComponent<BoxCollider2D>());
            SetObject(serialized, "exitGateRenderer", gate.GetComponent<TutorialGateVisualBindingHost>().BoundRenderer);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureExteriorTransition(
            Scene scene,
            Transform fSpawn,
            Transform fExitTarget,
            GeometryAnalysis geometry)
        {
            var exit = Require(scene, "E01_Exit_ToF");
            var collider = exit.GetComponent<BoxCollider2D>();
            if (collider == null) collider = exit.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.6f, 3f);

            var host = exit.GetComponent<TutorialZoneTransitionHost>();
            if (host == null) host = exit.AddComponent<TutorialZoneTransitionHost>();
            var player = Require(scene, "PlayerRoot");
            var serialized = new SerializedObject(host);
            SetObject(serialized, "serviceRoot", Require(scene, "StageSystems").GetComponent<ServiceRoot>());
            SetObject(serialized, "playerInputHost", player.GetComponent<PlayerInputHost>());
            SetObject(serialized, "questSequenceHost", FindSceneComponent<TutorialQuestSequenceHost>(scene));
            SetObject(serialized, "dialoguePresenter", FindSceneComponent<TutorialDialoguePresenter>(scene));
            SetObject(serialized, "guideCompanion", FindSceneComponent<TutorialGuideCompanionHost>(scene));
            SetObject(serialized, "cameraFollowHost", Require(scene, "Main Camera").GetComponent<CameraFollowHost>());
            SetObject(serialized, "restartHost", FindSceneComponent<TutorialRestartHost>(scene));
            SetObject(serialized, "objectiveBeacon", FindSceneComponent<TutorialObjectiveBeaconHost>(scene));
            SetObject(serialized, "player", player.transform);
            SetObject(serialized, "playerBody", player.GetComponent<Rigidbody2D>());
            SetObject(serialized, "fadeCanvasGroup", Require(scene, "TutorialZoneFadeOverlay").GetComponent<CanvasGroup>());
            SetObject(serialized, "currentZoneRoot", Require(scene, "외부"));
            SetObject(serialized, "nextZoneRoot", Require(scene, "F스테이지"));
            SetObject(serialized, "destinationSpawn", fSpawn);
            serialized.FindProperty("guideArrivalOffset").vector3Value = new Vector3(1.1f, 1.1f, 0f);
            serialized.FindProperty("requiredQuestId").stringValue = TravelQuestId;
            serialized.FindProperty("portalSignalTargetId").stringValue = TravelSignalId;
            serialized.FindProperty("destinationCheckpointQuestId").stringValue = EncounterQuestId;
            SetObject(serialized, "destinationObjectiveTarget", fExitTarget);
            serialized.FindProperty("useLadderSequence").boolValue = false;
            serialized.FindProperty("requireInteractInput").boolValue = false;
            serialized.FindProperty("destinationCameraMinX").floatValue = geometry.minX + 8f;
            serialized.FindProperty("destinationCameraMaxX").floatValue = geometry.maxX - 8f;
            var verticalMargin = Mathf.Min(5f, Mathf.Max(0f, (geometry.maxY - geometry.minY) * 0.2f));
            serialized.FindProperty("destinationCameraTracksVertical").boolValue = true;
            serialized.FindProperty("destinationCameraMinY").floatValue = geometry.minY + verticalMargin;
            serialized.FindProperty("destinationCameraMaxY").floatValue = geometry.maxY - verticalMargin;
            serialized.FindProperty("fadeOutDuration").floatValue = 0.3f;
            serialized.FindProperty("blackHoldDuration").floatValue = 0.12f;
            serialized.FindProperty("fadeInDuration").floatValue = 0.4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureQuestGuidanceAndRestart(
            Scene scene,
            Transform fSpawn,
            Transform fExitTarget)
        {
            var beacon = FindSceneComponent<TutorialObjectiveBeaconHost>(scene);
            if (beacon == null) throw new InvalidOperationException("TutorialObjectiveBeaconHost를 찾지 못했습니다.");
            var beaconSerialized = new SerializedObject(beacon);
            SetBeaconTarget(beaconSerialized.FindProperty("targets"), EncounterQuestId, fExitTarget);
            beaconSerialized.ApplyModifiedPropertiesWithoutUndo();

            var restart = FindSceneComponent<TutorialRestartHost>(scene);
            if (restart == null) throw new InvalidOperationException("TutorialRestartHost를 찾지 못했습니다.");
            var restartSerialized = new SerializedObject(restart);
            var checkpoints = restartSerialized.FindProperty("questCheckpoints");
            for (var index = 0; index < checkpoints.arraySize; index++)
            {
                var checkpoint = checkpoints.GetArrayElementAtIndex(index);
                if (checkpoint.FindPropertyRelative("questId").stringValue != EncounterQuestId) continue;
                checkpoint.FindPropertyRelative("spawnPoint").objectReferenceValue = fSpawn;
            }
            restartSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBeaconTarget(SerializedProperty targets, string questId, Transform target)
        {
            for (var index = 0; index < targets.arraySize; index++)
            {
                var candidate = targets.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("questId").stringValue != questId) continue;
                candidate.FindPropertyRelative("target").objectReferenceValue = target;
                return;
            }
            var nextIndex = targets.arraySize;
            targets.InsertArrayElementAtIndex(nextIndex);
            var entry = targets.GetArrayElementAtIndex(nextIndex);
            entry.FindPropertyRelative("questId").stringValue = questId;
            entry.FindPropertyRelative("target").objectReferenceValue = target;
        }

        private static bool IsWhiteBlockout(Renderer renderer)
        {
            Color color;
            if (renderer is SpriteRenderer spriteRenderer)
                color = spriteRenderer.color;
            else
            {
                var material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty("_Color")) return true;
                color = material.color;
            }
            var minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            var maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            return minimum >= 0.62f && maximum - minimum <= 0.18f;
        }

        private static bool IsBlueBlockout(Renderer renderer)
        {
            Color color;
            if (renderer is SpriteRenderer spriteRenderer)
                color = spriteRenderer.color;
            else
            {
                var material = renderer.sharedMaterial;
                if (material == null) return false;
                if (material.HasProperty("_BaseColor")) color = material.GetColor("_BaseColor");
                else if (material.HasProperty("_Color")) color = material.color;
                else return false;
            }
            return color.b >= 0.38f && color.b > color.r * 1.15f && color.b >= color.g * 0.9f;
        }

        private static void ValidateAppliedScene(Scene scene)
        {
            var ladder = Require(scene, "C03_Exit_ExteriorSide").GetComponent<TutorialZoneTransitionHost>();
            if (ladder == null || !ladder.HasValidSetup || !ladder.UsesLadderSequence ||
                !ladder.RequiresInteraction)
                throw new InvalidOperationException("C03 사다리는 유효한 F 상호작용 전환이어야 합니다.");

            var transition = Require(scene, "E01_Exit_ToF").GetComponent<TutorialZoneTransitionHost>();
            if (transition == null || !transition.HasValidSetup)
                throw new InvalidOperationException("E→F 구역 전환 참조가 유효하지 않습니다.");

            var encounter = Require(scene, "F01_EncounterController")
                .GetComponent<TutorialSimultaneousEncounterHost>();
            if (encounter == null || !encounter.HasValidSetup ||
                !encounter.ActivatesAllEnemiesAtOnce || encounter.EnemyCount != 3)
                throw new InvalidOperationException("F 전투는 유효한 적 3기를 동시에 활성화해야 합니다.");

            foreach (var enemyName in new[]
                     {
                         "ExteriorA_Enemy_01_ART_SLOT",
                         "ExteriorA_Enemy_02_ART_SLOT",
                         "ExteriorA_Enemy_03_ART_SLOT"
                     })
            {
                var enemy = Require(scene, enemyName);
                var pursuit = enemy.GetComponent<TutorialEnemyPursuitHost>();
                if (pursuit == null || !pursuit.HasValidSetup)
                    throw new InvalidOperationException($"{enemyName}의 플레이어 추적 참조가 유효하지 않습니다.");
            }

            var gate = Require(scene, "F01_출구잠금문_PROXY");
            if (gate.GetComponent<BoxCollider2D>() == null ||
                gate.GetComponent<TutorialGateVisualBindingHost>()?.BoundRenderer == null)
                throw new InvalidOperationException("F 전멸 게이트의 충돌·문 도형 참조가 유효하지 않습니다.");

            var openingWind = Require(scene, "F01_시작활공바람_MARKER");
            var fallRecovery = Require(scene, "F01_낙사복귀_MARKER")
                .GetComponent<TutorialFallRestartHost>();
            if (fallRecovery == null || !fallRecovery.HasValidSetup ||
                fallRecovery.RequiredQuestId != EncounterQuestId)
                throw new InvalidOperationException("F 낙사 복귀 마커 설정이 유효하지 않습니다.");
            if (openingWind.GetComponent<TutorialFunctionMarkerHost>() == null ||
                openingWind.GetComponent<TutorialWindHazardHost>()?.HasValidSetup != true)
                throw new InvalidOperationException("F 시작 활공 바람 마커의 방향·플레이어 참조가 유효하지 않습니다.");
            var windCollider = openingWind.GetComponent<BoxCollider2D>();
            var fBounds = CalculateRendererBounds(Require(scene, "F스테이지"));
            if (windCollider == null || windCollider.bounds.max.y < fBounds.max.y - 0.5f)
                throw new InvalidOperationException("F 시작 상승기류가 상단 이동 경로까지 이어지지 않습니다.");

            if (!transition.DestinationTracksVertical ||
                transition.DestinationCameraMinY > transition.DestinationCameraMaxY)
                throw new InvalidOperationException("F 진입 카메라는 플레이어의 수직 상승을 추적해야 합니다.");

            var condition = RequireAsset<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-007-RELAY.asset");
            if (condition.SignalType != QuestSignalType.PortalUsed ||
                condition.TargetId != TravelSignalId || condition.RequiredAmount != 1)
                throw new InvalidOperationException("QST-TUTO-007은 E→F 출구 사용으로 완료되어야 합니다.");

            Debug.Log(
                "[sragon000][F03][검증 통과] 상승 사다리 F 입력, E→F 수직 카메라 전환, " +
                "상단까지 이어지는 시작 활공 바람, 적 3기 동시 활성화·추적, 전멸 문 잠금, F 체크포인트 정상.");
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"필수 에셋이 없습니다: {path}");
            return asset;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{serialized.targetObject.GetType().Name}.{propertyName} 필드가 없습니다.");
            property.objectReferenceValue = value;
        }

        private static void SetObjectArray<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
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

        private static Transform GetOrCreateAnchor(Transform parent, string name, Vector3 worldPosition)
        {
            var gameObject = GetOrCreateChild(parent, name);
            gameObject.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            gameObject.transform.localScale = Vector3.one;
            return gameObject.transform;
        }

        private static Transform GetOrCreateFunctionalMarker(
            Transform parent,
            string name,
            Vector3 suggestedWorldPosition,
            TutorialFunctionMarkerKind kind)
        {
            var existing = parent.Find(name);
            var gameObject = existing != null ? existing.gameObject : GetOrCreateChild(parent, name);
            if (existing == null)
            {
                gameObject.transform.SetPositionAndRotation(suggestedWorldPosition, Quaternion.identity);
                gameObject.transform.localScale = Vector3.one;
            }

            var marker = gameObject.GetComponent<TutorialFunctionMarkerHost>();
            if (marker == null) marker = gameObject.AddComponent<TutorialFunctionMarkerHost>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("markerId").stringValue = name;
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return gameObject.transform;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static GameObject Require(Scene scene, params string[] names)
        {
            foreach (var name in names)
            {
                var candidate = FindSceneObject(scene, name);
                if (candidate != null) return candidate;
            }
            throw new InvalidOperationException($"필수 씬 오브젝트를 찾지 못했습니다: {string.Join(" / ", names)}");
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate != null && candidate.name == objectName)
                    return candidate.gameObject;
            return null;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault();
        }

        private readonly struct GeometryAnalysis
        {
            public readonly float minX;
            public readonly float maxX;
            public readonly float minY;
            public readonly float maxY;
            public readonly Renderer exitGateRenderer;

            public GeometryAnalysis(float minX, float maxX, float minY, float maxY, Renderer exitGateRenderer)
            {
                this.minX = minX;
                this.maxX = maxX;
                this.minY = minY;
                this.maxY = maxY;
                this.exitGateRenderer = exitGateRenderer;
            }
        }
    }
}
