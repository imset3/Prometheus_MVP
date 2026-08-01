using System;
using System.Linq;
using Narthex.Core;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    /// <summary>Legacy meeting-return migration. Never runs automatically.</summary>
    public static class TutorialImportedMeetingReturnSetup
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string CompletionMarkerName = "A03_연동완료";
        private const string RequiredQuestId = "QST-TUTO-007";

        [MenuItem(PrometheusToolMenuPaths.Legacy + "Apply Meeting Return")]
        public static void ApplyFromMenu()
        {
            Apply();
        }

        private static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"[sragon000][A03] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            try
            {
                var meetingIntegration = Require(scene, "A_Meeting_Integration");
                var corridorIntegration = Require(scene, "C_Corridor_Integration");
                var meetingSpawn = GetOrCreateAnchor(
                    meetingIntegration.transform,
                    "A03_회의장_긴급복귀스폰",
                    new Vector3(9.5f, -3.9f, 0f));
                var meetingDeparture = ConfigureMeetingDeparture(
                    scene,
                    meetingIntegration.transform,
                    corridorIntegration.transform);
                ConfigureCorridorArrival(
                    scene,
                    corridorIntegration.transform,
                    meetingSpawn,
                    meetingDeparture);

                var marker = GetOrCreateChild(meetingIntegration.transform, CompletionMarkerName);
                marker.transform.localPosition = Vector3.zero;
                marker.SetActive(false);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAppliedScene(scene);
                Debug.Log(
                    "[sragon000][A03] C02 역주행 출구→회의장 3차 방문, " +
                    "에온·아르온 긴급 대화 게이트, C03 재출발을 연결했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static GameObject ConfigureMeetingDeparture(
            Scene scene,
            Transform meetingIntegration,
            Transform corridorIntegration)
        {
            var meetingRoot = Require(scene, "회의장");
            var corridorRoot = Require(scene, "복도");
            var c03Spawn = GetOrCreateAnchor(
                corridorIntegration,
                "C03_Spawn_MeetingSide",
                new Vector3(112f, -4f, 0f));

            var departure = GetOrCreateChild(meetingIntegration, "A03_Exit_To_C03");
            departure.transform.SetPositionAndRotation(new Vector3(13f, -3.9f, 0f), Quaternion.identity);
            departure.transform.localScale = Vector3.one;
            var collider = departure.GetComponent<BoxCollider2D>();
            if (collider == null) collider = departure.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.6f, 2.4f);

            var host = departure.GetComponent<TutorialZoneTransitionHost>();
            if (host == null) host = departure.AddComponent<TutorialZoneTransitionHost>();
            var serialized = new SerializedObject(host);
            SetObject(serialized, "serviceRoot", Require(scene, "StageSystems").GetComponent<ServiceRoot>());
            SetObject(serialized, "playerInputHost", Require(scene, "PlayerRoot").GetComponent<PlayerInputHost>());
            SetObject(serialized, "questSequenceHost", FindSceneComponent<TutorialQuestSequenceHost>(scene));
            SetObject(serialized, "dialoguePresenter", FindSceneComponent<TutorialDialoguePresenter>(scene));
            SetObject(serialized, "guideCompanion", FindSceneComponent<TutorialGuideCompanionHost>(scene));
            SetObject(serialized, "cameraFollowHost", Require(scene, "Main Camera").GetComponent<CameraFollowHost>());
            SetObject(serialized, "restartHost", FindSceneComponent<TutorialRestartHost>(scene));
            SetObject(serialized, "player", Require(scene, "PlayerRoot").transform);
            SetObject(serialized, "playerBody", Require(scene, "PlayerRoot").GetComponent<Rigidbody2D>());
            SetObject(serialized, "fadeCanvasGroup", Require(scene, "TutorialZoneFadeOverlay").GetComponent<CanvasGroup>());
            SetObject(serialized, "currentZoneRoot", meetingRoot);
            SetObject(serialized, "nextZoneRoot", corridorRoot);
            SetObject(serialized, "destinationSpawn", c03Spawn);
            serialized.FindProperty("guideArrivalOffset").vector3Value = new Vector3(1.1f, 1.1f, 0f);
            serialized.FindProperty("requiredQuestId").stringValue = RequiredQuestId;
            serialized.FindProperty("portalSignalTargetId").stringValue = "TUTORIAL-A03-TO-C03";
            serialized.FindProperty("destinationCheckpointQuestId").stringValue = RequiredQuestId;
            serialized.FindProperty("useLadderSequence").boolValue = false;
            serialized.FindProperty("destinationCameraMinX").floatValue = 118.5f;
            serialized.FindProperty("destinationCameraMaxX").floatValue = 141.5f;
            serialized.FindProperty("destinationCameraFixedY").floatValue = 0f;
            serialized.FindProperty("destinationCameraTracksVertical").boolValue = false;
            serialized.FindProperty("fadeOutDuration").floatValue = 0.3f;
            serialized.FindProperty("blackHoldDuration").floatValue = 0.12f;
            serialized.FindProperty("fadeInDuration").floatValue = 0.38f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            departure.SetActive(false);
            return departure;
        }

        private static void ConfigureCorridorArrival(
            Scene scene,
            Transform corridorIntegration,
            Transform meetingSpawn,
            GameObject meetingDeparture)
        {
            var arrival = Require(scene, "C02_Exit_MeetingSide");
            var collider = arrival.GetComponent<BoxCollider2D>();
            if (collider == null) collider = arrival.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.6f, 2.8f);

            var host = arrival.GetComponent<TutorialEmergencyMeetingArrivalHost>();
            if (host == null) host = arrival.AddComponent<TutorialEmergencyMeetingArrivalHost>();
            var player = Require(scene, "PlayerRoot");
            var serialized = new SerializedObject(host);
            SetObject(serialized, "serviceRoot", Require(scene, "StageSystems").GetComponent<ServiceRoot>());
            SetObject(serialized, "questSequenceHost", FindSceneComponent<TutorialQuestSequenceHost>(scene));
            SetObject(serialized, "dialoguePresenter", FindSceneComponent<TutorialDialoguePresenter>(scene));
            SetObject(serialized, "playerInputHost", player.GetComponent<PlayerInputHost>());
            SetObject(serialized, "playerMotor", player.GetComponent<PlayerMotorHost>());
            SetObject(serialized, "player", player.transform);
            SetObject(serialized, "playerBody", player.GetComponent<Rigidbody2D>());
            SetObject(serialized, "guideCompanion", FindSceneComponent<TutorialGuideCompanionHost>(scene));
            SetObject(serialized, "cameraFollowHost", Require(scene, "Main Camera").GetComponent<CameraFollowHost>());
            SetObject(serialized, "objectiveBeacon", FindSceneComponent<TutorialObjectiveBeaconHost>(scene));
            SetObject(serialized, "restartHost", FindSceneComponent<TutorialRestartHost>(scene));
            SetObject(serialized, "fadeCanvasGroup", Require(scene, "TutorialZoneFadeOverlay").GetComponent<CanvasGroup>());
            SetObject(serialized, "corridorRoot", Require(scene, "복도"));
            SetObject(serialized, "meetingRoot", Require(scene, "회의장"));
            SetObject(serialized, "meetingSpawn", meetingSpawn);
            SetObject(serialized, "meetingDepartureTrigger", meetingDeparture);
            SetObject(serialized, "meetingDepartureTarget", meetingDeparture.transform);
            serialized.FindProperty("requiredQuestId").stringValue = RequiredQuestId;
            serialized.FindProperty("portalSignalTargetId").stringValue = "TUTORIAL-C02-TO-A03";
            serialized.FindProperty("guideArrivalOffset").vector3Value = new Vector3(-1.1f, 1.1f, 0f);
            serialized.FindProperty("meetingCameraMinX").floatValue = -6.5f;
            serialized.FindProperty("meetingCameraMaxX").floatValue = 6.5f;
            serialized.FindProperty("meetingCameraY").floatValue = 0f;
            serialized.FindProperty("stageId").stringValue = "아다마스 본부 회의장 · TUTO_A_03";
            SetStringArray(serialized.FindProperty("dialogueLines"), new[]
            {
                "아르온: 프로메, 뭔가 이상해. 판도라 유닛이 본부 안쪽까지 들어왔어.",
                "에온: 우린 남은 사람들을 대피시키고 뒤따라갈게.",
                "에온: 프로메, 넌 테우스와 먼저 외부로 빠져나가렴.",
                "프로메: 둘 다 무사해야 해. 반드시 다시 만나자.",
                "아르온: 걱정 말고 가. 외부로 이어지는 길은 내가 열어 둘게.",
                "에온: 잘 다녀오렴, 프로메."
            });
            serialized.FindProperty("fadeOutDuration").floatValue = 0.28f;
            serialized.FindProperty("blackHoldDuration").floatValue = 0.12f;
            serialized.FindProperty("fadeInDuration").floatValue = 0.38f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            arrival.transform.SetParent(corridorIntegration, true);
            arrival.SetActive(true);
        }

        private static void ValidateAppliedScene(Scene scene)
        {
            var arrival = Require(scene, "C02_Exit_MeetingSide");
            var arrivalHost = arrival.GetComponent<TutorialEmergencyMeetingArrivalHost>();
            if (arrivalHost == null || !arrivalHost.HasValidSetup ||
                !arrivalHost.LocksDepartureUntilDialogue || arrivalHost.DialogueLineCount != 6)
                throw new InvalidOperationException("C02→A03 전환 또는 긴급 대화 게이트 참조가 유효하지 않습니다.");

            var departure = Require(scene, "A03_Exit_To_C03");
            var departureHost = departure.GetComponent<TutorialZoneTransitionHost>();
            if (departureHost == null || !departureHost.HasValidSetup)
                throw new InvalidOperationException("A03→C03 복도 재출발 전환 참조가 유효하지 않습니다.");

            Require(scene, "A03_회의장_긴급복귀스폰");
            Require(scene, "C03_Spawn_MeetingSide");
            if (departure.activeSelf)
                throw new InvalidOperationException("A03 출구는 긴급 대화가 끝나기 전 비활성 상태여야 합니다.");

            Debug.Log(
                "[sragon000][A03][검증 통과] C02 회의장측 출구, A03 복귀 스폰, " +
                "6줄 긴급 대화, 대화 종료 후 C03 출구 해제와 구역 체크포인트 정상.");
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{serialized.targetObject.GetType().Name}.{propertyName} 필드가 없습니다.");
            property.objectReferenceValue = value;
        }

        private static void SetStringArray(SerializedProperty property, string[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).stringValue = values[index];
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
    }
}
