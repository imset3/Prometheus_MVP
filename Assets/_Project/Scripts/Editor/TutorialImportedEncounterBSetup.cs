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
    public static class TutorialImportedEncounterBSetup
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string CompletionMarkerName = "G01_연동완료";
        private const string EncounterQuestId = "QST-TUTO-007-B";
        private const string BossQuestId = "QST-TUTO-008";
        private const string EntrySignalId = "TUTORIAL-ENCOUNTER-A-EXIT";
        private const string ExitSignalId = "TUTORIAL-ENCOUNTER-B-EXIT";

        static TutorialImportedEncounterBSetup()
        {
            EditorApplication.delayCall += TryAutoApply;
        }

        [MenuItem("sragon000/튜토리얼/F에서 G 전투 스테이지 연동 적용")]
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
                    Debug.LogWarning($"[sragon000][G01] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            try
            {
                var integration = Require(scene, "G_Encounter02_Integration");
                var fRoot = Require(scene, "F스테이지");
                var gRoot = Require(scene, "G스테이지");
                var hRoot = Require(scene, "선착장");
                var player = Require(scene, "PlayerRoot");
                var geometry = AnalyzeGeometry(gRoot);
                var hGeometry = AnalyzeDestinationGeometry(hRoot);

                var gSpawn = GetOrCreateAnchor(
                    integration.transform,
                    "G01_Spawn_FromF",
                    new Vector3(
                        geometry.minX + 2.5f,
                        FindFloorY(geometry.renderers, geometry.minX + 2.5f) + 0.8f,
                        0f));
                var phaseTrigger = GetOrCreateAnchor(
                    integration.transform,
                    "G01_후반부진입_TRIGGER",
                    new Vector3(geometry.internalGate.bounds.center.x + 2f,
                        FindFloorY(geometry.renderers, geometry.internalGate.bounds.center.x + 2f) + 1.2f,
                        0f));
                var gExit = GetOrCreateAnchor(
                    integration.transform,
                    "G01_Exit_ToH",
                    new Vector3(
                        geometry.maxX - 1.5f,
                        FindFloorY(geometry.renderers, geometry.maxX - 1.5f) + 1.2f,
                        0f));
                var hSpawn = GetOrCreateAnchor(
                    integration.transform,
                    "H01_Spawn_FromG",
                    new Vector3(hGeometry.minX + 2.5f,
                        FindFloorY(hGeometry.renderers, hGeometry.minX + 2.5f) + 0.8f,
                        0f));

                var gates = ConfigureCollisionAndGates(gRoot, integration.transform, geometry);
                var enemies = ConfigureEnemies(scene, integration.transform, player.transform, geometry);
                var encounter = ConfigureEncounter(
                    scene,
                    integration.transform,
                    player.transform,
                    enemies.actors,
                    enemies.spawns,
                    enemies.warnings,
                    gates.internalGate,
                    gates.exitGate,
                    phaseTrigger,
                    gExit);
                ConfigurePhaseTrigger(phaseTrigger, player.transform, encounter);
                ConfigureFToGTransition(scene, fRoot, gRoot, integration, gSpawn, gExit, geometry);
                ConfigureGToHTransition(scene, gRoot, hRoot, gExit, hSpawn, hGeometry);
                DisableReplacedLegacyTransitions(scene);
                ConfigureGuidanceAndRestart(scene, gSpawn, phaseTrigger);
                ConfigureNarrative(scene);

                var marker = GetOrCreateChild(integration.transform, CompletionMarkerName);
                marker.transform.localPosition = Vector3.zero;
                marker.SetActive(false);
                gRoot.SetActive(false);
                hRoot.SetActive(false);
                integration.SetActive(false);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAppliedScene(scene);
                Debug.Log(
                    $"[sragon000][G01] F→G 전환, 전반부 2기→통로 개방→후반부 2기, " +
                    $"전멸 출구와 G 재시작을 연결했습니다. 문: " +
                    $"{geometry.internalGate.name} / {geometry.exitGate.name}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static GeometryAnalysis AnalyzeGeometry(GameObject root)
        {
            var renderers = GetGeometryRenderers(root);
            if (renderers.Length == 0)
                throw new InvalidOperationException("G스테이지에 분석할 도형 Renderer가 없습니다.");

            var thinDoors = renderers
                .Where(IsWhiteBlockout)
                .Where(renderer => renderer.bounds.size.x <= 1.6f &&
                                   renderer.bounds.size.y >= 2.2f &&
                                   renderer.bounds.size.y <= 12f)
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();
            if (thinDoors.Length < 2)
                throw new InvalidOperationException("G스테이지의 내부 통로문과 출구문 도형을 찾지 못했습니다.");
            Debug.Log(
                "[sragon000][G01][문 후보] " +
                string.Join(
                    " | ",
                    thinDoors.Select(renderer =>
                        $"{renderer.name} center={renderer.bounds.center:F1} size={renderer.bounds.size:F1}")));

            var exitGate = thinDoors[^1];
            var internalGate = thinDoors
                .Where(renderer => renderer.bounds.size.x <= 0.35f)
                .Where(renderer => renderer.bounds.center.x < exitGate.bounds.center.x - 2f)
                .OrderByDescending(renderer => renderer.bounds.center.x)
                .FirstOrDefault();
            internalGate ??= thinDoors
                .Where(renderer => renderer.bounds.center.x < exitGate.bounds.center.x - 2f)
                .OrderByDescending(renderer => renderer.bounds.center.x)
                .FirstOrDefault();
            if (internalGate == null)
                throw new InvalidOperationException("G스테이지의 두 전투 공간을 나누는 얇은 문 도형을 찾지 못했습니다.");

            return new GeometryAnalysis(
                renderers.Min(renderer => renderer.bounds.min.x),
                renderers.Max(renderer => renderer.bounds.max.x),
                renderers.Min(renderer => renderer.bounds.min.y),
                renderers.Max(renderer => renderer.bounds.max.y),
                renderers,
                internalGate,
                exitGate);
        }

        private static DestinationGeometry AnalyzeDestinationGeometry(GameObject root)
        {
            var renderers = GetGeometryRenderers(root);
            if (renderers.Length == 0)
                throw new InvalidOperationException($"{root.name}에 분석할 도형 Renderer가 없습니다.");
            return new DestinationGeometry(
                renderers.Min(renderer => renderer.bounds.min.x),
                renderers.Max(renderer => renderer.bounds.max.x),
                renderers.Min(renderer => renderer.bounds.min.y),
                renderers.Max(renderer => renderer.bounds.max.y),
                renderers);
        }

        private static Renderer[] GetGeometryRenderers(GameObject root)
        {
            return root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null &&
                                   renderer.bounds.size.x > 0.02f &&
                                   renderer.bounds.size.y > 0.02f)
                .ToArray();
        }

        private static (GameObject internalGate, GameObject exitGate) ConfigureCollisionAndGates(
            GameObject gRoot,
            Transform integration,
            GeometryAnalysis geometry)
        {
            var proxyRoot = GetOrCreateChild(integration, "G 스테이지 충돌체");
            proxyRoot.transform.localPosition = Vector3.zero;
            proxyRoot.transform.localRotation = Quaternion.identity;
            proxyRoot.transform.localScale = Vector3.one;
            foreach (var collider in proxyRoot.GetComponentsInChildren<BoxCollider2D>(true))
                UnityEngine.Object.DestroyImmediate(collider.gameObject);

            var collisionRenderers = gRoot.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null &&
                                   renderer != geometry.internalGate &&
                                   renderer != geometry.exitGate &&
                                   renderer.bounds.size.x > 0.02f &&
                                   renderer.bounds.size.y > 0.02f &&
                                   IsWhiteBlockout(renderer))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ThenBy(renderer => renderer.name, StringComparer.Ordinal)
                .ToArray();
            for (var index = 0; index < collisionRenderers.Length; index++)
            {
                var source = collisionRenderers[index];
                var bounds = source.bounds;
                var proxy = new GameObject($"충돌체_{index + 1:00}_{source.name}");
                proxy.transform.SetParent(proxyRoot.transform, true);
                proxy.transform.SetPositionAndRotation(
                    new Vector3(bounds.center.x, bounds.center.y, 0f),
                    Quaternion.identity);
                proxy.transform.localScale = Vector3.one;
                var collider = proxy.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(bounds.size.x, bounds.size.y);
            }

            return (
                ConfigureGate(integration, "G01_내부통로잠금문_PROXY", geometry.internalGate),
                ConfigureGate(integration, "G01_출구잠금문_PROXY", geometry.exitGate));
        }

        private static GameObject ConfigureGate(Transform parent, string name, Renderer source)
        {
            var gate = GetOrCreateChild(parent, name);
            var bounds = source.bounds;
            gate.transform.SetPositionAndRotation(
                new Vector3(bounds.center.x, bounds.center.y, 0f),
                Quaternion.identity);
            gate.transform.localScale = Vector3.one;
            var collider = gate.GetComponent<BoxCollider2D>();
            if (collider == null) collider = gate.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = new Vector2(bounds.size.x, bounds.size.y);

            var binding = gate.GetComponent<TutorialGateVisualBindingHost>();
            if (binding == null) binding = gate.AddComponent<TutorialGateVisualBindingHost>();
            var serialized = new SerializedObject(binding);
            SetObject(serialized, "boundRenderer", source);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return gate;
        }

        private static (CombatActorHost[] actors, Transform[] spawns, GameObject[] warnings) ConfigureEnemies(
            Scene scene,
            Transform integration,
            Transform player,
            GeometryAnalysis geometry)
        {
            var enemyObjects = new[]
            {
                Require(scene, "ExteriorB_Enemy_01_ART_SLOT"),
                Require(scene, "ExteriorB_Enemy_02_ART_SLOT"),
                Require(scene, "ExteriorB_Enemy_03_ART_SLOT"),
                Require(scene, "ExteriorB_Enemy_04_ART_SLOT")
            };
            var internalX = geometry.internalGate.bounds.center.x;
            var positions = new[]
            {
                GetGroundedPosition(geometry.renderers, Mathf.Lerp(geometry.minX + 1f, internalX - 1f, 0.38f)),
                GetGroundedPosition(geometry.renderers, Mathf.Lerp(geometry.minX + 1f, internalX - 1f, 0.72f)),
                GetGroundedPosition(geometry.renderers, Mathf.Lerp(internalX + 1f, geometry.maxX - 1f, 0.36f)),
                GetGroundedPosition(geometry.renderers, Mathf.Lerp(internalX + 1f, geometry.maxX - 1f, 0.72f))
            };
            var warningObjects = new[]
            {
                Require(scene, "EncounterB_SpawnWarning_01_ART_SLOT"),
                Require(scene, "EncounterB_SpawnWarning_02_ART_SLOT"),
                Require(scene, "EncounterB_SpawnWarning_03_ART_SLOT"),
                Require(scene, "EncounterB_SpawnWarning_04_ART_SLOT")
            };

            var actors = new CombatActorHost[enemyObjects.Length];
            var spawns = new Transform[enemyObjects.Length];
            var enemyRoot = GetOrCreateChild(integration, "G01_EnemySlots");
            var spawnRoot = GetOrCreateChild(integration, "G01_EnemySpawns");
            var warningRoot = GetOrCreateChild(integration, "G01_SpawnWarnings");
            for (var index = 0; index < enemyObjects.Length; index++)
            {
                var enemy = enemyObjects[index];
                enemy.transform.SetParent(enemyRoot.transform, true);
                enemy.transform.SetPositionAndRotation(positions[index], Quaternion.identity);
                enemy.transform.localScale = Vector3.one;
                actors[index] = enemy.GetComponent<CombatActorHost>();
                if (actors[index] == null)
                    throw new InvalidOperationException($"{enemy.name}에 CombatActorHost가 없습니다.");

                var pursuit = enemy.GetComponent<TutorialEnemyPursuitHost>();
                if (pursuit == null) pursuit = enemy.AddComponent<TutorialEnemyPursuitHost>();
                var pursuitSerialized = new SerializedObject(pursuit);
                SetObject(pursuitSerialized, "actor", actors[index]);
                SetObject(pursuitSerialized, "target", player);
                pursuitSerialized.FindProperty("moveSpeed").floatValue = 1.9f;
                pursuitSerialized.FindProperty("stopDistance").floatValue = 1.15f;
                pursuitSerialized.ApplyModifiedPropertiesWithoutUndo();

                spawns[index] = GetOrCreateAnchor(
                    spawnRoot.transform,
                    $"G01_EnemySpawn_{index + 1:00}",
                    positions[index]);
                warningObjects[index].transform.SetParent(warningRoot.transform, true);
                warningObjects[index].SetActive(false);
                enemy.SetActive(false);
            }
            return (actors, spawns, warningObjects);
        }

        private static TutorialWaveEncounterHost ConfigureEncounter(
            Scene scene,
            Transform integration,
            Transform player,
            CombatActorHost[] enemies,
            Transform[] spawns,
            GameObject[] warnings,
            GameObject internalGate,
            GameObject exitGate,
            Transform phaseTrigger,
            Transform exitTarget)
        {
            var controller = Require(scene, "G01_EncounterController", "EncounterB_Controller");
            controller.transform.SetParent(integration, true);
            controller.name = "G01_EncounterController";
            var host = controller.GetComponent<TutorialWaveEncounterHost>();
            if (host == null) host = controller.AddComponent<TutorialWaveEncounterHost>();
            var serialized = new SerializedObject(host);
            SetObject(serialized, "serviceRoot", Require(scene, "StageSystems").GetComponent<Narthex.Core.ServiceRoot>());
            SetObject(serialized, "combatSystemHost", Require(scene, "StageSystems").GetComponent<CombatSystemHost>());
            SetObject(serialized, "questSequenceHost", FindSceneComponent<TutorialQuestSequenceHost>(scene));
            serialized.FindProperty("encounterQuestId").stringValue = EncounterQuestId;
            serialized.FindProperty("clearSignalTargetId").stringValue = "ENCOUNTER-B-CLEAR";
            SetObjectArray(serialized.FindProperty("enemies"), enemies);
            SetObjectArray(serialized.FindProperty("spawnPoints"), spawns);
            SetObjectArray(serialized.FindProperty("spawnWarnings"), warnings);
            var counts = serialized.FindProperty("waveEnemyCounts");
            counts.arraySize = 2;
            counts.GetArrayElementAtIndex(0).intValue = 2;
            counts.GetArrayElementAtIndex(1).intValue = 2;
            serialized.FindProperty("initialDelay").floatValue = 0.35f;
            serialized.FindProperty("warningDuration").floatValue = 0.55f;
            serialized.FindProperty("nextWaveDelay").floatValue = 0.25f;
            serialized.FindProperty("requireTraversalForNextWave").boolValue = true;
            SetObject(serialized, "internalGateCollider", internalGate.GetComponent<BoxCollider2D>());
            SetObject(serialized, "internalGateRenderer",
                internalGate.GetComponent<TutorialGateVisualBindingHost>().BoundRenderer);
            SetObject(serialized, "exitGateCollider", exitGate.GetComponent<BoxCollider2D>());
            SetObject(serialized, "exitGateRenderer",
                exitGate.GetComponent<TutorialGateVisualBindingHost>().BoundRenderer);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            host.enabled = true;
            return host;
        }

        private static void ConfigurePhaseTrigger(
            Transform triggerTransform,
            Transform player,
            TutorialWaveEncounterHost encounter)
        {
            var collider = triggerTransform.GetComponent<BoxCollider2D>();
            if (collider == null) collider = triggerTransform.gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2.2f, 4f);
            var trigger = triggerTransform.GetComponent<TutorialEncounterPhaseTriggerHost>();
            if (trigger == null) trigger = triggerTransform.gameObject.AddComponent<TutorialEncounterPhaseTriggerHost>();
            var serialized = new SerializedObject(trigger);
            SetObject(serialized, "encounter", encounter);
            SetObject(serialized, "player", player);
            SetObject(serialized, "objectiveBeacon", FindSceneComponent<TutorialObjectiveBeaconHost>(
                EditorSceneManager.GetActiveScene()));
            SetObject(serialized, "nextObjectiveTarget",
                Require(EditorSceneManager.GetActiveScene(), "G01_Exit_ToH").transform);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFToGTransition(
            Scene scene,
            GameObject fRoot,
            GameObject gRoot,
            GameObject integration,
            Transform spawn,
            Transform exitTarget,
            GeometryAnalysis geometry)
        {
            var trigger = Require(scene, "F01_Exit_ToG");
            ConfigureTransition(
                scene,
                trigger,
                fRoot,
                gRoot,
                spawn,
                EncounterQuestId,
                EntrySignalId,
                EncounterQuestId,
                Require(scene, "G01_후반부진입_TRIGGER").transform,
                geometry.minX,
                geometry.maxX,
                geometry.minY,
                geometry.maxY,
                new[] { integration });
        }

        private static void ConfigureGToHTransition(
            Scene scene,
            GameObject gRoot,
            GameObject hRoot,
            Transform trigger,
            Transform spawn,
            DestinationGeometry geometry)
        {
            ConfigureTransition(
                scene,
                trigger.gameObject,
                gRoot,
                hRoot,
                spawn,
                BossQuestId,
                ExitSignalId,
                BossQuestId,
                spawn,
                geometry.minX,
                geometry.maxX,
                geometry.minY,
                geometry.maxY,
                Array.Empty<GameObject>());
        }

        private static void ConfigureTransition(
            Scene scene,
            GameObject trigger,
            GameObject currentRoot,
            GameObject nextRoot,
            Transform spawn,
            string requiredQuest,
            string signal,
            string checkpointQuest,
            Transform objectiveTarget,
            float minX,
            float maxX,
            float minY,
            float maxY,
            GameObject[] activateOnArrival)
        {
            var collider = trigger.GetComponent<BoxCollider2D>();
            if (collider == null) collider = trigger.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.8f, 4f);

            var player = Require(scene, "PlayerRoot");
            var host = trigger.GetComponent<TutorialZoneTransitionHost>();
            if (host == null) host = trigger.AddComponent<TutorialZoneTransitionHost>();
            var serialized = new SerializedObject(host);
            SetObject(serialized, "serviceRoot", Require(scene, "StageSystems").GetComponent<Narthex.Core.ServiceRoot>());
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
            SetObject(serialized, "currentZoneRoot", currentRoot);
            SetObject(serialized, "nextZoneRoot", nextRoot);
            SetObject(serialized, "destinationSpawn", spawn);
            serialized.FindProperty("guideArrivalOffset").vector3Value = new Vector3(1.1f, 1.1f, 0f);
            serialized.FindProperty("requiredQuestId").stringValue = requiredQuest;
            serialized.FindProperty("portalSignalTargetId").stringValue = signal;
            serialized.FindProperty("destinationCheckpointQuestId").stringValue = checkpointQuest;
            SetObject(serialized, "destinationObjectiveTarget", objectiveTarget);
            SetObjectArray(serialized.FindProperty("activateOnArrival"), activateOnArrival);
            SetObjectArray(serialized.FindProperty("deactivateOnArrival"), Array.Empty<GameObject>());
            serialized.FindProperty("useLadderSequence").boolValue = false;
            serialized.FindProperty("requireInteractInput").boolValue = false;
            var horizontalMargin = Mathf.Min(8f, Mathf.Max(0f, (maxX - minX) * 0.15f));
            serialized.FindProperty("destinationCameraMinX").floatValue = minX + horizontalMargin;
            serialized.FindProperty("destinationCameraMaxX").floatValue = maxX - horizontalMargin;
            serialized.FindProperty("destinationCameraFixedY").floatValue = (minY + maxY) * 0.5f;
            serialized.FindProperty("destinationCameraTracksVertical").boolValue = true;
            var verticalMargin = Mathf.Min(3f, Mathf.Max(0f, (maxY - minY) * 0.2f));
            serialized.FindProperty("destinationCameraMinY").floatValue = minY + verticalMargin;
            serialized.FindProperty("destinationCameraMaxY").floatValue = maxY - verticalMargin;
            serialized.FindProperty("fadeOutDuration").floatValue = 0.3f;
            serialized.FindProperty("blackHoldDuration").floatValue = 0.12f;
            serialized.FindProperty("fadeInDuration").floatValue = 0.4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureGuidanceAndRestart(Scene scene, Transform spawn, Transform firstTarget)
        {
            var beacon = FindSceneComponent<TutorialObjectiveBeaconHost>(scene);
            if (beacon == null) throw new InvalidOperationException("TutorialObjectiveBeaconHost를 찾지 못했습니다.");
            var beaconSerialized = new SerializedObject(beacon);
            SetBeaconTarget(beaconSerialized.FindProperty("targets"), EncounterQuestId, firstTarget);
            beaconSerialized.ApplyModifiedPropertiesWithoutUndo();

            var restart = FindSceneComponent<TutorialRestartHost>(scene);
            if (restart == null) throw new InvalidOperationException("TutorialRestartHost를 찾지 못했습니다.");
            var serialized = new SerializedObject(restart);
            SetCheckpoint(serialized.FindProperty("questCheckpoints"), EncounterQuestId, spawn);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DisableReplacedLegacyTransitions(Scene scene)
        {
            foreach (var transition in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<TutorialZoneTransitionHost>(true)))
            {
                if (transition == null) continue;
                var serialized = new SerializedObject(transition);
                var signal = serialized.FindProperty("portalSignalTargetId")?.stringValue;
                var isNewEntry = transition.name == "F01_Exit_ToG" && signal == EntrySignalId;
                var isNewExit = transition.name == "G01_Exit_ToH" && signal == ExitSignalId;
                if (isNewEntry || isNewExit) continue;
                if (signal != EntrySignalId && signal != ExitSignalId) continue;
                transition.enabled = false;
            }
        }

        private static void ConfigureNarrative(Scene scene)
        {
            var narrative = FindSceneComponent<TutorialNarrativeSequenceHost>(scene);
            if (narrative == null) throw new InvalidOperationException("TutorialNarrativeSequenceHost를 찾지 못했습니다.");
            var serialized = new SerializedObject(narrative);
            var beats = serialized.FindProperty("beats");
            for (var index = 0; index < beats.arraySize; index++)
            {
                var beat = beats.GetArrayElementAtIndex(index);
                if (beat.FindPropertyRelative("questId").stringValue != EncounterQuestId) continue;
                beat.FindPropertyRelative("stageId").stringValue = "외부 전투 스테이지 2 · TUTO_G_01";
                beat.FindPropertyRelative("deferUntilPortalTargetId").stringValue = EntrySignalId;
                SetStringArray(beat.FindPropertyRelative("lines"), new[]
                {
                    "테우스: 두 번째 방어선은 내부 차단문을 사이에 두고 나뉘어 있어.",
                    "테우스: 앞쪽 적을 먼저 정리하면 통로가 열릴 거야. 안쪽 움직임도 계속 살펴.",
                    "프로메: 통로를 확보한 다음 남은 적까지 처리할게."
                });
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateAppliedScene(Scene scene)
        {
            var encounterObject = Require(scene, "G01_EncounterController");
            var encounter = encounterObject.GetComponent<TutorialWaveEncounterHost>();
            if (encounter == null || !encounter.enabled || !encounter.HasValidSetup ||
                !encounter.RequiresTraversalForNextWave || encounter.WaveCount != 2 ||
                encounter.EnemyCount != 4)
                throw new InvalidOperationException("G 전투는 이동으로 분리된 2개 그룹과 적 4기를 가져야 합니다.");

            var phaseTrigger = Require(scene, "G01_후반부진입_TRIGGER")
                .GetComponent<TutorialEncounterPhaseTriggerHost>();
            if (phaseTrigger == null || !phaseTrigger.HasValidSetup || !phaseTrigger.HasDynamicGuidance)
                throw new InvalidOperationException("G 후반부 진입 트리거 참조가 유효하지 않습니다.");

            foreach (var gateName in new[] { "G01_내부통로잠금문_PROXY", "G01_출구잠금문_PROXY" })
            {
                var gate = Require(scene, gateName);
                if (gate.GetComponent<BoxCollider2D>() == null ||
                    gate.GetComponent<TutorialGateVisualBindingHost>()?.BoundRenderer == null)
                    throw new InvalidOperationException($"{gateName}의 충돌·문 도형 참조가 유효하지 않습니다.");
            }

            foreach (var enemyName in new[]
                     {
                         "ExteriorB_Enemy_01_ART_SLOT",
                         "ExteriorB_Enemy_02_ART_SLOT",
                         "ExteriorB_Enemy_03_ART_SLOT",
                         "ExteriorB_Enemy_04_ART_SLOT"
                     })
            {
                var pursuit = Require(scene, enemyName).GetComponent<TutorialEnemyPursuitHost>();
                if (pursuit == null || !pursuit.HasValidSetup)
                    throw new InvalidOperationException($"{enemyName}의 플레이어 추적 참조가 유효하지 않습니다.");
            }

            foreach (var transitionName in new[] { "F01_Exit_ToG", "G01_Exit_ToH" })
            {
                var transition = Require(scene, transitionName).GetComponent<TutorialZoneTransitionHost>();
                if (transition == null || !transition.HasValidSetup)
                    throw new InvalidOperationException($"{transitionName} 구역 전환 참조가 유효하지 않습니다.");
            }

            Require(scene, "G01_Spawn_FromF");
            Require(scene, "H01_Spawn_FromG");
            Debug.Log(
                "[sragon000][G01][검증 통과] F→G, 전반부 2기, 이동 후 후반부 2기, " +
                "내부문·출구문, G 체크포인트, G→선착장 전환 정상.");
        }

        private static Vector3 GetGroundedPosition(Renderer[] renderers, float x)
        {
            return new Vector3(x, FindFloorY(renderers, x) + 0.8f, 0f);
        }

        private static float FindFloorY(Renderer[] renderers, float x)
        {
            var floors = renderers
                .Where(IsWhiteBlockout)
                .Where(renderer => renderer.bounds.size.x >= 2f &&
                                   renderer.bounds.size.y <= 2.2f &&
                                   renderer.bounds.min.x <= x &&
                                   renderer.bounds.max.x >= x)
                .OrderBy(renderer => Mathf.Abs(renderer.bounds.max.y + 4f))
                .ToArray();
            return floors.Length > 0 ? floors[0].bounds.max.y : -4f;
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

        private static void SetBeaconTarget(SerializedProperty targets, string questId, Transform target)
        {
            for (var index = 0; index < targets.arraySize; index++)
            {
                var candidate = targets.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("questId").stringValue != questId) continue;
                candidate.FindPropertyRelative("target").objectReferenceValue = target;
                return;
            }
            targets.InsertArrayElementAtIndex(targets.arraySize);
            var entry = targets.GetArrayElementAtIndex(targets.arraySize - 1);
            entry.FindPropertyRelative("questId").stringValue = questId;
            entry.FindPropertyRelative("target").objectReferenceValue = target;
        }

        private static void SetCheckpoint(SerializedProperty checkpoints, string questId, Transform spawn)
        {
            for (var index = 0; index < checkpoints.arraySize; index++)
            {
                var checkpoint = checkpoints.GetArrayElementAtIndex(index);
                if (checkpoint.FindPropertyRelative("questId").stringValue != questId) continue;
                checkpoint.FindPropertyRelative("spawnPoint").objectReferenceValue = spawn;
                return;
            }
            checkpoints.InsertArrayElementAtIndex(checkpoints.arraySize);
            var entry = checkpoints.GetArrayElementAtIndex(checkpoints.arraySize - 1);
            entry.FindPropertyRelative("questId").stringValue = questId;
            entry.FindPropertyRelative("spawnPoint").objectReferenceValue = spawn;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    $"{serialized.targetObject.GetType().Name}.{propertyName} 필드가 없습니다.");
            property.objectReferenceValue = value;
        }

        private static void SetObjectArray<T>(SerializedProperty property, T[] values)
            where T : UnityEngine.Object
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
            throw new InvalidOperationException(
                $"필수 씬 오브젝트를 찾지 못했습니다: {string.Join(" / ", names)}");
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
            public readonly Renderer[] renderers;
            public readonly Renderer internalGate;
            public readonly Renderer exitGate;

            public GeometryAnalysis(
                float minX,
                float maxX,
                float minY,
                float maxY,
                Renderer[] renderers,
                Renderer internalGate,
                Renderer exitGate)
            {
                this.minX = minX;
                this.maxX = maxX;
                this.minY = minY;
                this.maxY = maxY;
                this.renderers = renderers;
                this.internalGate = internalGate;
                this.exitGate = exitGate;
            }
        }

        private readonly struct DestinationGeometry
        {
            public readonly float minX;
            public readonly float maxX;
            public readonly float minY;
            public readonly float maxY;
            public readonly Renderer[] renderers;

            public DestinationGeometry(
                float minX,
                float maxX,
                float minY,
                float maxY,
                Renderer[] renderers)
            {
                this.minX = minX;
                this.maxX = maxX;
                this.minY = minY;
                this.maxY = maxY;
                this.renderers = renderers;
            }
        }
    }
}
