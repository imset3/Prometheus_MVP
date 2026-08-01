using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class TutorialDebugSectionSkipSetup
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string HostName = "DeveloperSectionSkip";
        private const string SkipPlayModeTestName =
            "Narthex.PlayModeTests.TutorialSceneRuntimeSmokeTests." +
            "DeveloperSectionSkip_JumpsFromFToGAndHelteWithoutCompletingSkippedQuests";
        private static TestRunnerApi testRunnerApi;

        [MenuItem(PrometheusToolMenuPaths.Legacy + "Apply Developer Section Skip")]
        public static void ApplyFromMenu() => Apply();

        [MenuItem(PrometheusToolMenuPaths.Tests + "Developer Section Skip")]
        public static void RunSkipPlayModeTest()
        {
            if (testRunnerApi != null)
            {
                Debug.LogWarning("[sragon000][개발자 스킵][플레이 테스트] 이미 실행 중입니다.");
                return;
            }

            testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            testRunnerApi.RegisterCallbacks(new SkipTestCallbacks());
            testRunnerApi.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { SkipPlayModeTestName },
                assemblyNames = new[] { "Narthex.PlayModeTests" }
            }));
            Debug.Log("[sragon000][개발자 스킵][플레이 테스트] F→G→헬테 빠른 전환 검증을 시작합니다.");
        }

        private static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"'{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            try
            {
                var stageSystems = Require(scene, "StageSystems");
                var hostObject = Find(scene, HostName);
                if (hostObject == null)
                {
                    hostObject = new GameObject(HostName);
                    hostObject.transform.SetParent(stageSystems.transform, false);
                }

                var host = hostObject.GetComponent<TutorialDebugSectionSkipHost>();
                if (host == null) host = hostObject.AddComponent<TutorialDebugSectionSkipHost>();
                var player = Require(scene, "PlayerRoot");
                var fRoot = Require(scene, "F스테이지");
                var gRoot = Require(scene, "G스테이지");
                var hRoot = Require(scene, "선착장");
                var fTechnical = Require(scene, "F_Encounter01_Integration");
                var gTechnical = Require(scene, "G_Encounter02_Integration");
                var hTechnical = Require(scene, "H_Helte_Integration");

                var serialized = new SerializedObject(host);
                SetObject(serialized, "serviceRoot", stageSystems.GetComponent<ServiceRoot>());
                SetObject(serialized, "questSequenceHost", FindComponent<TutorialQuestSequenceHost>(scene));
                SetObject(serialized, "restartHost", FindComponent<TutorialRestartHost>(scene));
                SetObject(serialized, "dialoguePresenter", FindComponent<TutorialDialoguePresenter>(scene));
                SetObject(serialized, "introFlowHost", FindComponent<TutorialChapter0IntroFlowHost>(scene));
                SetObject(serialized, "guideCompanion", FindComponent<TutorialGuideCompanionHost>(scene));
                SetObject(serialized, "cameraFollowHost", Require(scene, "Main Camera").GetComponent<CameraFollowHost>());
                SetObject(serialized, "playerInputHost", player.GetComponent<PlayerInputHost>());
                SetObject(serialized, "playerMotorHost", player.GetComponent<PlayerMotorHost>());
                SetObject(serialized, "player", player.transform);
                SetObject(serialized, "playerBody", player.GetComponent<Rigidbody2D>());
                SetObject(
                    serialized,
                    "fadeCanvasGroup",
                    Require(scene, "TutorialZoneFadeOverlay").GetComponent<CanvasGroup>());

                var zoneRoots = CollectZoneRoots(scene, fRoot, gRoot, hRoot);
                SetObjectArray(serialized.FindProperty("zoneRoots"), zoneRoots);
                SetObjectArray(
                    serialized.FindProperty("technicalRoots"),
                    new[] { fTechnical, gTechnical, hTechnical });

                var sections = serialized.FindProperty("sections");
                sections.arraySize = 3;
                ConfigureSection(
                    sections.GetArrayElementAtIndex(0),
                    "F 전투 스테이지",
                    "QST-TUTO-007-A",
                    "외부 · 전투 스테이지 1",
                    fRoot,
                    fTechnical,
                    Require(scene, "F01_Spawn_ExteriorSide").transform,
                    Require(scene, "E01_Exit_ToF").GetComponent<TutorialZoneTransitionHost>(),
                    Array.Empty<GameObject>());
                ConfigureSection(
                    sections.GetArrayElementAtIndex(1),
                    "G 전투 스테이지",
                    "QST-TUTO-007-B",
                    "외부 · 전투 스테이지 2",
                    gRoot,
                    gTechnical,
                    Require(scene, "G01_Spawn_FromF").transform,
                    Require(scene, "F01_Exit_ToG").GetComponent<TutorialZoneTransitionHost>(),
                    Array.Empty<GameObject>());
                ConfigureSection(
                    sections.GetArrayElementAtIndex(2),
                    "헬테 선착장",
                    "QST-TUTO-008",
                    "선착장 · 보스전",
                    hRoot,
                    hTechnical,
                    FindSpawnUnder(hTechnical.transform, "H01_Spawn_FromG"),
                    Require(scene, "G01_Exit_ToH").GetComponent<TutorialZoneTransitionHost>(),
                    new[] { Require(scene, "TutorialHelte") });

                serialized.FindProperty("showOverlay").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(host);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[sragon000][개발자 스킵] F8=F 바로가기, F9=F→G→헬테 순차 이동을 적용했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void ConfigureSection(
            SerializedProperty section,
            string displayName,
            string questId,
            string locationName,
            GameObject zoneRoot,
            GameObject technicalRoot,
            Transform spawn,
            TutorialZoneTransitionHost transition,
            GameObject[] activateOnJump)
        {
            if (transition == null) throw new InvalidOperationException($"{displayName} 전환 Host가 없습니다.");
            var transitionSerialized = new SerializedObject(transition);
            section.FindPropertyRelative("displayName").stringValue = displayName;
            section.FindPropertyRelative("questId").stringValue = questId;
            section.FindPropertyRelative("locationName").stringValue = locationName;
            section.FindPropertyRelative("zoneRoot").objectReferenceValue = zoneRoot;
            section.FindPropertyRelative("technicalRoot").objectReferenceValue = technicalRoot;
            SetObjectArray(section.FindPropertyRelative("activateOnJump"), activateOnJump);
            section.FindPropertyRelative("spawnPoint").objectReferenceValue = spawn;
            CopyFloat(transitionSerialized, section, "destinationCameraMinX", "cameraMinX");
            CopyFloat(transitionSerialized, section, "destinationCameraMaxX", "cameraMaxX");
            CopyBool(transitionSerialized, section, "destinationCameraTracksVertical", "cameraTracksVertical");
            CopyFloat(transitionSerialized, section, "destinationCameraFixedY", "cameraFixedY");
            CopyFloat(transitionSerialized, section, "destinationCameraMinY", "cameraMinY");
            CopyFloat(transitionSerialized, section, "destinationCameraMaxY", "cameraMaxY");
        }

        private static GameObject[] CollectZoneRoots(
            Scene scene,
            GameObject fRoot,
            GameObject gRoot,
            GameObject hRoot)
        {
            var roots = new HashSet<GameObject> { fRoot, gRoot, hRoot };
            foreach (var transition in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<TutorialZoneTransitionHost>(true)))
            {
                var serialized = new SerializedObject(transition);
                var current = serialized.FindProperty("currentZoneRoot")?.objectReferenceValue as GameObject;
                var next = serialized.FindProperty("nextZoneRoot")?.objectReferenceValue as GameObject;
                if (current != null) roots.Add(current);
                if (next != null) roots.Add(next);
            }

            return roots.Where(root => root != null).ToArray();
        }

        private static Transform FindSpawnUnder(Transform parent, string objectName)
        {
            foreach (var candidate in parent.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate;
            throw new InvalidOperationException($"{parent.name} 아래에서 {objectName}을 찾지 못했습니다.");
        }

        private static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            throw new InvalidOperationException($"{typeof(T).Name}을 찾지 못했습니다.");
        }

        private static GameObject Require(Scene scene, string objectName)
        {
            var found = Find(scene, objectName);
            if (found != null) return found;
            throw new InvalidOperationException($"TutorialScene에서 '{objectName}'을 찾지 못했습니다.");
        }

        private static GameObject Find(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName)
                    return candidate.gameObject;
            return null;
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"{propertyName} 직렬화 필드를 찾지 못했습니다.");
            property.objectReferenceValue = value;
        }

        private static void SetObjectArray(SerializedProperty property, GameObject[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void CopyFloat(
            SerializedObject source,
            SerializedProperty destination,
            string sourceName,
            string destinationName)
        {
            destination.FindPropertyRelative(destinationName).floatValue =
                source.FindProperty(sourceName).floatValue;
        }

        private static void CopyBool(
            SerializedObject source,
            SerializedProperty destination,
            string sourceName,
            string destinationName)
        {
            destination.FindPropertyRelative(destinationName).boolValue =
                source.FindProperty(sourceName).boolValue;
        }

        private sealed class SkipTestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log(
                    $"[sragon000][개발자 스킵][플레이 테스트 {result.TestStatus}] " +
                    $"pass={result.PassCount}, fail={result.FailCount}, duration={result.Duration:F2}s");
                testRunnerApi = null;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Passed) return;
                Debug.LogError(
                    $"[sragon000][개발자 스킵][실패] {result.FullName}: " +
                    $"{result.Message}\n{result.StackTrace}");
            }
        }
    }
}
