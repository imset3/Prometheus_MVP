using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Content;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    [InitializeOnLoad]
    public static class TutorialImportedTrainingSetup
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene-이경수 버전.unity";
        private const string CompletionMarkerName = "D01_연동완료";
        private const string RangedConditionPath =
            "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-005-RANGED-TRIPLE-HIT.asset";

        private static readonly string[] TrainingQuestOrder =
        {
            "QST-TUTO-004",
            "QST-TUTO-002",
            "QST-TUTO-006",
            "QST-TUTO-003",
            "QST-TUTO-005"
        };
        private const string ImportedTrainingPlayModeTestName =
            "Narthex.PlayModeTests.TutorialSceneRuntimeSmokeTests." +
            "ImportedTrainingRoom_RunsFiveSequentialLessonsWithRetryAndScopeProtection";
        private const string ImportedFullTutorialPlayModeTestName =
            "Narthex.PlayModeTests.TutorialSceneRuntimeSmokeTests." +
            "TrainingThroughHelte_CompletesTheTutorialThroughLiveSceneSystems";
        private static TestRunnerApi trainingTestRunnerApi;
        private static string runningTestLabel = "훈련장";

        static TutorialImportedTrainingSetup()
        {
            EditorApplication.delayCall += TryAutoApply;
        }

        [MenuItem("sragon000/튜토리얼/수정 훈련장 1차 연동 적용")]
        public static void ApplyFromMenu()
        {
            Apply(false);
        }

        [MenuItem("sragon000/튜토리얼/훈련장 단계 런타임 스모크")]
        public static void RunTrainingPhaseRuntimeSmoke()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[sragon000][훈련장][런타임 스모크] 플레이 모드에서 실행하세요.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            try
            {
                var controller = FindSceneComponents<TutorialTrainingPhaseControllerHost>(scene)
                    .Single(host => host.gameObject.name == "TrainingFlowManager");
                if (!controller.HasValidSetup)
                    throw new InvalidOperationException("단계 컨트롤러 참조가 유효하지 않습니다.");

                for (var index = 0; index < TrainingQuestOrder.Length; index++)
                {
                    controller.Refresh(TrainingQuestOrder[index]);
                    if (controller.CurrentPhaseIndex != index ||
                        controller.ActivePhaseAreaCount != 1 ||
                        !controller.IsExitLocked)
                        throw new InvalidOperationException(
                            $"{TrainingQuestOrder[index]} 전환 실패: " +
                            $"phase={controller.CurrentPhaseIndex}, " +
                            $"activeAreas={controller.ActivePhaseAreaCount}, " +
                            $"exitLocked={controller.IsExitLocked}");
                }

                controller.Refresh("QST-TUTO-007");
                if (controller.CurrentPhaseIndex != -1 ||
                    controller.ActivePhaseAreaCount != 0 ||
                    controller.IsExitLocked)
                    throw new InvalidOperationException(
                        "훈련 종료 전환 실패: 모든 훈련 범위가 꺼지고 출구가 열려야 합니다.");

                Debug.Log(
                    "[sragon000][훈련장][런타임 스모크 통과] 5개 단계 각각 행동 범위 1개, " +
                    "훈련 중 출구 잠금, 완료 후 전 범위 비활성·출구 개방 정상.");
                controller.RefreshCurrentQuest();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("sragon000/튜토리얼/가져온 훈련장 플레이 테스트 실행")]
        public static void RunImportedTrainingPlayModeTest()
        {
            RunImportedPlayModeTest(ImportedTrainingPlayModeTestName, "훈련장");
        }

        [MenuItem("sragon000/튜토리얼/가져온 전체 튜토리얼 플레이 테스트 실행")]
        public static void RunImportedFullTutorialPlayModeTest()
        {
            RunImportedPlayModeTest(ImportedFullTutorialPlayModeTestName, "전체 튜토리얼");
        }

        private static void RunImportedPlayModeTest(string testName, string label)
        {
            if (trainingTestRunnerApi != null)
            {
                Debug.LogWarning($"[sragon000][{runningTestLabel}][플레이 테스트] 이미 테스트가 실행 중입니다.");
                return;
            }

            runningTestLabel = label;
            trainingTestRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            trainingTestRunnerApi.RegisterCallbacks(new ImportedTrainingTestCallbacks());
            trainingTestRunnerApi.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { testName },
                assemblyNames = new[] { "Narthex.PlayModeTests" }
            }));
            Debug.Log($"[sragon000][{label}][플레이 테스트] 새 레벨 씬 통합 테스트를 시작합니다.");
        }

        private sealed class ImportedTrainingTestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var passed = result.TestStatus == TestStatus.Passed;
                Debug.Log(
                    $"[sragon000][{runningTestLabel}][플레이 테스트 {(passed ? "통과" : "실패")}] " +
                    $"status={result.TestStatus}, pass={result.PassCount}, fail={result.FailCount}, " +
                    $"skip={result.SkipCount}, duration={result.Duration:F2}s");
                trainingTestRunnerApi = null;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Passed) return;
                Debug.LogError(
                    $"[sragon000][{runningTestLabel}][플레이 테스트 상세] {result.FullName}: " +
                    $"{result.Message}\n{result.StackTrace}");
            }
        }

        [MenuItem("sragon000/튜토리얼/수정 훈련장 구조 출력")]
        public static void LogTrainingLayout()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"[sragon000][훈련장] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            var trainingRoot = Require(scene, "훈련장 수정버전");
            var renderers = trainingRoot.GetComponentsInChildren<Renderer>(true)
                .OrderBy(renderer => renderer.bounds.center.x)
                .ThenBy(renderer => renderer.bounds.center.y)
                .ToArray();
            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                var color = renderer is SpriteRenderer spriteRenderer
                    ? spriteRenderer.color
                    : renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color")
                        ? renderer.sharedMaterial.color
                        : Color.white;
                Debug.Log(
                    $"[sragon000][훈련장][도형] {renderer.name} | " +
                    $"center=({bounds.center.x:F2},{bounds.center.y:F2}) " +
                    $"size=({bounds.size.x:F2},{bounds.size.y:F2}) " +
                    $"color=({color.r:F2},{color.g:F2},{color.b:F2},{color.a:F2})");
            }
            Debug.Log($"[sragon000][훈련장][구조 완료] 도형 {renderers.Length}개");
        }

        [MenuItem("sragon000/튜토리얼/훈련장 배치 마커 생성")]
        public static void CreateTrainingPlacementMarkers()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"[sragon000][훈련장] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            var integration = Require(scene, "D_Training_Integration", "D_훈련장_연동");
            var markerRoot = GetOrCreateChild(integration.transform, "훈련장 배치 마커");
            markerRoot.tag = "EditorOnly";

            var definitions = new[]
            {
                ("00_공통", "훈련_진입", new Vector3(184f, -3.9f, 0f), 0),
                ("00_공통", "훈련_종료", new Vector3(216f, -3.9f, 0f), 0),
                ("00_공통", "훈련_습격_시작", new Vector3(218f, -3.9f, 0f), 0),

                ("01_대시", "훈련_대시_시작", new Vector3(187f, -3.9f, 0f), 1),
                ("01_대시", "훈련_대시_끝", new Vector3(195f, -3.9f, 0f), 1),
                ("01_대시", "훈련_낙하_01", new Vector3(189f, -3.85f, 0f), 1),
                ("01_대시", "훈련_낙하_02", new Vector3(191f, -3.85f, 0f), 1),
                ("01_대시", "훈련_낙하_03", new Vector3(193f, -3.85f, 0f), 1),
                ("01_대시", "훈련_대시_재시작", new Vector3(187f, -3.9f, 0f), 1),

                ("02_점프", "훈련_점프_시작", new Vector3(195f, -3.9f, 0f), 2),
                ("02_점프", "훈련_점프_끝", new Vector3(201f, -3.9f, 0f), 2),
                ("02_점프", "훈련_점프_발사", new Vector3(201f, -3.2f, 0f), 2),
                ("02_점프", "훈련_점프_도착", new Vector3(195f, -3.2f, 0f), 2),
                ("02_점프", "훈련_점프_재시작", new Vector3(195f, -3.9f, 0f), 2),

                ("03_더블점프", "훈련_더블점프_시작", new Vector3(201f, -3.9f, 0f), 3),
                ("03_더블점프", "훈련_더블점프_끝", new Vector3(206f, -3.9f, 0f), 3),

                ("04_근접공격", "훈련_근접_시작", new Vector3(206f, -3.9f, 0f), 4),
                ("04_근접공격", "훈련_근접_끝", new Vector3(211f, -3.9f, 0f), 4),
                ("04_근접공격", "훈련_근접적_등장", new Vector3(209f, 7f, 0f), 4),
                ("04_근접공격", "훈련_근접적_착지", new Vector3(209f, -3.9f, 0f), 4),

                ("05_원거리공격", "훈련_원거리_시작", new Vector3(211f, -3.9f, 0f), 5),
                ("05_원거리공격", "훈련_원거리_끝", new Vector3(216f, -3.9f, 0f), 5),
                ("05_원거리공격", "훈련_원거리_01", new Vector3(213f, -3.9f, 0f), 5),
                ("05_원거리공격", "훈련_원거리_02", new Vector3(214.5f, -3.9f, 0f), 5),
                ("05_원거리공격", "훈련_원거리_03", new Vector3(216f, -3.9f, 0f), 5)
            };

            var createdCount = 0;
            foreach (var definition in definitions)
            {
                var group = GetOrCreateChild(markerRoot.transform, definition.Item1);
                group.tag = "EditorOnly";
                var existing = group.transform.Find(definition.Item2);
                var marker = existing != null ? existing.gameObject : new GameObject(definition.Item2);
                if (existing == null)
                {
                    marker.transform.SetParent(group.transform, true);
                    marker.transform.SetPositionAndRotation(definition.Item3, Quaternion.identity);
                    marker.transform.localScale = Vector3.one;
                    createdCount++;
                }
                marker.tag = "EditorOnly";
                var icon = EditorGUIUtility.IconContent($"sv_label_{definition.Item4}").image as Texture2D;
                if (icon != null) EditorGUIUtility.SetIconForObject(marker, icon);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = markerRoot;
            EditorGUIUtility.PingObject(markerRoot);
            Debug.Log(
                $"[sragon000][훈련장] 배치 마커 준비 완료: 신규 {createdCount}개 / 전체 {definitions.Length}개. " +
                "기존 마커 위치는 다시 실행해도 유지됩니다.");
        }

        [MenuItem("sragon000/튜토리얼/훈련장 배치 마커 추천 위치 적용")]
        public static void ApplySuggestedTrainingMarkerLayout()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"[sragon000][훈련장] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            if (FindSceneObject(scene, "훈련장 배치 마커") == null)
                CreateTrainingPlacementMarkers();

            var positions = new Dictionary<string, Vector3>
            {
                ["훈련_진입"] = new(184.5f, -3.9f, 0f),
                ["훈련_종료"] = new(216.2f, -3.9f, 0f),
                ["훈련_습격_시작"] = new(215.8f, -3.9f, 0f),

                ["훈련_대시_시작"] = new(185.2f, -3.9f, 0f),
                ["훈련_대시_끝"] = new(192.5f, -3.9f, 0f),
                ["훈련_낙하_01"] = new(187f, -4.25f, 0f),
                ["훈련_낙하_02"] = new(189.6f, -4.25f, 0f),
                ["훈련_낙하_03"] = new(192.2f, -4.25f, 0f),
                ["훈련_대시_재시작"] = new(185.5f, -3.9f, 0f),

                ["훈련_점프_시작"] = new(192.5f, -3.9f, 0f),
                ["훈련_점프_끝"] = new(198.5f, -3.9f, 0f),
                ["훈련_점프_발사"] = new(198.5f, -2.4f, 0f),
                ["훈련_점프_도착"] = new(192.5f, -2.4f, 0f),
                ["훈련_점프_재시작"] = new(193f, -3.9f, 0f),

                ["훈련_더블점프_시작"] = new(198.5f, -3.9f, 0f),
                ["훈련_더블점프_끝"] = new(203.5f, -0.9f, 0f),

                ["훈련_근접_시작"] = new(205f, -3.9f, 0f),
                ["훈련_근접_끝"] = new(211f, -3.9f, 0f),
                ["훈련_근접적_등장"] = new(208f, 8.5f, 0f),
                ["훈련_근접적_착지"] = new(208f, -3.9f, 0f),

                ["훈련_원거리_시작"] = new(211f, -3.9f, 0f),
                ["훈련_원거리_끝"] = new(216.2f, -3.9f, 0f),
                ["훈련_원거리_01"] = new(212.5f, -3.9f, 0f),
                ["훈련_원거리_02"] = new(214.2f, -3.9f, 0f),
                ["훈련_원거리_03"] = new(215.8f, -3.9f, 0f)
            };

            var moved = new List<Transform>();
            foreach (var pair in positions)
            {
                var marker = FindSceneObject(scene, pair.Key);
                if (marker == null)
                    throw new InvalidOperationException($"훈련장 배치 마커가 없습니다: {pair.Key}");
                moved.Add(marker.transform);
            }

            Undo.RecordObjects(moved.Cast<UnityEngine.Object>().ToArray(), "Apply suggested training marker layout");
            foreach (var pair in positions)
                FindSceneObject(scene, pair.Key).transform.SetPositionAndRotation(pair.Value, Quaternion.identity);

            var markerRoot = Require(scene, "훈련장 배치 마커");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = markerRoot;
            EditorGUIUtility.PingObject(markerRoot);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log(
                "[sragon000][훈련장] 실제 도형 기준 추천 배치 완료: " +
                "왼쪽 대시 낙하물 → 중앙 점프/더블점프 발판 → 오른쪽 근접/원거리 → 습격.");
        }

        [MenuItem("sragon000/튜토리얼/훈련장 배치 마커 검증")]
        public static void ValidateTrainingMarkerLayout()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"[sragon000][훈련장] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            var trainingRoot = Require(scene, "훈련장 수정버전");
            var renderers = trainingRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("수정 훈련장 도형을 찾지 못했습니다.");

            var roomBounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                roomBounds.Encapsulate(renderers[index].bounds);

            var markerNames = new[]
            {
                "훈련_진입", "훈련_종료", "훈련_습격_시작",
                "훈련_대시_시작", "훈련_대시_끝", "훈련_낙하_01", "훈련_낙하_02", "훈련_낙하_03",
                "훈련_대시_재시작", "훈련_점프_시작", "훈련_점프_끝", "훈련_점프_발사",
                "훈련_점프_도착", "훈련_점프_재시작", "훈련_더블점프_시작", "훈련_더블점프_끝",
                "훈련_근접_시작", "훈련_근접_끝", "훈련_근접적_등장", "훈련_근접적_착지",
                "훈련_원거리_시작", "훈련_원거리_끝", "훈련_원거리_01", "훈련_원거리_02", "훈련_원거리_03"
            };
            var markers = markerNames.ToDictionary(
                name => name,
                name => Require(scene, name).transform.position);
            foreach (var marker in markers)
            {
                if (marker.Value.x < roomBounds.min.x || marker.Value.x > roomBounds.max.x ||
                    marker.Value.y < roomBounds.min.y || marker.Value.y > roomBounds.max.y)
                    throw new InvalidOperationException($"{marker.Key}이 훈련장 도형 경계를 벗어났습니다: {marker.Value}");
            }

            var phaseStarts = new[]
            {
                markers["훈련_대시_시작"].x,
                markers["훈련_점프_시작"].x,
                markers["훈련_더블점프_시작"].x,
                markers["훈련_근접_시작"].x,
                markers["훈련_원거리_시작"].x
            };
            for (var index = 1; index < phaseStarts.Length; index++)
                if (phaseStarts[index] < phaseStarts[index - 1])
                    throw new InvalidOperationException("훈련 단계 시작점이 대시→점프→더블점프→근접→원거리 순서가 아닙니다.");

            var dashMinimumX = markers["훈련_대시_시작"].x;
            var dashMaximumX = markers["훈련_대시_끝"].x;
            foreach (var name in new[] { "훈련_낙하_01", "훈련_낙하_02", "훈련_낙하_03" })
                if (markers[name].x < dashMinimumX || markers[name].x > dashMaximumX)
                    throw new InvalidOperationException($"{name}이 대시 훈련 구간 밖에 있습니다.");

            if (markers["훈련_점프_발사"].x <= markers["훈련_점프_도착"].x)
                throw new InvalidOperationException("점프 회피 투사체는 플레이어 진행 방향의 반대인 오른쪽→왼쪽으로 이동해야 합니다.");

            var lowerPlatform = renderers
                .Select(renderer => renderer.bounds)
                .FirstOrDefault(bounds =>
                    Mathf.Abs(bounds.center.x - 202f) <= 0.1f &&
                    Mathf.Abs(bounds.center.y + 1.5f) <= 0.1f);
            if (lowerPlatform.size == Vector3.zero)
                throw new InvalidOperationException("더블점프 착지용 X=202 중앙 발판을 찾지 못했습니다.");
            var doubleJumpEnd = markers["훈련_더블점프_끝"];
            if (doubleJumpEnd.x < lowerPlatform.min.x || doubleJumpEnd.x > lowerPlatform.max.x ||
                doubleJumpEnd.y < lowerPlatform.max.y - 0.25f || doubleJumpEnd.y > lowerPlatform.max.y + 0.5f)
                throw new InvalidOperationException("더블점프 종료 마커가 중앙 발판 상단 안에 있지 않습니다.");

            var player = Require(scene, "PlayerRoot");
            var body = RequireComponent<Rigidbody2D>(player);
            var motorDefinition = RequireAsset<PlayerMotorDefinition>(
                "Assets/_Project/GameData/Player/PlayerMotor_Default.asset");
            var gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
            var singleJumpHeight = motorDefinition.JumpVelocity * motorDefinition.JumpVelocity / (2f * gravity);
            var requiredRise = lowerPlatform.max.y - markers["훈련_더블점프_시작"].y;
            if (singleJumpHeight * 2f < requiredRise + 0.25f)
                throw new InvalidOperationException(
                    $"더블점프 도달 높이가 부족합니다: 가능 {singleJumpHeight * 2f:F2}, 필요 {requiredRise:F2}");

            if (markers["훈련_근접적_등장"].y <= markers["훈련_근접적_착지"].y)
                throw new InvalidOperationException("근접 훈련 적 등장점은 착지점보다 높아야 합니다.");

            var rangedRoot = Require(scene, "RangedAttackRoot");
            var ranged = RequireComponent<PlayerRangedAttackHost>(rangedRoot);
            var rangedSerialized = new SerializedObject(ranged);
            var travelDistance = rangedSerialized.FindProperty("travelDistance").floatValue;
            var rangedStartX = markers["훈련_원거리_시작"].x;
            var rangedTargets = new[]
            {
                markers["훈련_원거리_01"].x,
                markers["훈련_원거리_02"].x,
                markers["훈련_원거리_03"].x
            };
            if (!(rangedTargets[0] < rangedTargets[1] && rangedTargets[1] < rangedTargets[2]))
                throw new InvalidOperationException("원거리 표적은 플레이어 진행 방향으로 01→02→03 순서여야 합니다.");
            if (rangedTargets[2] - rangedStartX > travelDistance)
                throw new InvalidOperationException("마지막 원거리 표적이 투사체 사거리 밖에 있습니다.");

            Debug.Log(
                $"[sragon000][훈련장][마커 검증 통과] 방 경계, 단계 순서, 낙하 3점, " +
                $"오른쪽→왼쪽 점프 투사체, 더블점프 높이 {singleJumpHeight * 2f:F2}, " +
                $"근접 낙하, 원거리 사거리 {travelDistance:F1} 정상.");
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
                    Debug.LogWarning($"[sragon000][훈련장] '{TargetScenePath}' 씬을 연 뒤 다시 실행하세요.");
                return;
            }

            try
            {
                var integration = Require(scene, "D_Training_Integration", "D_훈련장_연동");
                integration.name = "D_Training_Integration";
                var stageSystems = Require(scene, "StageSystems");
                var player = Require(scene, "PlayerRoot");
                var trainingController = Require(scene, "TrainingSpawnController");
                trainingController.transform.SetParent(integration.transform, true);
                trainingController.SetActive(true);

                var trainingArea = ConfigureTrainingArea(integration.transform);
                var phaseAreas = ConfigureActionScopes(trainingController, stageSystems, player);
                ConfigureFallingTraining(scene, trainingController, phaseAreas[0]);
                ConfigureJumpTraining(trainingController);
                var exitGate = ConfigureTrainingExitGate(scene, integration.transform);
                ConfigurePlayerAbilities(scene, player);
                var flowHost = ConfigureTrainingFlow(scene, integration.transform, stageSystems, player, trainingArea);
                ConfigureTrainingPhaseController(
                    stageSystems,
                    trainingController,
                    flowHost,
                    phaseAreas,
                    exitGate);
                ConfigureQuestDefinitions();
                ConfigureQuestSequenceAndNarrative(stageSystems);
                ConfigureRangedAttackIntroduction(scene);
                ConfigureObjectiveBeacon(scene, integration.transform);
                ConfigureRestartCheckpoints(stageSystems, integration.transform);

                var marker = GetOrCreateChild(integration.transform, CompletionMarkerName);
                marker.SetActive(false);
                marker.transform.localPosition = Vector3.zero;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAppliedScene(scene);
                Debug.Log(
                    $"[sragon000][훈련장] 1차 연동 완료: 대시→점프→더블점프→기본 공격→원거리 공격, " +
                    $"구역 제한, 실패 재시작, 표적 3기, 기술 부모 영어 명칭을 적용했습니다. flow={flowHost.name}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static BoxCollider2D ConfigureTrainingArea(Transform integration)
        {
            var area = GetOrCreateChild(integration, "TrainingActivationArea");
            area.transform.SetPositionAndRotation(new Vector3(200f, 3f, 0f), Quaternion.identity);
            area.transform.localScale = Vector3.one;
            var collider = area.GetComponent<BoxCollider2D>();
            if (collider == null) collider = area.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.offset = Vector2.zero;
            collider.size = new Vector2(33f, 15f);
            return collider;
        }

        private static void ConfigureFallingTraining(
            Scene scene,
            GameObject controller,
            Collider2D dashArea)
        {
            var host = RequireComponent<TutorialTrainingSpawnHost>(controller);
            var restartPosition = Require(scene, "훈련_대시_재시작").transform.position;
            var restart = GetOrCreateAnchor(controller.transform, "DashTrainingRestartPoint", restartPosition);
            var serialized = new SerializedObject(host);
            serialized.FindProperty("dashRestartPoint").objectReferenceValue = restart;
            serialized.FindProperty("activationArea").objectReferenceValue = dashArea;

            var startPoints = serialized.FindProperty("fallingStartPoints");
            var landingPoints = serialized.FindProperty("fallingLandingPoints");
            var warnings = serialized.FindProperty("fallingWarnings");
            if (startPoints.arraySize != 3 || landingPoints.arraySize != 3)
                throw new InvalidOperationException("낙하물 시작점과 착지점은 각각 3개여야 합니다.");
            for (var index = 0; index < 3; index++)
            {
                var start = startPoints.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                var landing = landingPoints.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                var warning = warnings.GetArrayElementAtIndex(index).objectReferenceValue as GameObject;
                if (start == null || landing == null)
                    throw new InvalidOperationException($"낙하물 앵커 {index + 1} 참조가 비어 있습니다.");
                var marker = Require(scene, $"훈련_낙하_{index + 1:00}").transform.position;
                start.position = new Vector3(marker.x, 9.5f, 0f);
                landing.position = marker;
                if (warning != null) warning.transform.position = marker + new Vector3(0f, 0.18f, 0f);
            }

            var enemySpawn = serialized.FindProperty("enemySpawnPoint").objectReferenceValue as Transform;
            var enemyLanding = serialized.FindProperty("enemyLandingPoint").objectReferenceValue as Transform;
            if (enemySpawn == null || enemyLanding == null)
                throw new InvalidOperationException("기본 공격 훈련 적 앵커가 비어 있습니다.");
            enemySpawn.position = Require(scene, "훈련_근접적_등장").transform.position;
            enemyLanding.position = Require(scene, "훈련_근접적_착지").transform.position;
            var tutorialEnemy = Require(scene, "TutorialEnemy").GetComponent<CombatActorHost>();
            if (tutorialEnemy == null)
                throw new InvalidOperationException("기본 공격 훈련 적의 CombatActorHost가 없습니다.");
            var meleeAreaRoot = tutorialEnemy.transform.parent;
            if (meleeAreaRoot == null)
                throw new InvalidOperationException("기본 공격 훈련 적의 부모 구역이 없습니다.");
            meleeAreaRoot.SetParent(controller.transform, true);
            meleeAreaRoot.gameObject.SetActive(true);
            var enemyActor = new SerializedObject(tutorialEnemy);
            enemyActor.FindProperty("maxHealth").intValue = 100;
            enemyActor.ApplyModifiedPropertiesWithoutUndo();
            serialized.FindProperty("fallingStartDelay").floatValue = 0.3f;
            serialized.FindProperty("fallingWarningDuration").floatValue = 0.45f;
            serialized.FindProperty("fallingDuration").floatValue = 1.15f;
            serialized.FindProperty("fallingStagger").floatValue = 0.5f;
            serialized.FindProperty("fallingWaveDelay").floatValue = 0.75f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureJumpTraining(GameObject trainingController)
        {
            var jumpController = RequireChild(trainingController.transform, "JumpProjectileController");
            jumpController.gameObject.SetActive(true);
            var host = RequireComponent<TutorialJumpTrainingHost>(jumpController.gameObject);
            var serialized = new SerializedObject(host);
            var scene = EditorSceneManager.GetActiveScene();
            SetTransformPosition(serialized, "restartPoint", Require(scene, "훈련_점프_재시작").transform.position);
            SetTransformPosition(serialized, "launchPoint", Require(scene, "훈련_점프_발사").transform.position);
            SetTransformPosition(serialized, "endPoint", Require(scene, "훈련_점프_도착").transform.position);
            serialized.FindProperty("initialDelay").floatValue = 0.55f;
            serialized.FindProperty("travelDuration").floatValue = 2.2f;
            serialized.FindProperty("repeatDelay").floatValue = 0.5f;
            serialized.FindProperty("restartDelay").floatValue = 0.4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Collider2D[] ConfigureActionScopes(
            GameObject trainingController,
            GameObject stageSystems,
            GameObject player)
        {
            var root = RequireChild(trainingController.transform, "TrainingActionScopes").gameObject;
            root.SetActive(true);
            var names = new[] { "Dash", "Jump", "DoubleJump", "Attack", "Ranged" };
            var markerPrefixes = new[] { "대시", "점프", "더블점프", "근접", "원거리" };
            var questIds = TrainingQuestOrder;
            var colliders = new Collider2D[names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                var scope = GetOrCreateChild(root.transform, $"TrainingScope_{names[index]}");
                var scene = EditorSceneManager.GetActiveScene();
                var start = Require(scene, $"훈련_{markerPrefixes[index]}_시작").transform.position;
                var end = Require(scene, $"훈련_{markerPrefixes[index]}_끝").transform.position;
                var minimumX = Mathf.Min(start.x, end.x);
                var maximumX = Mathf.Max(start.x, end.x);
                scope.transform.SetPositionAndRotation(
                    new Vector3((minimumX + maximumX) * 0.5f, 3f, 0f),
                    Quaternion.identity);
                scope.transform.localScale = Vector3.one;
                var collider = scope.GetComponent<BoxCollider2D>();
                if (collider == null) collider = scope.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.offset = Vector2.zero;
                collider.size = new Vector2(Mathf.Max(1.5f, maximumX - minimumX + 0.6f), 15f);
                colliders[index] = collider;
            }

            var legacyPulse = root.transform.Find("TrainingScope_Pulse");
            if (legacyPulse != null) UnityEngine.Object.DestroyImmediate(legacyPulse.gameObject);

            var host = RequireComponent<TutorialTrainingActionScopeHost>(root);
            var serialized = new SerializedObject(host);
            serialized.FindProperty("questManagerHost").objectReferenceValue =
                stageSystems.GetComponent<QuestManagerHost>();
            serialized.FindProperty("player").objectReferenceValue = player.transform;
            var ids = serialized.FindProperty("scopedQuestIds");
            var areas = serialized.FindProperty("scopeAreas");
            ids.arraySize = questIds.Length;
            areas.arraySize = colliders.Length;
            for (var index = 0; index < questIds.Length; index++)
            {
                ids.GetArrayElementAtIndex(index).stringValue = questIds[index];
                areas.GetArrayElementAtIndex(index).objectReferenceValue = colliders[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return colliders;
        }

        private static GameObject ConfigureTrainingExitGate(Scene scene, Transform integration)
        {
            var gateRoot = GetOrCreateChild(integration, "TrainingExitGates");
            var gate = gateRoot.transform.Find("Gate_Block_TrainingExit")?.gameObject;
            if (gate == null)
            {
                gate = gateRoot.transform.Find("Gate_Block_Dash")?.gameObject ??
                       FindSceneObject(scene, "Gate_After_Dash") ??
                       FindSceneObject(scene, "Gate_Block_Dash");
                if (gate == null)
                    throw new InvalidOperationException("단일 훈련장 출구 문으로 사용할 기존 문을 찾지 못했습니다.");
                gate.transform.SetParent(gateRoot.transform, true);
            }

            gate.name = "Gate_Block_TrainingExit";
            gate.SetActive(true);
            var exitPosition = Require(scene, "훈련_종료").transform.position;
            gate.transform.SetPositionAndRotation(new Vector3(exitPosition.x, 3f, 0f), Quaternion.identity);
            gate.transform.localScale = new Vector3(0.32f, 15f, 1f);
            var gateCollider = gate.GetComponent<Collider2D>();
            var gateRenderer = gate.GetComponent<Renderer>();
            if (gateCollider == null || gateRenderer == null)
                throw new InvalidOperationException("단일 훈련장 출구 문에 Collider2D 또는 Renderer가 없습니다.");
            gateCollider.isTrigger = false;
            var legacyHost = gate.GetComponent<TutorialQuestGateHost>();
            if (legacyHost != null) legacyHost.enabled = false;

            foreach (Transform child in gateRoot.transform)
            {
                if (child == gate.transform) continue;
                var childHost = child.GetComponent<TutorialQuestGateHost>();
                if (childHost != null) childHost.enabled = false;
                child.gameObject.SetActive(false);
            }

            return gate;
        }

        private static void ConfigurePlayerAbilities(Scene scene, GameObject player)
        {
            var rangedRoot = Require(scene, "RangedAttackRoot");
            var ranged = RequireComponent<PlayerRangedAttackHost>(rangedRoot);
            var serialized = new SerializedObject(ranged);
            serialized.FindProperty("cooldownSeconds").floatValue = 1.5f;
            serialized.FindProperty("travelDistance").floatValue = 10f;
            serialized.FindProperty("trainingMultiHitTargetCount").intValue = 3;
            serialized.FindProperty("trainingSignalTargetId").stringValue = "PLAYER-001";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var legacyModuleUse = player.GetComponent<TutorialModuleUseHost>();
            if (legacyModuleUse != null) legacyModuleUse.enabled = false;
            var pulse = FindSceneObject(scene, "ModulePulseHitbox");
            if (pulse != null) pulse.SetActive(false);
        }

        private static TutorialImportedTrainingFlowHost ConfigureTrainingFlow(
            Scene scene,
            Transform integration,
            GameObject stageSystems,
            GameObject player,
            Collider2D trainingArea)
        {
            var manager = GetOrCreateChild(integration, "TrainingFlowManager");
            var targetRoot = GetOrCreateChild(manager.transform, "RangedTrainingTargets");
            var sourceEnemy = Require(scene, "TutorialEnemy");
            var targets = new GameObject[3];
            var positions = new[]
            {
                Require(scene, "훈련_원거리_01").transform.position,
                Require(scene, "훈련_원거리_02").transform.position,
                Require(scene, "훈련_원거리_03").transform.position
            };
            for (var index = 0; index < targets.Length; index++)
            {
                var name = $"RangedTarget_{index + 1:00}";
                var target = targetRoot.transform.Find(name)?.gameObject;
                if (target == null)
                {
                    target = UnityEngine.Object.Instantiate(sourceEnemy);
                    target.name = name;
                    target.transform.SetParent(targetRoot.transform, true);
                }
                target.transform.SetPositionAndRotation(positions[index], Quaternion.identity);
                target.transform.localScale = Vector3.one;
                foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = false;
                foreach (var attack in target.GetComponentsInChildren<EnemyAttackHost>(true))
                    attack.enabled = false;
                foreach (var visualMotion in target.GetComponentsInChildren<CombatVisualMotionHost>(true))
                    visualMotion.enabled = false;
                var body = target.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                    body.gravityScale = 0f;
                    body.constraints = RigidbodyConstraints2D.FreezeAll;
                }
                var actor = RequireComponent<CombatActorHost>(target);
                var actorSerialized = new SerializedObject(actor);
                actorSerialized.FindProperty("actorId").stringValue = $"TRAINING-RANGED-{index + 1:00}";
                actorSerialized.FindProperty("maxHealth").intValue = 999;
                actorSerialized.ApplyModifiedPropertiesWithoutUndo();
                target.SetActive(false);
                targets[index] = target;
            }

            var host = manager.GetComponent<TutorialImportedTrainingFlowHost>();
            if (host == null) host = manager.AddComponent<TutorialImportedTrainingFlowHost>();
            var serialized = new SerializedObject(host);
            serialized.FindProperty("serviceRoot").objectReferenceValue =
                stageSystems.GetComponent<Narthex.Core.ServiceRoot>();
            serialized.FindProperty("questSequenceHost").objectReferenceValue =
                stageSystems.GetComponent<TutorialQuestSequenceHost>();
            serialized.FindProperty("playerMotor").objectReferenceValue = player.GetComponent<PlayerMotorHost>();
            serialized.FindProperty("player").objectReferenceValue = player.transform;
            serialized.FindProperty("trainingArea").objectReferenceValue = trainingArea;
            serialized.FindProperty("rangedQuestId").stringValue = "QST-TUTO-005";
            var targetArray = serialized.FindProperty("rangedTargets");
            targetArray.arraySize = targets.Length;
            for (var index = 0; index < targets.Length; index++)
                targetArray.GetArrayElementAtIndex(index).objectReferenceValue = targets[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return host;
        }

        private static void ConfigureTrainingPhaseController(
            GameObject stageSystems,
            GameObject trainingController,
            TutorialImportedTrainingFlowHost flowHost,
            Collider2D[] phaseAreas,
            GameObject exitGate)
        {
            var host = flowHost.GetComponent<TutorialTrainingPhaseControllerHost>();
            if (host == null) host = flowHost.gameObject.AddComponent<TutorialTrainingPhaseControllerHost>();
            var serialized = new SerializedObject(host);
            serialized.FindProperty("serviceRoot").objectReferenceValue =
                stageSystems.GetComponent<Narthex.Core.ServiceRoot>();
            serialized.FindProperty("questSequenceHost").objectReferenceValue =
                stageSystems.GetComponent<TutorialQuestSequenceHost>();

            var questIds = serialized.FindProperty("trainingQuestIds");
            questIds.arraySize = TrainingQuestOrder.Length;
            for (var index = 0; index < TrainingQuestOrder.Length; index++)
                questIds.GetArrayElementAtIndex(index).stringValue = TrainingQuestOrder[index];

            var phaseAreaProperty = serialized.FindProperty("phaseAreas");
            phaseAreaProperty.arraySize = phaseAreas.Length;
            for (var index = 0; index < phaseAreas.Length; index++)
                phaseAreaProperty.GetArrayElementAtIndex(index).objectReferenceValue = phaseAreas[index];

            serialized.FindProperty("exitGateCollider").objectReferenceValue =
                exitGate.GetComponent<Collider2D>();
            serialized.FindProperty("exitGateRenderer").objectReferenceValue =
                exitGate.GetComponent<Renderer>();

            var spawnHost = RequireComponent<TutorialTrainingSpawnHost>(trainingController);
            var spawnSerialized = new SerializedObject(spawnHost);
            CopyReferenceArray(
                spawnSerialized.FindProperty("fallingObjects"),
                serialized.FindProperty("fallingObjects"));
            CopyReferenceArray(
                spawnSerialized.FindProperty("fallingWarnings"),
                serialized.FindProperty("fallingWarnings"));
            var meleeEnemy =
                spawnSerialized.FindProperty("tutorialEnemy").objectReferenceValue as GameObject;
            serialized.FindProperty("meleeEnemy").objectReferenceValue = meleeEnemy;
            serialized.FindProperty("meleeAreaRoot").objectReferenceValue =
                meleeEnemy != null && meleeEnemy.transform.parent != null
                    ? meleeEnemy.transform.parent.gameObject
                    : null;

            var jumpHost = RequireComponent<TutorialJumpTrainingHost>(
                RequireChild(trainingController.transform, "JumpProjectileController").gameObject);
            var jumpSerialized = new SerializedObject(jumpHost);
            serialized.FindProperty("jumpProjectile").objectReferenceValue =
                jumpSerialized.FindProperty("projectile").objectReferenceValue;

            var flowSerialized = new SerializedObject(flowHost);
            CopyReferenceArray(
                flowSerialized.FindProperty("rangedTargets"),
                serialized.FindProperty("rangedTargets"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureQuestDefinitions()
        {
            var doubleJump = RequireAsset<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-006-DOUBLE-JUMP.asset");
            doubleJump.SignalType = QuestSignalType.DoubleJumpPerformed;
            doubleJump.TargetId = "PLAYER-001";
            doubleJump.RequiredAmount = 1;
            EditorUtility.SetDirty(doubleJump);

            var doubleJumpQuest = RequireAsset<QuestDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Quests/QST-TUTO-006.asset");
            doubleJumpQuest.Conditions = new[] { doubleJump };
            doubleJumpQuest.NextQuestIds = new[] { "QST-TUTO-003" };
            EditorUtility.SetDirty(doubleJumpQuest);

            var jumpQuest = RequireAsset<QuestDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Quests/QST-TUTO-002.asset");
            jumpQuest.NextQuestIds = new[] { "QST-TUTO-006" };
            EditorUtility.SetDirty(jumpQuest);

            var meleeCondition = RequireAsset<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-003-ATTACK.asset");
            meleeCondition.SignalType = QuestSignalType.AttackPerformed;
            meleeCondition.TargetId = "PLAYER-001";
            meleeCondition.RequiredAmount = 3;
            EditorUtility.SetDirty(meleeCondition);

            var meleeQuest = RequireAsset<QuestDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Quests/QST-TUTO-003.asset");
            meleeQuest.Conditions = new[] { meleeCondition };
            meleeQuest.NextQuestIds = new[] { "QST-TUTO-005" };
            EditorUtility.SetDirty(meleeQuest);

            var rangedCondition = AssetDatabase.LoadAssetAtPath<QuestConditionDefinition>(RangedConditionPath);
            if (rangedCondition == null)
            {
                rangedCondition = ScriptableObject.CreateInstance<QuestConditionDefinition>();
                rangedCondition.ConfigureIdentity("COND-TUTO-005-RANGED-TRIPLE-HIT");
                AssetDatabase.CreateAsset(rangedCondition, RangedConditionPath);
            }
            rangedCondition.SignalType = QuestSignalType.RangedTripleHitPerformed;
            rangedCondition.TargetId = "PLAYER-001";
            rangedCondition.RequiredAmount = 1;
            EditorUtility.SetDirty(rangedCondition);

            var rangedQuest = RequireAsset<QuestDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Quests/QST-TUTO-005.asset");
            rangedQuest.Conditions = new[] { rangedCondition };
            rangedQuest.NextQuestIds = new[] { "QST-TUTO-007" };
            EditorUtility.SetDirty(rangedQuest);
        }

        private static void ConfigureQuestSequenceAndNarrative(GameObject stageSystems)
        {
            var sequence = RequireComponent<TutorialQuestSequenceHost>(stageSystems);
            var serialized = new SerializedObject(sequence);
            var quests = serialized.FindProperty("questSequence");
            var objectives = serialized.FindProperty("objectiveTexts");
            var definitions = new Dictionary<string, QuestDefinition>();
            var objectiveByQuest = new Dictionary<string, string>();
            for (var index = 0; index < quests.arraySize; index++)
            {
                var definition = quests.GetArrayElementAtIndex(index).objectReferenceValue as QuestDefinition;
                if (definition == null) continue;
                definitions[definition.StableId] = definition;
                objectiveByQuest[definition.StableId] = objectives.GetArrayElementAtIndex(index).stringValue;
            }

            var order = new[]
            {
                "QST-TUTO-001",
                "QST-TUTO-004",
                "QST-TUTO-002",
                "QST-TUTO-006",
                "QST-TUTO-003",
                "QST-TUTO-005",
                "QST-TUTO-007",
                "QST-TUTO-007-A",
                "QST-TUTO-007-B",
                "QST-TUTO-008"
            };
            var trainingObjectives = new Dictionary<string, string>
            {
                ["QST-TUTO-004"] = "훈련장 안에서 낙하물 3회 회피",
                ["QST-TUTO-002"] = "훈련장 안에서 전방 투사체 3회 점프 회피",
                ["QST-TUTO-006"] = "발판을 이용해 더블 점프 1회 성공",
                ["QST-TUTO-003"] = "훈련 적에게 기본 공격 3회 적중",
                ["QST-TUTO-005"] = "2번 원거리 공격 한 발로 표적 3기 동시 관통"
            };
            quests.arraySize = order.Length;
            objectives.arraySize = order.Length;
            for (var index = 0; index < order.Length; index++)
            {
                if (!definitions.TryGetValue(order[index], out var definition))
                    throw new InvalidOperationException($"퀘스트 정의가 씬 순서에 없습니다: {order[index]}");
                quests.GetArrayElementAtIndex(index).objectReferenceValue = definition;
                objectives.GetArrayElementAtIndex(index).stringValue =
                    trainingObjectives.TryGetValue(order[index], out var trainingObjective)
                        ? trainingObjective
                        : objectiveByQuest[order[index]];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var narrative = RequireComponent<TutorialNarrativeSequenceHost>(stageSystems);
            var narrativeSerialized = new SerializedObject(narrative);
            var beats = narrativeSerialized.FindProperty("beats");
            for (var index = 0; index < beats.arraySize; index++)
            {
                var beat = beats.GetArrayElementAtIndex(index);
                var questId = beat.FindPropertyRelative("questId").stringValue;
                if (questId == "QST-TUTO-006")
                {
                    SetLines(beat,
                        "테우스: 부츠 동기화 완료. 이제 공중에서 SPACE를 한 번 더 눌러 더블 점프할 수 있어.",
                        "테우스: 앞의 높은 발판을 향해 더블 점프해 봐.");
                    beat.FindPropertyRelative("deferUntilPortalTargetId").stringValue = string.Empty;
                }
                else if (questId == "QST-TUTO-003")
                {
                    SetLines(beat,
                        "테우스: 다음은 기본 공격이야. 훈련 표적 가까이 이동해.",
                        "테우스: 마우스 왼쪽 버튼을 0.5초 안에 연속 입력해서 1·2·3단 공격을 모두 적중시켜.");
                }
                else if (questId == "QST-TUTO-005")
                {
                    SetLines(beat,
                        "테우스: 마지막은 원거리 공격이야.",
                        "테우스: 2번 키를 누르면 바라보는 방향으로 관통 투사체를 발사해. 재사용 대기시간은 1.5초야.",
                        "테우스: 위치를 맞춰 한 발로 표적 세 기를 모두 관통해 봐.");
                }
                else if (questId == "QST-TUTO-007")
                {
                    SetLines(beat,
                        "경보음: 본부 외곽에서 다수의 적성 반응 감지.",
                        "테우스: 프로메, 훈련 중단! 판도라 유닛이 본부를 습격하고 있어.",
                        "테우스: 회의장으로 돌아가자. 서둘러!");
                    beat.FindPropertyRelative("deferUntilPortalTargetId").stringValue = string.Empty;
                }
            }
            narrativeSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRangedAttackIntroduction(Scene scene)
        {
            var presenter = FindSceneComponent<TutorialDialoguePresenter>(scene);
            if (presenter == null)
                throw new InvalidOperationException("TutorialDialoguePresenter를 찾지 못했습니다.");

            var serializedPresenter = new SerializedObject(presenter);
            var definitions = serializedPresenter.FindProperty("introductionDefinitions");
            var found = false;
            for (var index = 0; index < definitions.arraySize; index++)
            {
                var definition = definitions.GetArrayElementAtIndex(index);
                if (definition.FindPropertyRelative("questId").stringValue != "QST-TUTO-005") continue;
                definition.FindPropertyRelative("displayName").stringValue = "원거리 공격";
                definition.FindPropertyRelative("englishName").stringValue = "Ranged Attack";
                definition.FindPropertyRelative("description").stringValue =
                    "프로메의 무기에 내장된 기본 원거리 공격.\n" +
                    "2번 키를 누르면 현재 바라보는 방향으로 관통 투사체를 발사한다.";
                found = true;
                break;
            }

            if (!found)
                throw new InvalidOperationException("QST-TUTO-005 원거리 공격 소개 정의를 찾지 못했습니다.");
            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

            var introductionCard = FindSceneComponent<DialogueIntroductionCardModule>(scene);
            if (introductionCard == null)
                throw new InvalidOperationException("DialogueIntroductionCardModule을 찾지 못했습니다.");
            var serializedCard = new SerializedObject(introductionCard);
            serializedCard.FindProperty("promptDelay").floatValue = 1f;
            serializedCard.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureObjectiveBeacon(Scene scene, Transform integration)
        {
            var beacon = FindSceneComponent<TutorialObjectiveBeaconHost>(scene);
            if (beacon == null) throw new InvalidOperationException("TutorialObjectiveBeaconHost를 찾지 못했습니다.");
            var serialized = new SerializedObject(beacon);
            serialized.FindProperty("equipmentGuidanceEnabled").boolValue = false;
            var targets = serialized.FindProperty("targets");
            var targetMarkers = new Dictionary<string, string>
            {
                ["QST-TUTO-004"] = "훈련_대시_시작",
                ["QST-TUTO-002"] = "훈련_점프_시작",
                ["QST-TUTO-006"] = "훈련_더블점프_끝",
                ["QST-TUTO-003"] = "훈련_근접_시작",
                ["QST-TUTO-005"] = "훈련_원거리_시작"
            };
            foreach (var pair in targetMarkers)
            {
                var anchor = GetOrCreateAnchor(
                    integration,
                    $"Objective_{pair.Key}",
                    Require(scene, pair.Value).transform.position);
                SetBeaconTarget(targets, pair.Key, anchor);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRestartCheckpoints(GameObject stageSystems, Transform integration)
        {
            var restart = RequireComponent<TutorialRestartHost>(stageSystems);
            var serialized = new SerializedObject(restart);
            var checkpoints = serialized.FindProperty("questCheckpoints");
            var markerNames = new Dictionary<string, string>
            {
                ["QST-TUTO-004"] = "훈련_대시_재시작",
                ["QST-TUTO-002"] = "훈련_점프_재시작",
                ["QST-TUTO-006"] = "훈련_더블점프_시작",
                ["QST-TUTO-003"] = "훈련_근접_시작",
                ["QST-TUTO-005"] = "훈련_원거리_시작"
            };
            var scene = EditorSceneManager.GetActiveScene();
            for (var index = 0; index < checkpoints.arraySize; index++)
            {
                var checkpoint = checkpoints.GetArrayElementAtIndex(index);
                var questId = checkpoint.FindPropertyRelative("questId").stringValue;
                if (!markerNames.TryGetValue(questId, out var markerName)) continue;
                var position = Require(scene, markerName).transform.position;
                var anchor = GetOrCreateAnchor(integration, $"Restart_{questId}", position);
                checkpoint.FindPropertyRelative("spawnPoint").objectReferenceValue = anchor;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateAppliedScene(Scene scene)
        {
            var controller = Require(scene, "TrainingSpawnController");
            var spawn = RequireComponent<TutorialTrainingSpawnHost>(controller);
            var jump = RequireComponent<TutorialJumpTrainingHost>(Require(scene, "JumpProjectileController"));
            var scopes = RequireComponent<TutorialTrainingActionScopeHost>(Require(scene, "TrainingActionScopes"));
            var flow = RequireComponent<TutorialImportedTrainingFlowHost>(Require(scene, "TrainingFlowManager"));
            var phaseController = RequireComponent<TutorialTrainingPhaseControllerHost>(
                Require(scene, "TrainingFlowManager"));
            var ranged = RequireComponent<PlayerRangedAttackHost>(Require(scene, "RangedAttackRoot"));
            var gateRoot = Require(scene, "TrainingExitGates");
            var singleExitGate = Require(scene, "Gate_Block_TrainingExit");
            var activeLegacyGates = FindSceneComponents<TutorialQuestGateHost>(scene)
                .Where(host => host.transform.IsChildOf(gateRoot.transform) &&
                               host.enabled && host.gameObject.activeInHierarchy)
                .ToArray();

            if (!controller.activeInHierarchy || !spawn.HasValidSetup || !jump.HasValidSetup ||
                !scopes.HasValidSetup || !flow.HasValidSetup || !phaseController.HasValidSetup)
                throw new InvalidOperationException("수정 훈련장 핵심 로직 중 유효하지 않거나 비활성인 항목이 있습니다.");
            if (!ranged.HasValidSetup || !ranged.HasAssignedInput)
                throw new InvalidOperationException("원거리 공격의 2번 입력 또는 투사체 참조가 유효하지 않습니다.");
            if (singleExitGate.GetComponent<Collider2D>() == null ||
                singleExitGate.GetComponent<Renderer>() == null ||
                activeLegacyGates.Length != 0)
                throw new InvalidOperationException("훈련장에는 단계 컨트롤러가 관리하는 단일 출구 문만 활성화되어야 합니다.");

            var sequence = RequireComponent<TutorialQuestSequenceHost>(Require(scene, "StageSystems"));
            var sequenceSerialized = new SerializedObject(sequence);
            var quests = sequenceSerialized.FindProperty("questSequence");
            var actualTrainingOrder = new string[TrainingQuestOrder.Length];
            for (var index = 0; index < actualTrainingOrder.Length; index++)
            {
                var definition = quests.GetArrayElementAtIndex(index + 1).objectReferenceValue as QuestDefinition;
                actualTrainingOrder[index] = definition != null ? definition.StableId : string.Empty;
            }
            if (!actualTrainingOrder.SequenceEqual(TrainingQuestOrder))
                throw new InvalidOperationException(
                    $"훈련 퀘스트 순서가 다릅니다: {string.Join(" → ", actualTrainingOrder)}");

            Debug.Log(
                "[sragon000][훈련장][검증 통과] 대시→점프→더블점프→기본 공격→원거리 공격, " +
                "단계별 행동 범위 1개만 활성화, 단일 출구 문, 이전 단계 정리, " +
                "원거리 표적 3개, 체크포인트와 입력 참조 정상.");
        }

        private static void SetLines(SerializedProperty beat, params string[] values)
        {
            var lines = beat.FindPropertyRelative("lines");
            lines.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                lines.GetArrayElementAtIndex(index).stringValue = values[index];
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
            var newIndex = targets.arraySize;
            targets.InsertArrayElementAtIndex(newIndex);
            var entry = targets.GetArrayElementAtIndex(newIndex);
            entry.FindPropertyRelative("questId").stringValue = questId;
            entry.FindPropertyRelative("target").objectReferenceValue = target;
        }

        private static void CopyReferenceArray(SerializedProperty source, SerializedProperty destination)
        {
            destination.arraySize = source.arraySize;
            for (var index = 0; index < source.arraySize; index++)
                destination.GetArrayElementAtIndex(index).objectReferenceValue =
                    source.GetArrayElementAtIndex(index).objectReferenceValue;
        }

        private static void SetTransformPosition(SerializedObject serialized, string propertyName, Vector3 position)
        {
            var target = serialized.FindProperty(propertyName).objectReferenceValue as Transform;
            if (target == null) throw new InvalidOperationException($"{propertyName} Transform 참조가 비어 있습니다.");
            target.position = position;
        }

        private static Transform GetOrCreateAnchor(Transform parent, string name, Vector3 worldPosition)
        {
            var anchor = GetOrCreateChild(parent, name);
            anchor.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            anchor.transform.localScale = Vector3.one;
            return anchor.transform;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private static Transform RequireChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) throw new InvalidOperationException($"{parent.name}/{name}을 찾지 못했습니다.");
            return child;
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

        private static T RequireComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject != null ? gameObject.GetComponent<T>() : null;
            if (component == null)
                throw new InvalidOperationException($"{gameObject?.name ?? "null"}에 {typeof(T).Name}이 없습니다.");
            return component;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException($"필수 에셋을 찾지 못했습니다: {path}");
            return asset;
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
            return FindSceneComponents<T>(scene).FirstOrDefault();
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
