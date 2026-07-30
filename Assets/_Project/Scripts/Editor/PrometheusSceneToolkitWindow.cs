using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public sealed class PrometheusSceneToolkitWindow : EditorWindow
    {
        private enum Tab
        {
            Overview,
            Markers,
            SceneDoctor,
            Snapshots,
            ZoneFlow,
            AiCommands,
            ExistingTools
        }

        private static readonly string[] TabLabels =
        {
            "개요", "마커", "Scene Doctor", "스냅샷", "구역 흐름", "AI 명령", "기존 도구"
        };

        private Tab tab;
        private Vector2 scroll;
        private TutorialFunctionMarkerKind markerKind;
        private string markerId = "";
        private Transform markerParent;
        private Vector2 markerPosition;
        private Vector2 markerSize = Vector2.one;
        private List<PrometheusAiIssue> doctorIssues = new();
        private string firstSnapshotPath = "";
        private string secondSnapshotPath = "";
        private PrometheusZoneFlowAsset flowAsset;
        private UnityEditor.Editor flowEditor;
        private string aiRequestJson = "";
        private string aiResponseJson = "";

        [MenuItem("sragon000/Prometheus Scene Toolkit")]
        public static void Open() => GetWindow<PrometheusSceneToolkitWindow>("Prometheus Toolkit");

        private void OnEnable()
        {
            minSize = new Vector2(620f, 520f);
            aiRequestJson = ExampleRequest("scene.report");
        }

        private void OnGUI()
        {
            DrawHeader();
            tab = (Tab)GUILayout.Toolbar((int)tab, TabLabels);
            EditorGUILayout.Space(6f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case Tab.Overview: DrawOverview(); break;
                case Tab.Markers: DrawMarkers(); break;
                case Tab.SceneDoctor: DrawDoctor(); break;
                case Tab.Snapshots: DrawSnapshots(); break;
                case Tab.ZoneFlow: DrawFlow(); break;
                case Tab.AiCommands: DrawAi(); break;
                case Tab.ExistingTools: DrawExistingTools(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            var scene = SceneManager.GetActiveScene();
            EditorGUILayout.LabelField("Prometheus Scene Toolkit", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                scene.IsValid() ? $"현재 씬: {scene.name}  ({scene.path})" : "현재 씬 없음",
                EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(
                "AI 명령은 기본적으로 dry-run입니다. 사람이 위치와 범위를 편집할 때는 마커 탭을 사용하세요.",
                MessageType.Info);
        }

        private void DrawOverview()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            var objects = PrometheusSceneQuery.All(scene).ToArray();
            var markers = objects.Count(item => item.GetComponent<TutorialFunctionMarkerHost>() != null);
            EditorGUILayout.LabelField("씬 요약", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("루트", scene.rootCount.ToString());
            EditorGUILayout.LabelField("오브젝트", objects.Length.ToString());
            EditorGUILayout.LabelField("기능 마커", markers.ToString());
            EditorGUILayout.Space();
            if (GUILayout.Button("전체 안전 점검"))
            {
                doctorIssues = PrometheusSceneDoctor.Scan(scene);
                tab = Tab.SceneDoctor;
            }
            if (GUILayout.Button("현재 씬 스냅샷 저장"))
            {
                firstSnapshotPath = PrometheusSceneSnapshotService.Save(
                    PrometheusSceneSnapshotService.Capture(scene));
                ShowNotification(new GUIContent($"저장됨: {firstSnapshotPath}"));
            }
            if (GUILayout.Button("AI용 씬 리포트 생성"))
            {
                aiRequestJson = ExampleRequest("scene.report");
                ExecuteJson();
                tab = Tab.AiCommands;
            }
        }

        private void DrawMarkers()
        {
            EditorGUILayout.LabelField("마커 배치 팔레트", EditorStyles.boldLabel);
            markerKind = (TutorialFunctionMarkerKind)EditorGUILayout.EnumPopup("기능", markerKind);
            markerId = EditorGUILayout.TextField("안정 ID", markerId);
            markerParent = (Transform)EditorGUILayout.ObjectField("부모", markerParent, typeof(Transform), true);
            markerPosition = EditorGUILayout.Vector2Field("월드 위치", markerPosition);
            markerSize = EditorGUILayout.Vector2Field("범위", markerSize);
            using (new EditorGUI.DisabledScope(!SceneManager.GetActiveScene().IsValid()))
            {
                if (GUILayout.Button("마커 생성"))
                {
                    try
                    {
                        var created = PrometheusMarkerAuthoring.Create(
                            SceneManager.GetActiveScene(),
                            markerParent,
                            markerKind,
                            markerId,
                            markerPosition,
                            markerSize);
                        Selection.activeGameObject = created;
                        markerId = "";
                    }
                    catch (Exception exception)
                    {
                        EditorUtility.DisplayDialog("마커 생성 실패", exception.Message, "확인");
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("선택한 마커", EditorStyles.boldLabel);
            var selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<TutorialFunctionMarkerHost>()
                : null;
            if (selected == null)
            {
                EditorGUILayout.HelpBox("Hierarchy 또는 Scene View에서 기능 마커를 선택하세요.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("ID", selected.MarkerId);
            EditorGUILayout.LabelField("종류", selected.Kind.ToString());
            EditorGUILayout.LabelField("경로", PrometheusSceneQuery.Path(selected.gameObject));
            if (GUILayout.Button("선택 마커에 Scene View 맞추기"))
                SceneView.lastActiveSceneView?.FrameSelected();
        }

        private void DrawDoctor()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("검사"))
                doctorIssues = PrometheusSceneDoctor.Scan(SceneManager.GetActiveScene());
            if (GUILayout.Button("안전 복구 미리보기"))
                ShowRepairPreview(true);
            if (GUILayout.Button("안전 복구 적용"))
                ShowRepairPreview(false);
            EditorGUILayout.EndHorizontal();

            if (doctorIssues.Count == 0)
            {
                EditorGUILayout.HelpBox("검사를 실행하거나 현재 검사에서 문제가 발견되지 않았습니다.", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField($"문제 {doctorIssues.Count}개", EditorStyles.boldLabel);
            foreach (var issue in doctorIssues)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"[{issue.severity}] {issue.rule}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(issue.hierarchyPath, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(issue.message, EditorStyles.wordWrappedLabel);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(issue.objectId)))
                    if (GUILayout.Button("오브젝트 선택"))
                        PrometheusSceneDoctor.TryFocus(issue);
                EditorGUILayout.EndVertical();
            }
        }

        private void ShowRepairPreview(bool dryRun)
        {
            var changes = PrometheusSceneDoctor.RepairSafe(SceneManager.GetActiveScene(), dryRun);
            if (dryRun)
                EditorUtility.DisplayDialog("안전 복구 미리보기",
                    changes.Count == 0
                        ? "적용할 안전 복구가 없습니다."
                        : string.Join("\n", changes.Select(change =>
                            $"{change.hierarchyPath}: {change.action} → {change.after}")),
                    "확인");
            else
            {
                doctorIssues = PrometheusSceneDoctor.Scan(SceneManager.GetActiveScene());
                ShowNotification(new GUIContent($"{changes.Count}개 복구 적용"));
            }
        }

        private void DrawSnapshots()
        {
            if (GUILayout.Button("현재 상태를 첫 번째 스냅샷으로 저장"))
                firstSnapshotPath = PrometheusSceneSnapshotService.Save(
                    PrometheusSceneSnapshotService.Capture(SceneManager.GetActiveScene()));
            firstSnapshotPath = EditorGUILayout.TextField("이전", firstSnapshotPath);
            if (GUILayout.Button("현재 상태를 두 번째 스냅샷으로 저장"))
                secondSnapshotPath = PrometheusSceneSnapshotService.Save(
                    PrometheusSceneSnapshotService.Capture(SceneManager.GetActiveScene()));
            secondSnapshotPath = EditorGUILayout.TextField("이후", secondSnapshotPath);
            using (new EditorGUI.DisabledScope(!File.Exists(firstSnapshotPath) || !File.Exists(secondSnapshotPath)))
            {
                if (GUILayout.Button("두 스냅샷 비교"))
                {
                    var diff = PrometheusSceneSnapshotService.Compare(
                        PrometheusSceneSnapshotService.Load(firstSnapshotPath),
                        PrometheusSceneSnapshotService.Load(secondSnapshotPath),
                        firstSnapshotPath,
                        secondSnapshotPath);
                    var outputPath = $"{PrometheusSceneSnapshotService.DefaultDirectory}/diff_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
                    File.WriteAllText(outputPath, JsonUtility.ToJson(diff, true));
                    EditorUtility.RevealInFinder(outputPath);
                }
            }
        }

        private void DrawFlow()
        {
            var selected = (PrometheusZoneFlowAsset)EditorGUILayout.ObjectField(
                "구역 흐름 에셋", flowAsset, typeof(PrometheusZoneFlowAsset), false);
            if (selected != flowAsset)
            {
                flowAsset = selected;
                DestroyImmediate(flowEditor);
                flowEditor = flowAsset != null ? UnityEditor.Editor.CreateEditor(flowAsset) : null;
            }
            if (GUILayout.Button("새 구역 흐름 에셋 생성")) CreateFlowAsset();
            if (flowEditor != null)
            {
                EditorGUILayout.Space();
                flowEditor.OnInspectorGUI();
                if (GUILayout.Button("현재 씬 기준 흐름 검증"))
                {
                    var issues = flowAsset.Validate(SceneManager.GetActiveScene());
                    EditorUtility.DisplayDialog(
                        "구역 흐름 검증",
                        issues.Count == 0 ? "문제가 없습니다." : string.Join("\n", issues.Select(issue => issue.message)),
                        "확인");
                }
            }
        }

        private void CreateFlowAsset()
        {
            const string directory = "Assets/_Project/EditorData/ZoneFlows";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/EditorData"))
                AssetDatabase.CreateFolder("Assets/_Project", "EditorData");
            if (!AssetDatabase.IsValidFolder(directory))
                AssetDatabase.CreateFolder("Assets/_Project/EditorData", "ZoneFlows");
            var scene = SceneManager.GetActiveScene();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{scene.name}Flow.asset");
            var asset = CreateInstance<PrometheusZoneFlowAsset>();
            asset.Configure(scene.path, "", Array.Empty<PrometheusZoneFlowNode>());
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            flowAsset = asset;
            flowEditor = UnityEditor.Editor.CreateEditor(asset);
            Selection.activeObject = asset;
        }

        private void DrawAi()
        {
            EditorGUILayout.LabelField("JSON 요청", EditorStyles.boldLabel);
            aiRequestJson = EditorGUILayout.TextArea(aiRequestJson, GUILayout.MinHeight(170f));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("씬 리포트 예시")) aiRequestJson = ExampleRequest("scene.report");
            if (GUILayout.Button("Doctor 예시")) aiRequestJson = ExampleRequest("scene.doctor.scan");
            if (GUILayout.Button("마커 목록 예시")) aiRequestJson = ExampleRequest("marker.list");
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("현재 Unity에서 실행")) ExecuteJson();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("JSON 응답", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(aiResponseJson, GUILayout.MinHeight(220f));
            EditorGUILayout.HelpBox(
                $"{PrometheusAiCommandRunner.PendingRequestPath}에 요청을 저장한 뒤 " +
                "`sragon000/AI Toolkit/Run Pending Command` 메뉴를 MCP로 실행할 수 있습니다.",
                MessageType.Info);
        }

        private void ExecuteJson()
        {
            try
            {
                var request = JsonUtility.FromJson<PrometheusAiCommandRequest>(aiRequestJson);
                var response = PrometheusAiCommandRunner.Execute(request);
                aiResponseJson = PrometheusAiCommandRunner.SerializeResponse(response);
            }
            catch (Exception exception)
            {
                aiResponseJson = exception.ToString();
            }
        }

        private static string ExampleRequest(string command) =>
            JsonUtility.ToJson(new PrometheusAiCommandRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                command = command,
                scenePath = SceneManager.GetActiveScene().path,
                dryRun = true
            }, true);

        private static void DrawExistingTools()
        {
            EditorGUILayout.HelpBox(
                "기존 Setup 코드는 유지하고 여기서 호출합니다. 검증이 끝날 때까지 기존 메뉴도 호환용으로 남습니다.",
                MessageType.Info);
            DrawLegacyButton("활성 튜토리얼 씬 검증", "sragon000/Validation/Validate Active Tutorial Scene");
            DrawLegacyButton("훈련장 구조 출력", "sragon000/튜토리얼/수정 훈련장 구조 출력");
            DrawLegacyButton("훈련장 마커 검증", "sragon000/튜토리얼/훈련장 배치 마커 검증");
            DrawLegacyButton("F 전투 적 배치 재정렬", "sragon000/튜토리얼/F 전투 적 배치 추천값으로 재정렬");
            DrawLegacyButton("G 전투 적 배치 재정렬", "sragon000/튜토리얼/G 전투 적 배치 추천값으로 재정렬");
            DrawLegacyButton("전체 튜토리얼 플레이 테스트", "sragon000/튜토리얼/가져온 전체 튜토리얼 플레이 테스트 실행");
            DrawLegacyButton("G→H 플레이 테스트", "sragon000/튜토리얼/G 바람에서 H 도착 플레이 테스트 실행");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("아트 도구", EditorStyles.boldLabel);
            DrawLegacyButton("PNG 시퀀스 적용", "sragon000/Art/Character PNG Sequence Setup");
            DrawLegacyButton("스프라이트 시트 생성", "sragon000/Art/Sprite Sheet Animation Builder");
        }

        private static void DrawLegacyButton(string label, string menuPath)
        {
            if (!GUILayout.Button(label)) return;
            if (!EditorApplication.ExecuteMenuItem(menuPath))
                EditorUtility.DisplayDialog("실행 실패", $"메뉴를 찾을 수 없습니다.\n{menuPath}", "확인");
        }
    }
}
