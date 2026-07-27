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
    [InitializeOnLoad]
    public static class TutorialImportedExteriorSetup
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene-이경수 버전.unity";
        private const string CompletionMarkerName = "E01_연동완료";
        private const string RequiredQuestId = "QST-TUTO-007";

        static TutorialImportedExteriorSetup()
        {
            EditorApplication.delayCall += TryAutoApply;
        }

        [MenuItem("sragon000/튜토리얼/C03 사다리 및 E 외부 연동 적용")]
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
                    Debug.LogWarning($"[sragon000][E01] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            try
            {
                var corridorIntegration = Require(scene, "C_Corridor_Integration");
                var exteriorIntegration = Require(scene, "E_Exterior_Integration");
                DisableLegacyLadderTransition(scene);
                var c03State = ConfigureCorridorThirdVisit(scene, corridorIntegration.transform);
                ConfigureA03ArrivalState(scene, c03State);
                ConfigureCorridorToExterior(scene, corridorIntegration.transform, exteriorIntegration.transform, c03State);
                ConfigureExterior(scene, exteriorIntegration.transform);

                var marker = GetOrCreateChild(exteriorIntegration.transform, CompletionMarkerName);
                marker.transform.localPosition = Vector3.zero;
                marker.SetActive(false);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAppliedScene(scene);
                Debug.Log(
                    "[sragon000][E01] C03 전용 자막·진동, 사다리 자동 이동, " +
                    "외부 스폰·카메라·충돌체·습격 전망 연출을 연결했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void DisableLegacyLadderTransition(Scene scene)
        {
            var legacyExit = Require(scene, "ExitTrigger");
            var legacyHost = legacyExit.GetComponent<TutorialZoneTransitionHost>();
            if (legacyHost == null) return;
            var serialized = new SerializedObject(legacyHost);
            serialized.FindProperty("useLadderSequence").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            legacyHost.enabled = false;
        }

        private static GameObject ConfigureCorridorThirdVisit(Scene scene, Transform corridorIntegration)
        {
            var stateRoot = GetOrCreateChild(corridorIntegration, "C03_긴급이동_연출");
            stateRoot.transform.localPosition = Vector3.zero;
            stateRoot.transform.localRotation = Quaternion.identity;
            stateRoot.transform.localScale = Vector3.one;

            var ladder = Require(scene, "C03_LadderPresentation");
            ladder.transform.SetParent(stateRoot.transform, true);
            ladder.transform.position = new Vector3(147f, -5.15f, 0f);
            ladder.SetActive(true);

            GetOrCreateAnchor(stateRoot.transform, "C03_LadderEntry", new Vector3(147f, -3.9f, 0f));
            GetOrCreateAnchor(stateRoot.transform, "C03_LadderExit", new Vector3(147f, -6.4f, 0f));

            var subtitles = FindSceneComponent<TutorialLoreSubtitlePresenter>(scene);
            var quests = FindSceneComponent<TutorialQuestSequenceHost>(scene);
            var player = Require(scene, "PlayerRoot").transform;
            if (subtitles == null || quests == null)
                throw new InvalidOperationException("C03 자막에 필요한 런타임 참조가 없습니다.");

            var subtitleRoot = GetOrCreateChild(stateRoot.transform, "C03_테우스_긴급자막");
            CreateLoreTrigger(
                subtitleRoot.transform,
                "C03_자막_01_외부출구",
                new Vector3(123f, -3.5f, 0f),
                "테우스: 외부 출구까지 직진이야. 복도 끝의 사다리로 올라가자.",
                subtitles,
                quests,
                player);
            CreateLoreTrigger(
                subtitleRoot.transform,
                "C03_자막_02_포위경고",
                new Vector3(137f, -3.5f, 0f),
                "테우스: 판도라 반응이 계속 늘고 있어. 여기서 멈추면 포위돼.",
                subtitles,
                quests,
                player);

            var shakeRoot = GetOrCreateChild(stateRoot.transform, "C03_충격연출");
            CreateShakeTrigger(scene, shakeRoot.transform, "C03_진동_01", new Vector3(126f, -3.5f, 0f), 0.1f, 0.28f);
            CreateShakeTrigger(scene, shakeRoot.transform, "C03_진동_02", new Vector3(141f, -3.5f, 0f), 0.14f, 0.36f);

            stateRoot.SetActive(false);
            return stateRoot;
        }

        private static void ConfigureA03ArrivalState(Scene scene, GameObject c03State)
        {
            var departure = Require(scene, "A03_Exit_To_C03");
            var host = departure.GetComponent<TutorialZoneTransitionHost>();
            if (host == null) throw new InvalidOperationException("A03 출구에 TutorialZoneTransitionHost가 없습니다.");

            var serialized = new SerializedObject(host);
            SetObject(serialized, "objectiveBeacon", FindSceneComponent<TutorialObjectiveBeaconHost>(scene));
            SetObject(serialized, "destinationObjectiveTarget", Require(scene, "C03_Exit_ExteriorSide").transform);
            SetObjectArray(serialized.FindProperty("activateOnArrival"), c03State);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCorridorToExterior(
            Scene scene,
            Transform corridorIntegration,
            Transform exteriorIntegration,
            GameObject c03State)
        {
            var exit = Require(scene, "C03_Exit_ExteriorSide");
            exit.transform.SetParent(c03State.transform, true);
            var collider = exit.GetComponent<BoxCollider2D>();
            if (collider == null) collider = exit.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.6f, 2.8f);

            var host = exit.GetComponent<TutorialZoneTransitionHost>();
            if (host == null) host = exit.AddComponent<TutorialZoneTransitionHost>();
            var player = Require(scene, "PlayerRoot");
            var ladder = Require(scene, "C03_LadderPresentation");
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
            SetObject(serialized, "currentZoneRoot", Require(scene, "복도"));
            SetObject(serialized, "nextZoneRoot", Require(scene, "외부"));
            SetObject(serialized, "destinationSpawn", Require(scene, "E01_Spawn_HQExit").transform);
            serialized.FindProperty("guideArrivalOffset").vector3Value = new Vector3(1.1f, 1.1f, 0f);
            serialized.FindProperty("requiredQuestId").stringValue = RequiredQuestId;
            serialized.FindProperty("portalSignalTargetId").stringValue = "TUTORIAL-C03-TO-E01";
            serialized.FindProperty("destinationCheckpointQuestId").stringValue = RequiredQuestId;
            SetObject(serialized, "destinationObjectiveTarget", Require(scene, "E01_InvasionViewAnchor").transform);
            SetObjectArray(serialized.FindProperty("deactivateOnArrival"));
            SetObjectArray(serialized.FindProperty("deactivateOnCompletion"), c03State);
            serialized.FindProperty("useLadderSequence").boolValue = true;
            serialized.FindProperty("requireInteractInput").boolValue = true;
            SetObject(serialized, "ladderEntry", Require(scene, "C03_LadderEntry").transform);
            SetObject(serialized, "ladderExit", Require(scene, "C03_LadderExit").transform);
            SetObject(serialized, "ladderVisual", ladder);
            serialized.FindProperty("ladderMoveDuration").floatValue = 1.2f;
            serialized.FindProperty("ladderExitHoldDuration").floatValue = 0.12f;
            serialized.FindProperty("ladderStepSway").floatValue = 0.045f;
            serialized.FindProperty("destinationCameraMinX").floatValue = 246f;
            serialized.FindProperty("destinationCameraMaxX").floatValue = 274f;
            serialized.FindProperty("destinationCameraFixedY").floatValue = 0f;
            serialized.FindProperty("destinationCameraTracksVertical").boolValue = false;
            serialized.FindProperty("fadeOutDuration").floatValue = 0.28f;
            serialized.FindProperty("blackHoldDuration").floatValue = 0.12f;
            serialized.FindProperty("fadeInDuration").floatValue = 0.38f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            exteriorIntegration.gameObject.SetActive(true);
            exit.SetActive(true);
            ConfigureInteractionPrompt(scene, collider, c03State);
        }

        private static void ConfigureInteractionPrompt(Scene scene, Collider2D ladderTrigger, GameObject availabilityRoot)
        {
            var prompt = FindSceneComponent<TutorialInteractionPromptHost>(scene);
            if (prompt == null) throw new InvalidOperationException("TutorialInteractionPromptHost를 찾지 못했습니다.");
            var serialized = new SerializedObject(prompt);
            var targets = serialized.FindProperty("targets");
            var targetIndex = -1;
            for (var index = 0; index < targets.arraySize; index++)
            {
                var candidate = targets.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("trigger").objectReferenceValue != ladderTrigger) continue;
                targetIndex = index;
                break;
            }
            if (targetIndex < 0)
            {
                targetIndex = targets.arraySize;
                targets.InsertArrayElementAtIndex(targetIndex);
            }

            var target = targets.GetArrayElementAtIndex(targetIndex);
            target.FindPropertyRelative("trigger").objectReferenceValue = ladderTrigger;
            target.FindPropertyRelative("availabilityRoot").objectReferenceValue = availabilityRoot;
            target.FindPropertyRelative("promptText").stringValue = "F: 사다리 내려가기";
            target.FindPropertyRelative("requiredQuestId").stringValue = RequiredQuestId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureExterior(Scene scene, Transform exteriorIntegration)
        {
            var exteriorRoot = Require(scene, "외부");
            CreateWhiteColliderProxies(exteriorRoot, exteriorIntegration, "외부 충돌체");

            var viewAnchor = Require(scene, "E01_InvasionViewAnchor");
            var collider = viewAnchor.GetComponent<BoxCollider2D>();
            if (collider == null) collider = viewAnchor.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2.4f, 8f);

            var host = viewAnchor.GetComponent<TutorialExteriorInvasionViewHost>();
            if (host == null) host = viewAnchor.AddComponent<TutorialExteriorInvasionViewHost>();
            var serialized = new SerializedObject(host);
            SetObject(serialized, "questSequenceHost", FindSceneComponent<TutorialQuestSequenceHost>(scene));
            SetObject(serialized, "subtitlePresenter", FindSceneComponent<TutorialLoreSubtitlePresenter>(scene));
            SetObject(serialized, "cameraFollowHost", Require(scene, "Main Camera").GetComponent<CameraFollowHost>());
            SetObject(serialized, "objectiveBeacon", FindSceneComponent<TutorialObjectiveBeaconHost>(scene));
            SetObject(serialized, "player", Require(scene, "PlayerRoot").transform);
            SetObject(serialized, "viewAnchor", viewAnchor.transform);
            SetObject(serialized, "nextTarget", Require(scene, "E01_Exit_ToF").transform);
            serialized.FindProperty("requiredQuestId").stringValue = RequiredQuestId;
            SetStringArray(serialized.FindProperty("subtitles"), new[]
            {
                "테우스: 판도라 유닛이 본부 외곽을 봉쇄하고 있어. 정면으로 돌파해야 해.",
                "테우스: 프로메, 앞쪽 전투 구역으로 이동하자. 적을 모두 처리해야 길이 열릴 거야."
            });
            serialized.FindProperty("cameraHoldDuration").floatValue = 1.9f;
            serialized.FindProperty("restoreCameraMinX").floatValue = 246f;
            serialized.FindProperty("restoreCameraMaxX").floatValue = 274f;
            serialized.FindProperty("restoreCameraY").floatValue = 0f;
            serialized.FindProperty("shakeAmplitude").floatValue = 0.16f;
            serialized.FindProperty("shakeDuration").floatValue = 0.4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            exteriorRoot.SetActive(false);
        }

        private static void CreateLoreTrigger(
            Transform parent,
            string name,
            Vector3 position,
            string text,
            TutorialLoreSubtitlePresenter presenter,
            TutorialQuestSequenceHost quests,
            Transform player)
        {
            var gameObject = GetOrCreateChild(parent, name);
            gameObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            gameObject.transform.localScale = Vector3.one;
            var collider = gameObject.GetComponent<BoxCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2f, 8f);
            var host = gameObject.GetComponent<TutorialLoreSubtitleTriggerHost>();
            if (host == null) host = gameObject.AddComponent<TutorialLoreSubtitleTriggerHost>();
            var serialized = new SerializedObject(host);
            SetObject(serialized, "presenter", presenter);
            SetObject(serialized, "questSequenceHost", quests);
            SetObject(serialized, "player", player);
            serialized.FindProperty("requiredQuestId").stringValue = RequiredQuestId;
            serialized.FindProperty("subtitleText").stringValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateShakeTrigger(
            Scene scene,
            Transform parent,
            string name,
            Vector3 position,
            float amplitude,
            float duration)
        {
            var gameObject = GetOrCreateChild(parent, name);
            gameObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            gameObject.transform.localScale = Vector3.one;
            var collider = gameObject.GetComponent<BoxCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 8f);
            var host = gameObject.GetComponent<TutorialCameraShakeTriggerHost>();
            if (host == null) host = gameObject.AddComponent<TutorialCameraShakeTriggerHost>();
            var serialized = new SerializedObject(host);
            SetObject(serialized, "questSequenceHost", FindSceneComponent<TutorialQuestSequenceHost>(scene));
            SetObject(serialized, "cameraFollowHost", Require(scene, "Main Camera").GetComponent<CameraFollowHost>());
            SetObject(serialized, "player", Require(scene, "PlayerRoot").transform);
            serialized.FindProperty("requiredQuestId").stringValue = RequiredQuestId;
            serialized.FindProperty("amplitude").floatValue = amplitude;
            serialized.FindProperty("duration").floatValue = duration;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateWhiteColliderProxies(GameObject levelRoot, Transform parent, string rootName)
        {
            var proxyRoot = GetOrCreateChild(parent, rootName);
            proxyRoot.transform.localPosition = Vector3.zero;
            proxyRoot.transform.localRotation = Quaternion.identity;
            proxyRoot.transform.localScale = Vector3.one;
            foreach (var collider in proxyRoot.GetComponentsInChildren<BoxCollider2D>(true))
                UnityEngine.Object.DestroyImmediate(collider.gameObject);

            var renderers = levelRoot.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && renderer.bounds.size.x > 0.02f &&
                                   renderer.bounds.size.y > 0.02f && IsWhiteBlockout(renderer))
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

        private static void ValidateAppliedScene(Scene scene)
        {
            var state = Require(scene, "C03_긴급이동_연출");
            if (state.activeSelf)
                throw new InvalidOperationException("C03 방문 연출은 A03 출발 전 비활성 상태여야 합니다.");

            var lore = state.GetComponentsInChildren<TutorialLoreSubtitleTriggerHost>(true);
            var shakes = state.GetComponentsInChildren<TutorialCameraShakeTriggerHost>(true);
            if (lore.Length != 2 || lore.Any(host => host == null || !host.HasValidSetup))
                throw new InvalidOperationException("C03 긴급 이동 자막 2개의 참조가 유효하지 않습니다.");
            if (shakes.Length != 2 || shakes.Any(host => host == null || !host.HasValidSetup))
                throw new InvalidOperationException("C03 카메라 진동 트리거 2개의 참조가 유효하지 않습니다.");

            var transition = Require(scene, "C03_Exit_ExteriorSide").GetComponent<TutorialZoneTransitionHost>();
            if (transition == null || !transition.HasValidSetup ||
                !transition.UsesLadderSequence || !transition.RequiresInteraction ||
                !transition.HasValidLadderSetup)
                throw new InvalidOperationException("C03 사다리→E 외부 전환 참조가 유효하지 않습니다.");

            var exteriorView = Require(scene, "E01_InvasionViewAnchor")
                .GetComponent<TutorialExteriorInvasionViewHost>();
            if (exteriorView == null || !exteriorView.HasValidSetup ||
                !exteriorView.PreservesPlayerControl || exteriorView.SubtitleCount != 2)
                throw new InvalidOperationException("E 외부 습격 전망 연출 참조가 유효하지 않습니다.");

            var exteriorColliderCount = Require(scene, "외부 충돌체")
                .GetComponentsInChildren<BoxCollider2D>(true).Length;
            if (exteriorColliderCount == 0)
                throw new InvalidOperationException("E 외부 흰 도형 충돌 프록시가 없습니다.");

            Debug.Log(
                $"[sragon000][E01][검증 통과] C03 자막 {lore.Length}개, 진동 {shakes.Length}개, " +
                $"사다리 F 입력 후 자동 하강·전환, 외부 흰 도형 충돌체 {exteriorColliderCount}개, " +
                "플레이어 조작 유지 습격 전망과 체크포인트 정상.");
        }

        private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{serialized.targetObject.GetType().Name}.{propertyName} 필드가 없습니다.");
            property.objectReferenceValue = value;
        }

        private static void SetObjectArray(SerializedProperty property, params UnityEngine.Object[] values)
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
