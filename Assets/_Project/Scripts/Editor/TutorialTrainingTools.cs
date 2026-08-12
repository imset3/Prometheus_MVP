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
    public static class TutorialTrainingTools
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string CompletionMarkerName = "D07_노션진행HUD동기화완료";
        private const string DashFinishSignalId = "TRAINING-DASH-FINISH";
        private const string DoubleJumpFinishSignalId = "TRAINING-DOUBLE-JUMP-SUMMIT";
        private const string RangedConditionPath =
            "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-005-RANGED-TRIPLE-HIT.asset";

        private static readonly string[] TrainingQuestOrder =
        {
            "QST-TUTO-004",
            "QST-TUTO-006",
            "QST-TUTO-002",
            "QST-TUTO-003",
            "QST-TUTO-005"
        };
        private const string ImportedTrainingPlayModeTestName =
            "Narthex.PlayModeTests.TutorialSceneRuntimeSmokeTests." +
            "ImportedTrainingRoom_RunsFiveSequentialLessonsWithRetryAndScopeProtection";
        private const string ImportedFullTutorialPlayModeTestName =
            "Narthex.PlayModeTests.TutorialSceneRuntimeSmokeTests." +
            "TrainingThroughHelte_CompletesTheTutorialThroughLiveSceneSystems";
        private const string HiddenRoomPlayModeTestName =
            "Narthex.PlayModeTests.TutorialSceneRuntimeSmokeTests." +
            "Chapter0Intro_ReachesTheTrainingRoomThroughThePasskeyRoute";
        private const string GWindRoutePlayModeTestName =
            "Narthex.PlayModeTests.TutorialSceneRuntimeSmokeTests." +
            "GWindRoute_LiftsPromeThroughAllColumnsAndReachesHNormally";
        private static TestRunnerApi trainingTestRunnerApi;
        private static string trainingTestRunGuid = string.Empty;
        private static double trainingTestStartedAt;
        private static string runningTestLabel = "훈련장";

        [MenuItem(PrometheusToolMenuPaths.Legacy + "Apply Training Integration")]
        public static void ApplyFromMenu()
        {
            Apply();
        }

        [MenuItem(PrometheusToolMenuPaths.Tests + "Training Runtime Smoke")]
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

                    if (TrainingQuestOrder[index] == "QST-TUTO-003")
                    {
                        var dummy = FindSceneComponents<CombatActorHost>(scene)
                            .Single(actor => actor.gameObject.name == "TutorialEnemy");
                        var dummyCollider = dummy.GetComponent<Collider2D>();
                        var hasVisibleRenderer = dummy.GetComponentsInChildren<Renderer>(true)
                            .Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy);
                        if (!dummy.gameObject.activeInHierarchy || dummyCollider == null ||
                            !dummyCollider.enabled || !hasVisibleRenderer)
                            throw new InvalidOperationException(
                                "근접 훈련 마네킹이 활성·충돌·렌더링 상태로 준비되지 않았습니다.");
                    }
                }

                controller.Refresh("QST-TUTO-007");
                if (controller.CurrentPhaseIndex != -1 ||
                    controller.ActivePhaseAreaCount != 0 ||
                    controller.IsExitLocked)
                    throw new InvalidOperationException(
                        "훈련 종료 전환 실패: 모든 훈련 범위가 꺼지고 출구가 열려야 합니다.");

                Debug.Log(
                    "[sragon000][훈련장][런타임 스모크 통과] 5개 단계 각각 행동 범위 1개, " +
                    "근접 마네킹 표시·충돌, 훈련 중 출구 잠금, 완료 후 전 범위 비활성·출구 개방 정상.");
                controller.RefreshCurrentQuest();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem(PrometheusToolMenuPaths.Tests + "Imported Training")]
        public static void RunImportedTrainingPlayModeTest()
        {
            RunImportedPlayModeTest(ImportedTrainingPlayModeTestName, "훈련장");
        }

        [MenuItem(PrometheusToolMenuPaths.Tests + "Full Tutorial")]
        public static void RunImportedFullTutorialPlayModeTest()
        {
            RunImportedPlayModeTest(ImportedFullTutorialPlayModeTestName, "전체 튜토리얼");
        }

        [MenuItem(PrometheusToolMenuPaths.Tests + "Hidden Room")]
        public static void RunHiddenRoomPlayModeTest()
        {
            RunImportedPlayModeTest(HiddenRoomPlayModeTestName, "숨겨진 방");
        }

        [MenuItem(PrometheusToolMenuPaths.Tests + "G Wind To H")]
        public static void RunGWindRoutePlayModeTest()
        {
            RunImportedPlayModeTest(GWindRoutePlayModeTestName, "G→H");
        }

        private static void RunImportedPlayModeTest(string testName, string label)
        {
            if (trainingTestRunnerApi != null)
            {
                // PlayMode 진입 중에는 현재 실행을 보존하고, 편집 모드로 돌아온 뒤에도
                // 콜백이 남아 있다면 완료 콜백을 놓친 핸들이므로 즉시 정리한다.
                if (!EditorApplication.isPlayingOrWillChangePlaymode ||
                    EditorApplication.timeSinceStartup - trainingTestStartedAt > 15d)
                {
                    if (!string.IsNullOrEmpty(trainingTestRunGuid))
                        TestRunnerApi.CancelTestRun(trainingTestRunGuid);
                    UnityEngine.Object.DestroyImmediate(trainingTestRunnerApi);
                    trainingTestRunnerApi = null;
                    trainingTestRunGuid = string.Empty;
                    Debug.LogWarning("[sragon000][플레이 테스트] 응답 없는 이전 테스트 핸들을 정리했습니다.");
                }
                else
                {
                    Debug.LogWarning($"[sragon000][{runningTestLabel}][플레이 테스트] 이미 테스트가 실행 중입니다.");
                    return;
                }
            }

            runningTestLabel = label;
            trainingTestRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            trainingTestStartedAt = EditorApplication.timeSinceStartup;
            trainingTestRunnerApi.RegisterCallbacks(new ImportedTrainingTestCallbacks());
            trainingTestRunGuid = trainingTestRunnerApi.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { testName },
                assemblyNames = new[] { "Narthex.PlayModeTests" }
            }));
            Debug.Log($"[sragon000][{label}][플레이 테스트] 새 레벨 씬 통합 테스트를 시작합니다.");
        }

        [MenuItem(PrometheusToolMenuPaths.Tests + "Reset Stale Test Runner")]
        public static void ResetStalePlayModeTestRunner()
        {
            if (!string.IsNullOrEmpty(trainingTestRunGuid))
                TestRunnerApi.CancelTestRun(trainingTestRunGuid);
            if (trainingTestRunnerApi != null)
                UnityEngine.Object.DestroyImmediate(trainingTestRunnerApi);
            trainingTestRunnerApi = null;
            trainingTestRunGuid = string.Empty;
            trainingTestStartedAt = 0d;
            runningTestLabel = string.Empty;
            Debug.Log("[sragon000][플레이 테스트] 테스트 러너 핸들을 초기화했습니다.");
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
                trainingTestRunGuid = string.Empty;
                trainingTestStartedAt = 0d;
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

        [MenuItem(PrometheusToolMenuPaths.Analysis + "Print Training Structure")]
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

        [MenuItem(PrometheusToolMenuPaths.Legacy + "Create Training Markers")]
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
            markerRoot.tag = "Untagged";

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

                ("02_더블점프", "훈련_더블점프_시작", new Vector3(201f, -3.9f, 0f), 2),
                ("02_더블점프", "훈련_더블점프_끝", new Vector3(206f, -3.9f, 0f), 2),

                ("03_점프", "훈련_점프_시작", new Vector3(195f, -3.9f, 0f), 3),
                ("03_점프", "훈련_점프_끝", new Vector3(201f, -3.9f, 0f), 3),
                ("03_점프", "훈련_점프_발사", new Vector3(201f, -3.2f, 0f), 3),
                ("03_점프", "훈련_점프_도착", new Vector3(195f, -3.2f, 0f), 3),
                ("03_점프", "훈련_점프_재시작", new Vector3(195f, -3.9f, 0f), 3),

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
                group.tag = "Untagged";
                var existing = group.transform.Find(definition.Item2);
                var marker = existing != null ? existing.gameObject : new GameObject(definition.Item2);
                if (existing == null)
                {
                    marker.transform.SetParent(group.transform, true);
                    marker.transform.SetPositionAndRotation(definition.Item3, Quaternion.identity);
                    marker.transform.localScale = Vector3.one;
                    createdCount++;
                }
                marker.tag = "Untagged";
                ConfigureFunctionMarker(
                    marker,
                    $"TRAINING-{definition.Item2}",
                    ResolveTrainingMarkerKind(definition.Item2));
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

        [MenuItem(PrometheusToolMenuPaths.Legacy + "Reset Training Marker Layout")]
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
                ["훈련_대시_끝"] = new(215.2f, -3.9f, 0f),
                ["훈련_낙하_01"] = new(187f, -4.25f, 0f),
                ["훈련_낙하_02"] = new(189.6f, -4.25f, 0f),
                ["훈련_낙하_03"] = new(192.2f, -4.25f, 0f),
                ["훈련_대시_재시작"] = new(185.5f, -3.9f, 0f),

                ["훈련_점프_시작"] = new(192.5f, -3.9f, 0f),
                ["훈련_점프_끝"] = new(198.5f, -3.9f, 0f),
                ["훈련_점프_발사"] = new(215.7f, -3.25f, 0f),
                ["훈련_점프_도착"] = new(184.8f, -3.25f, 0f),
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

        [MenuItem(PrometheusToolMenuPaths.Validation + "Validate Training Marker Layout")]
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

            var dashMinimumX = markers["훈련_대시_시작"].x;
            var dashMaximumX = markers["훈련_대시_끝"].x;
            foreach (var name in new[] { "훈련_낙하_01", "훈련_낙하_02", "훈련_낙하_03" })
                if (markers[name].x < dashMinimumX || markers[name].x > dashMaximumX)
                    throw new InvalidOperationException($"{name}이 대시 훈련 구간 밖에 있습니다.");

            if (markers["훈련_점프_발사"].x <= markers["훈련_점프_도착"].x)
                throw new InvalidOperationException("점프 회피 투사체는 플레이어 진행 방향의 반대인 오른쪽→왼쪽으로 이동해야 합니다.");

            var platformRenderers = renderers
                .Concat(RequireChild(
                    Require(scene, "TrainingPhaseContents").transform,
                    "02_더블점프").GetComponentsInChildren<Renderer>(true))
                .Where(IsTrainingPlatform)
                .Distinct()
                .ToArray();
            if (platformRenderers.Length == 0)
                throw new InvalidOperationException("더블점프 착지용 높은 발판을 찾지 못했습니다.");
            var highestPlatform = platformRenderers
                .OrderByDescending(renderer => renderer.bounds.max.y)
                .First()
                .bounds;
            var doubleJumpEnd = markers["훈련_더블점프_끝"];
            if (doubleJumpEnd.x < highestPlatform.min.x || doubleJumpEnd.x > highestPlatform.max.x ||
                doubleJumpEnd.y < highestPlatform.max.y ||
                doubleJumpEnd.y > highestPlatform.max.y + 1.5f)
                throw new InvalidOperationException("더블점프 종료 마커가 최고 발판 상단에 있지 않습니다.");

            var player = Require(scene, "PlayerRoot");
            var body = RequireComponent<Rigidbody2D>(player);
            var motorDefinition = RequireAsset<PlayerMotorDefinition>(
                "Assets/_Project/GameData/Player/PlayerMotor_Default.asset");
            var gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
            var singleJumpHeight = motorDefinition.JumpVelocity * motorDefinition.JumpVelocity / (2f * gravity);
            var reachableHeight = markers["훈련_더블점프_시작"].y;
            foreach (var platform in platformRenderers
                         .Select(renderer => renderer.bounds)
                         .OrderBy(bounds => bounds.max.y))
            {
                var requiredRise = platform.max.y - reachableHeight;
                if (requiredRise > singleJumpHeight * 2f - 0.25f)
                    throw new InvalidOperationException(
                        $"더블점프 발판 간 높이가 너무 큽니다: 가능 {singleJumpHeight * 2f:F2}, " +
                        $"필요 {requiredRise:F2}, 발판={platform.center}");
                reachableHeight = Mathf.Max(reachableHeight, platform.max.y);
            }

            if (Vector2.Distance(
                    markers["훈련_근접적_등장"],
                    markers["훈련_근접적_착지"]) > 0.05f)
                throw new InvalidOperationException("정지 허수아비의 등장점과 착지점은 같아야 합니다.");

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
                $"정지 허수아비, 원거리 사거리 {travelDistance:F1} 정상.");
        }

        private static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
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
                    $"[sragon000][훈련장] 마커 연동 완료: 대시→더블점프→점프→기본 공격→원거리 공격, " +
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
            var serialized = new SerializedObject(host);
            serialized.FindProperty("fallingQuestId").stringValue = "QST-TUTO-DISABLED-FALLING";
            serialized.FindProperty("dashRestartPoint").objectReferenceValue =
                Require(scene, "훈련_대시_재시작").transform;
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
            serialized.FindProperty("enemySpawnPoint").objectReferenceValue =
                Require(scene, "훈련_근접적_등장").transform;
            serialized.FindProperty("enemyLandingPoint").objectReferenceValue =
                Require(scene, "훈련_근접적_착지").transform;
            var meleeDummyVisuals = Require(scene, "근접공격훈련")
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.gameObject.name.StartsWith("Enemy", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();
            if (meleeDummyVisuals.Length != 1)
                throw new InvalidOperationException(
                    $"근접공격훈련에는 원형 허수아비 도형이 정확히 1개여야 합니다. 현재={meleeDummyVisuals.Length}");
            var meleePosition = meleeDummyVisuals[0].bounds.center;
            Require(scene, "훈련_근접적_등장").transform.position = meleePosition;
            Require(scene, "훈련_근접적_착지").transform.position = meleePosition;
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
            foreach (var renderer in tutorialEnemy.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            serialized.FindProperty("stationaryEnemy").boolValue = true;
            serialized.FindProperty("fallingStartDelay").floatValue = 0.3f;
            serialized.FindProperty("fallingWarningDuration").floatValue = 0.45f;
            serialized.FindProperty("fallingDuration").floatValue = 1.15f;
            serialized.FindProperty("fallingStagger").floatValue = 0.5f;
            serialized.FindProperty("fallingWaveDelay").floatValue = 0.75f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureJumpTraining(GameObject trainingController)
        {
            var jumpController = Require(
                EditorSceneManager.GetActiveScene(),
                "JumpProjectileController").transform;
            jumpController.gameObject.SetActive(true);
            var host = RequireComponent<TutorialJumpTrainingHost>(jumpController.gameObject);
            var serialized = new SerializedObject(host);
            var scene = EditorSceneManager.GetActiveScene();
            var trainingRenderers = Require(scene, "훈련장 수정버전")
                .GetComponentsInChildren<Renderer>(true);
            var roomBounds = trainingRenderers[0].bounds;
            for (var index = 1; index < trainingRenderers.Length; index++)
                roomBounds.Encapsulate(trainingRenderers[index].bounds);
            Require(scene, "훈련_점프_발사").transform.position =
                new Vector3(roomBounds.max.x - 1.8f, -3.25f, 0f);
            Require(scene, "훈련_점프_도착").transform.position =
                new Vector3(roomBounds.min.x + 1.8f, -3.25f, 0f);
            serialized.FindProperty("restartPoint").objectReferenceValue =
                Require(scene, "훈련_점프_재시작").transform;
            serialized.FindProperty("launchPoint").objectReferenceValue =
                Require(scene, "훈련_점프_발사").transform;
            serialized.FindProperty("endPoint").objectReferenceValue =
                Require(scene, "훈련_점프_도착").transform;
            var sourceProjectile = serialized.FindProperty("projectile").objectReferenceValue as GameObject;
            if (sourceProjectile == null)
                throw new InvalidOperationException("점프 회피 훈련의 원본 투사체가 없습니다.");
            var poolRoot = GetOrCreateChild(jumpController, "JumpProjectilePool");
            const int poolSize = 5;
            var projectileObjects = new GameObject[poolSize];
            var projectileBodies = new Rigidbody2D[poolSize];
            var projectileHazards = new TutorialJumpProjectileHazardHost[poolSize];
            for (var index = 0; index < poolSize; index++)
            {
                var current = index == 0
                    ? sourceProjectile
                    : poolRoot.transform.Find($"JumpProjectile_{index + 1:00}")?.gameObject;
                if (current == null)
                {
                    current = UnityEngine.Object.Instantiate(sourceProjectile, poolRoot.transform);
                    current.name = $"JumpProjectile_{index + 1:00}";
                }
                else
                {
                    current.transform.SetParent(poolRoot.transform, true);
                }
                current.SetActive(false);
                projectileObjects[index] = current;
                projectileBodies[index] = RequireComponent<Rigidbody2D>(current);
                projectileHazards[index] = RequireComponent<TutorialJumpProjectileHazardHost>(current);
            }
            var projectilePool = serialized.FindProperty("projectilePool");
            var bodyPool = serialized.FindProperty("projectileBodyPool");
            var hazardPool = serialized.FindProperty("projectileHazardPool");
            projectilePool.arraySize = poolSize;
            bodyPool.arraySize = poolSize;
            hazardPool.arraySize = poolSize;
            for (var index = 0; index < poolSize; index++)
            {
                projectilePool.GetArrayElementAtIndex(index).objectReferenceValue = projectileObjects[index];
                bodyPool.GetArrayElementAtIndex(index).objectReferenceValue = projectileBodies[index];
                hazardPool.GetArrayElementAtIndex(index).objectReferenceValue = projectileHazards[index];
            }
            serialized.FindProperty("initialDelay").floatValue = 0.75f;
            serialized.FindProperty("travelDuration").floatValue = 2.8f;
            serialized.FindProperty("launchInterval").floatValue = 1f;
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
            var names = new[] { "Dash", "DoubleJump", "Jump", "Attack", "Ranged" };
            var questIds = TrainingQuestOrder;
            var colliders = new Collider2D[names.Length];
            var trainingArea = Require(EditorSceneManager.GetActiveScene(), "TrainingActivationArea")
                .GetComponent<BoxCollider2D>();
            if (trainingArea == null)
                throw new InvalidOperationException("TrainingActivationArea의 BoxCollider2D가 없습니다.");
            for (var index = 0; index < names.Length; index++)
            {
                var scope = GetOrCreateChild(root.transform, $"TrainingScope_{names[index]}");
                scope.transform.SetPositionAndRotation(
                    new Vector3(trainingArea.bounds.center.x, trainingArea.bounds.center.y, 0f),
                    Quaternion.identity);
                scope.transform.localScale = Vector3.one;
                var collider = scope.GetComponent<BoxCollider2D>();
                if (collider == null) collider = scope.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.offset = Vector2.zero;
                collider.size = trainingArea.bounds.size;
                ConfigureFunctionMarker(
                    scope,
                    $"TRAINING-SCOPE-{names[index].ToUpperInvariant()}",
                    TutorialFunctionMarkerKind.Point);
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
            var importedTargetRenderers = Require(scene, "원거리공격훈련")
                .GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.gameObject.name.StartsWith("Enemy", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();
            if (importedTargetRenderers.Length != 3)
                throw new InvalidOperationException(
                    $"원거리공격훈련에는 원형 허수아비 도형이 정확히 3개여야 합니다. 현재={importedTargetRenderers.Length}");
            var targets = new GameObject[3];
            var targetRenderers = new Renderer[3];
            var positions = importedTargetRenderers
                .Select(renderer => renderer.bounds.center)
                .ToArray();
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
                targetRenderers[index] = importedTargetRenderers[index];
                Require(scene, $"훈련_원거리_{index + 1:00}").transform.position = positions[index];
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
            var rendererArray = serialized.FindProperty("rangedTargetRenderers");
            rendererArray.arraySize = targetRenderers.Length;
            for (var index = 0; index < targetRenderers.Length; index++)
                rendererArray.GetArrayElementAtIndex(index).objectReferenceValue = targetRenderers[index];
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
            serialized.FindProperty("questManagerHost").objectReferenceValue =
                stageSystems.GetComponent<QuestManagerHost>();

            var scene = EditorSceneManager.GetActiveScene();
            var player = Require(scene, "PlayerRoot");
            serialized.FindProperty("playerInputHost").objectReferenceValue =
                player.GetComponent<PlayerInputHost>();
            serialized.FindProperty("playerMotor").objectReferenceValue =
                player.GetComponent<PlayerMotorHost>();
            serialized.FindProperty("player").objectReferenceValue = player.transform;
            serialized.FindProperty("playerBody").objectReferenceValue =
                player.GetComponent<Rigidbody2D>();
            serialized.FindProperty("fadeCanvasGroup").objectReferenceValue =
                Require(scene, "TutorialZoneFadeOverlay").GetComponent<CanvasGroup>();

            var questIds = serialized.FindProperty("trainingQuestIds");
            questIds.arraySize = TrainingQuestOrder.Length;
            for (var index = 0; index < TrainingQuestOrder.Length; index++)
                questIds.GetArrayElementAtIndex(index).stringValue = TrainingQuestOrder[index];

            var phaseAreaProperty = serialized.FindProperty("phaseAreas");
            phaseAreaProperty.arraySize = phaseAreas.Length;
            for (var index = 0; index < phaseAreas.Length; index++)
                phaseAreaProperty.GetArrayElementAtIndex(index).objectReferenceValue = phaseAreas[index];

            var phaseRoots = ConfigurePhaseContentRoots(scene, trainingController, flowHost, host, player);
            var phaseRootProperty = serialized.FindProperty("phaseContentRoots");
            phaseRootProperty.arraySize = phaseRoots.Length;
            for (var index = 0; index < phaseRoots.Length; index++)
                phaseRootProperty.GetArrayElementAtIndex(index).objectReferenceValue = phaseRoots[index];

            var commonStart = Require(scene, "훈련_진입").transform;
            ConfigureFunctionMarker(
                commonStart.gameObject,
                "TRAINING-COMMON-START",
                TutorialFunctionMarkerKind.TrainingStart);
            var startMarkers = serialized.FindProperty("phaseStartMarkers");
            startMarkers.arraySize = TrainingQuestOrder.Length;
            for (var index = 0; index < TrainingQuestOrder.Length; index++)
                startMarkers.GetArrayElementAtIndex(index).objectReferenceValue = commonStart;

            serialized.FindProperty("exitGateCollider").objectReferenceValue =
                exitGate.GetComponent<Collider2D>();
            serialized.FindProperty("exitGateRenderer").objectReferenceValue =
                exitGate.GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject[] ConfigurePhaseContentRoots(
            Scene scene,
            GameObject trainingController,
            TutorialImportedTrainingFlowHost flowHost,
            TutorialTrainingPhaseControllerHost phaseController,
            GameObject player)
        {
            var contentRoot = GetOrCreateChild(trainingController.transform, "TrainingPhaseContents");
            var roots = new[]
            {
                GetOrCreateChild(contentRoot.transform, "01_대시"),
                GetOrCreateChild(contentRoot.transform, "02_더블점프"),
                GetOrCreateChild(contentRoot.transform, "03_점프"),
                GetOrCreateChild(contentRoot.transform, "04_근접공격"),
                GetOrCreateChild(contentRoot.transform, "05_원거리공격")
            };

            var trainingLevel = Require(scene, "훈련장 수정버전");
            var floorRenderer = trainingLevel.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.bounds.size.x >= 20f && renderer.bounds.center.y < 0f)
                .OrderBy(renderer => renderer.bounds.center.y)
                .FirstOrDefault();
            if (floorRenderer == null)
                throw new InvalidOperationException("불기둥 높이 기준으로 사용할 훈련장 바닥을 찾지 못했습니다.");
            var floorTop = floorRenderer.bounds.max.y;
            const float fireWidth = 0.55f;
            const float fireHeight = 8.5f;
            var dashFinish = Require(scene, "훈련_대시_끝");
            var roomRenderers = trainingLevel.GetComponentsInChildren<Renderer>(true);
            var trainingBounds = roomRenderers[0].bounds;
            for (var index = 1; index < roomRenderers.Length; index++)
                trainingBounds.Encapsulate(roomRenderers[index].bounds);
            dashFinish.transform.position =
                new Vector3(trainingBounds.max.x - 2.3f, dashFinish.transform.position.y, 0f);
            dashFinish.tag = "Untagged";
            ConfigureFunctionMarker(
                dashFinish,
                DashFinishSignalId,
                TutorialFunctionMarkerKind.TrainingFinish);
            var finishCollider = dashFinish.GetComponent<BoxCollider2D>();
            if (finishCollider == null) finishCollider = dashFinish.AddComponent<BoxCollider2D>();
            finishCollider.isTrigger = true;
            finishCollider.size = new Vector2(1.5f, 8f);
            var legacyArrival = dashFinish.GetComponent<TutorialTrainingArrivalMarkerHost>();
            if (legacyArrival != null) legacyArrival.enabled = false;
            var dashObjective = dashFinish.GetComponent<TutorialTrainingDashObjectiveHost>();
            if (dashObjective == null) dashObjective = dashFinish.AddComponent<TutorialTrainingDashObjectiveHost>();
            var objectiveSerialized = new SerializedObject(dashObjective);
            objectiveSerialized.FindProperty("serviceRoot").objectReferenceValue =
                Require(scene, "StageSystems").GetComponent<Narthex.Core.ServiceRoot>();
            objectiveSerialized.FindProperty("questSequenceHost").objectReferenceValue =
                Require(scene, "StageSystems").GetComponent<TutorialQuestSequenceHost>();
            objectiveSerialized.FindProperty("phaseController").objectReferenceValue = phaseController;
            objectiveSerialized.FindProperty("player").objectReferenceValue = player.transform;
            objectiveSerialized.FindProperty("questId").stringValue = "QST-TUTO-004";
            objectiveSerialized.FindProperty("signalTargetId").stringValue = DashFinishSignalId;
            objectiveSerialized.FindProperty("requiredFireCount").intValue = 3;
            objectiveSerialized.ApplyModifiedPropertiesWithoutUndo();

            var dashSources = trainingLevel.GetComponentsInChildren<Renderer>(true)
                .Concat(roots[0].GetComponentsInChildren<Renderer>(true))
                .Where(IsTrainingFireSource)
                .Distinct()
                .OrderBy(renderer => renderer.bounds.center.x)
                .Take(3)
                .ToArray();
            if (dashSources.Length != 3)
                throw new InvalidOperationException(
                    $"훈련장의 빨간 불기둥 도형은 정확히 3개가 필요합니다. 현재 감지={dashSources.Length}");

            for (var index = 0; index < dashSources.Length; index++)
            {
                var source = dashSources[index];
                source.transform.SetParent(roots[0].transform, true);
                source.transform.localScale = new Vector3(fireWidth, fireHeight, 1f);
                source.transform.position = new Vector3(
                    source.transform.position.x,
                    floorTop + fireHeight * 0.5f,
                    source.transform.position.z);
                var collider = source.GetComponent<BoxCollider2D>();
                if (collider == null) collider = source.gameObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.offset = Vector2.zero;
                collider.size = Vector2.one;
                var fire = source.GetComponent<TutorialTrainingDashFireHost>();
                if (fire == null) fire = source.gameObject.AddComponent<TutorialTrainingDashFireHost>();
                var fireSerialized = new SerializedObject(fire);
                fireSerialized.FindProperty("phaseController").objectReferenceValue = phaseController;
                fireSerialized.FindProperty("dashObjective").objectReferenceValue = dashObjective;
                fireSerialized.FindProperty("playerMotor").objectReferenceValue =
                    player.GetComponent<PlayerMotorHost>();
                fireSerialized.FindProperty("player").objectReferenceValue = player.transform;
                fireSerialized.FindProperty("fireIndex").intValue = index;
                fireSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            objectiveSerialized = new SerializedObject(dashObjective);
            var fireReferences = objectiveSerialized.FindProperty("fires");
            fireReferences.arraySize = dashSources.Length;
            for (var index = 0; index < dashSources.Length; index++)
                fireReferences.GetArrayElementAtIndex(index).objectReferenceValue =
                    dashSources[index].GetComponent<TutorialTrainingDashFireHost>();
            objectiveSerialized.ApplyModifiedPropertiesWithoutUndo();

            var elevatedPlatforms = trainingLevel.GetComponentsInChildren<Renderer>(true)
                .Concat(roots[1].GetComponentsInChildren<Renderer>(true))
                .Where(IsTrainingPlatform)
                .Distinct()
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();
            foreach (var platform in elevatedPlatforms)
            {
                platform.transform.SetParent(roots[1].transform, true);
                ConfigureSolidPlatformCollider(platform);
            }
            var importedDoubleJumpGroup = Require(scene, "더블점프훈련");
            importedDoubleJumpGroup.transform.SetParent(roots[1].transform, true);
            var importedDoubleJumpPlatforms = importedDoubleJumpGroup
                .GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && !IsTrainingFireSource(renderer))
                .OrderByDescending(renderer => renderer.bounds.max.y)
                .ToArray();
            foreach (var platform in importedDoubleJumpPlatforms)
                ConfigureSolidPlatformCollider(platform);
            if (importedDoubleJumpPlatforms.Length == 0)
                throw new InvalidOperationException("더블점프 훈련에 사용할 높은 발판이 없습니다.");
            var highestPlatform = importedDoubleJumpPlatforms[0];
            var doubleJumpFinish = Require(scene, "훈련_더블점프_끝");
            doubleJumpFinish.transform.position = new Vector3(
                highestPlatform.bounds.center.x,
                highestPlatform.bounds.max.y + 0.65f,
                0f);
            var doubleJumpCollider = doubleJumpFinish.GetComponent<BoxCollider2D>();
            if (doubleJumpCollider == null)
                doubleJumpCollider = doubleJumpFinish.AddComponent<BoxCollider2D>();
            doubleJumpCollider.isTrigger = true;
            doubleJumpCollider.size = new Vector2(
                Mathf.Max(1f, highestPlatform.bounds.size.x),
                1.5f);
            var doubleJumpArrival =
                doubleJumpFinish.GetComponent<TutorialTrainingArrivalMarkerHost>();
            if (doubleJumpArrival == null)
                doubleJumpArrival = doubleJumpFinish.AddComponent<TutorialTrainingArrivalMarkerHost>();
            var doubleJumpSerialized = new SerializedObject(doubleJumpArrival);
            doubleJumpSerialized.FindProperty("serviceRoot").objectReferenceValue =
                Require(scene, "StageSystems").GetComponent<Narthex.Core.ServiceRoot>();
            doubleJumpSerialized.FindProperty("questSequenceHost").objectReferenceValue =
                Require(scene, "StageSystems").GetComponent<TutorialQuestSequenceHost>();
            doubleJumpSerialized.FindProperty("player").objectReferenceValue = player.transform;
            doubleJumpSerialized.FindProperty("questId").stringValue = "QST-TUTO-006";
            doubleJumpSerialized.FindProperty("signalTargetId").stringValue = DoubleJumpFinishSignalId;
            doubleJumpSerialized.ApplyModifiedPropertiesWithoutUndo();

            var jumpController = Require(scene, "JumpProjectileController");
            jumpController.transform.SetParent(roots[2].transform, true);
            Require(scene, "점프훈련").transform.SetParent(roots[2].transform, true);

            var spawnHost = RequireComponent<TutorialTrainingSpawnHost>(trainingController);
            var spawnSerialized = new SerializedObject(spawnHost);
            var meleeEnemy = spawnSerialized.FindProperty("tutorialEnemy").objectReferenceValue as GameObject;
            if (meleeEnemy == null || meleeEnemy.transform.parent == null)
                throw new InvalidOperationException("근접 훈련 적 또는 근접 훈련 루트를 찾지 못했습니다.");
            meleeEnemy.transform.parent.SetParent(roots[3].transform, true);
            Require(scene, "근접공격훈련").transform.SetParent(roots[3].transform, true);

            var flowSerialized = new SerializedObject(flowHost);
            var rangedTargets = flowSerialized.FindProperty("rangedTargets");
            for (var index = 0; index < rangedTargets.arraySize; index++)
            {
                var target = rangedTargets.GetArrayElementAtIndex(index).objectReferenceValue as GameObject;
                if (target != null) target.transform.SetParent(roots[4].transform, true);
            }
            Require(scene, "원거리공격훈련").transform.SetParent(roots[4].transform, true);

            RemoveLegacyTrainingPropColliderProxies(scene, trainingBounds);
            foreach (var root in roots) root.SetActive(false);
            return roots;
        }

        private static void ConfigureSolidPlatformCollider(Renderer platform)
        {
            var platformCollider = platform.GetComponent<BoxCollider2D>();
            if (platformCollider == null)
                platformCollider = platform.gameObject.AddComponent<BoxCollider2D>();
            platformCollider.isTrigger = false;
            platformCollider.offset = Vector2.zero;
            var lossyScale = platform.transform.lossyScale;
            platformCollider.size = new Vector2(
                platform.bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(lossyScale.x)),
                platform.bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(lossyScale.y)));
        }

        private static void RemoveLegacyTrainingPropColliderProxies(Scene scene, Bounds trainingBounds)
        {
            var proxyRoot = FindSceneObject(scene, "훈련장 충돌체");
            if (proxyRoot == null) return;

            var proxies = proxyRoot.GetComponentsInChildren<BoxCollider2D>(true);
            var removedCount = 0;
            foreach (var proxy in proxies)
            {
                var size = proxy.size;
                var isRoomShell =
                    size.x >= trainingBounds.size.x * 0.7f ||
                    size.y >= trainingBounds.size.y * 0.7f;
                if (!isRoomShell)
                {
                    UnityEngine.Object.DestroyImmediate(proxy.gameObject);
                    removedCount++;
                }
            }

            Debug.Log(
                $"[sragon000][훈련장][충돌체 정리] 단계와 무관하게 남던 내부 프록시 {removedCount}개 제거, " +
                $"방 외곽 프록시 {proxyRoot.GetComponentsInChildren<BoxCollider2D>(true).Length}개 유지.");
        }

        private static void ConfigureQuestDefinitions()
        {
            var dashCondition = RequireAsset<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-004-DASH.asset");
            dashCondition.SignalType = QuestSignalType.PortalUsed;
            dashCondition.TargetId = DashFinishSignalId;
            dashCondition.RequiredAmount = 1;
            EditorUtility.SetDirty(dashCondition);

            var dashQuest = RequireAsset<QuestDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Quests/QST-TUTO-004.asset");
            dashQuest.Conditions = new[] { dashCondition };
            dashQuest.NextQuestIds = new[] { "QST-TUTO-006" };
            EditorUtility.SetDirty(dashQuest);

            var doubleJump = RequireAsset<QuestConditionDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Conditions/COND-TUTO-006-DOUBLE-JUMP.asset");
            doubleJump.SignalType = QuestSignalType.PortalUsed;
            doubleJump.TargetId = DoubleJumpFinishSignalId;
            doubleJump.RequiredAmount = 1;
            EditorUtility.SetDirty(doubleJump);

            var doubleJumpQuest = RequireAsset<QuestDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Quests/QST-TUTO-006.asset");
            doubleJumpQuest.Conditions = new[] { doubleJump };
            doubleJumpQuest.NextQuestIds = new[] { "QST-TUTO-002" };
            EditorUtility.SetDirty(doubleJumpQuest);

            var jumpQuest = RequireAsset<QuestDefinition>(
                "Assets/_Project/GameData/Tutorial/RuntimeDefinitionsV2/Quests/QST-TUTO-002.asset");
            jumpQuest.NextQuestIds = new[] { "QST-TUTO-003" };
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
                "QST-TUTO-006",
                "QST-TUTO-002",
                "QST-TUTO-003",
                "QST-TUTO-005",
                "QST-TUTO-007",
                "QST-TUTO-007-A",
                "QST-TUTO-007-B",
                "QST-TUTO-008"
            };
            var trainingObjectives = new Dictionary<string, string>
            {
                ["QST-TUTO-001"] = "비행선 패스키를 확보하고 훈련장 입구로 이동",
                ["QST-TUTO-004"] = "무적 대시로 불기둥 3개를 통과해 도착점에 도달",
                ["QST-TUTO-006"] = "더블 점프로 가장 높은 발판의 도착 마커에 도달",
                ["QST-TUTO-002"] = "훈련장 안에서 전방 투사체 3회 점프 회피",
                ["QST-TUTO-003"] = "훈련용 에너미에게 기본 공격 3콤보 적중",
                ["QST-TUTO-005"] = "원거리 공격 한 발로 훈련용 에너미 3기 동시 타격",
                ["QST-TUTO-007"] = "습격 경보에 따라 회의장과 외부 출구로 대피",
                ["QST-TUTO-007-A"] = "본부 외곽 통로의 판도라 개체 전원 처치",
                ["QST-TUTO-007-B"] = "나디르 선착장 진입로의 판도라 개체 전원 처치",
                ["QST-TUTO-008"] = "헬테와 조우해 전투를 완료"
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
                if (questId == "QST-TUTO-004")
                {
                    SetLines(beat,
                        "테우스: 첫 훈련은 대시야. 빨간 훈련 장치 세 개에서 불기둥이 계속 올라올 거야.",
                        "테우스: 대시 중에는 피해를 받지 않아. 불기둥을 관통해서 반대편 도착점까지 가 봐.");
                    beat.FindPropertyRelative("deferUntilPortalTargetId").stringValue = string.Empty;
                }
                else if (questId == "QST-TUTO-006")
                {
                    SetLines(beat,
                        "테우스: 부츠 동기화 완료. 이제 공중에서 SPACE를 한 번 더 눌러 더블 점프할 수 있어.",
                        "테우스: 발판을 타고 가장 높은 곳의 도착 마커까지 올라가 봐.");
                    beat.FindPropertyRelative("deferUntilPortalTargetId").stringValue = string.Empty;
                }
                else if (questId == "QST-TUTO-003")
                {
                    SetLines(beat,
                        "테우스: 다음은 기본 공격이야. 움직이지 않는 원형 허수아비 가까이 이동해.",
                        "테우스: 마우스 왼쪽 버튼을 0.5초 안에 연속 입력해서 1·2·3단 공격을 모두 적중시켜.");
                }
                else if (questId == "QST-TUTO-005")
                {
                    SetLines(beat,
                        "테우스: 마지막은 원거리 공격이야.",
                        "테우스: 2번 키를 누르면 바라보는 방향으로 관통 투사체를 발사해. 재사용 대기시간은 1.5초야.",
                        "테우스: 일렬로 선 원형 허수아비 세 기를 한 발로 모두 관통해 봐.");
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
                ["QST-TUTO-006"] = "훈련_더블점프_끝",
                ["QST-TUTO-002"] = "훈련_점프_시작",
                ["QST-TUTO-003"] = "훈련_근접_시작",
                ["QST-TUTO-005"] = "훈련_원거리_시작"
            };
            foreach (var pair in targetMarkers)
                SetBeaconTarget(targets, pair.Key, Require(scene, pair.Value).transform);
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
                ["QST-TUTO-006"] = "훈련_더블점프_시작",
                ["QST-TUTO-002"] = "훈련_점프_재시작",
                ["QST-TUTO-003"] = "훈련_근접_시작",
                ["QST-TUTO-005"] = "훈련_원거리_시작"
            };
            var scene = EditorSceneManager.GetActiveScene();
            for (var index = 0; index < checkpoints.arraySize; index++)
            {
                var checkpoint = checkpoints.GetArrayElementAtIndex(index);
                var questId = checkpoint.FindPropertyRelative("questId").stringValue;
                if (!markerNames.TryGetValue(questId, out var markerName)) continue;
                checkpoint.FindPropertyRelative("spawnPoint").objectReferenceValue =
                    Require(scene, markerName).transform;
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
                throw new InvalidOperationException("원거리 공격의 1번 입력 또는 투사체 참조가 유효하지 않습니다.");
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
                "[sragon000][훈련장][검증 통과] 대시→더블점프→점프→기본 공격→원거리 공격, " +
                "단계별 행동 범위 1개만 활성화, 단일 출구 문, 이전 단계 정리, " +
                "원거리 표적 3개, 체크포인트와 입력 참조 정상.");
        }

        private static bool IsTrainingFireSource(Renderer renderer)
        {
            if (renderer == null) return false;
            var color = ResolveRendererColor(renderer);
            return color.r >= 0.55f && color.r >= color.g * 1.35f && color.r >= color.b * 1.35f;
        }

        private static bool IsTrainingPlatform(Renderer renderer)
        {
            if (renderer == null || IsTrainingFireSource(renderer)) return false;
            var bounds = renderer.bounds;
            var color = ResolveRendererColor(renderer);
            var nearWhite = color.r >= 0.65f && color.g >= 0.65f && color.b >= 0.65f;
            return nearWhite &&
                   bounds.center.y > -3f &&
                   bounds.size.y <= 1.6f &&
                   bounds.size.x >= 0.5f &&
                   bounds.size.x <= 8f;
        }

        private static Color ResolveRendererColor(Renderer renderer)
        {
            if (renderer is SpriteRenderer spriteRenderer) return spriteRenderer.color;
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Color"))
                return renderer.sharedMaterial.color;
            return Color.white;
        }

        private static TutorialFunctionMarkerKind ResolveTrainingMarkerKind(string markerName)
        {
            if (markerName.Contains("등장")) return TutorialFunctionMarkerKind.EnemySpawn;
            if (markerName.Contains("재시작") || markerName.Contains("진입"))
                return TutorialFunctionMarkerKind.Checkpoint;
            if (markerName.Contains("끝") || markerName.Contains("도착") || markerName.Contains("종료"))
                return TutorialFunctionMarkerKind.TrainingFinish;
            if (markerName.Contains("시작")) return TutorialFunctionMarkerKind.TrainingStart;
            if (markerName.Contains("원거리")) return TutorialFunctionMarkerKind.Objective;
            return TutorialFunctionMarkerKind.Point;
        }

        private static void ConfigureFunctionMarker(
            GameObject target,
            string markerId,
            TutorialFunctionMarkerKind markerKind)
        {
            var current = target.transform;
            while (current != null)
            {
                if (current.CompareTag("EditorOnly")) current.gameObject.tag = "Untagged";
                if (current.name == "훈련장 배치 마커") break;
                current = current.parent;
            }

            var marker = target.GetComponent<TutorialFunctionMarkerHost>();
            if (marker == null) marker = target.AddComponent<TutorialFunctionMarkerHost>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("markerId").stringValue = markerId;
            serialized.FindProperty("kind").enumValueIndex = (int)markerKind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
