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
    public static class TutorialImportedEmergencySetup
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene-이경수 버전.unity";
        private const string CompletionMarkerName = "C02_Emergency_연동완료";

        static TutorialImportedEmergencySetup()
        {
            EditorApplication.delayCall += TryAutoApply;
        }

        [MenuItem("sragon000/튜토리얼/테우스 빛 변환 및 C02 긴급 전환 적용")]
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
                    Debug.LogWarning($"[sragon000][C02] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            try
            {
                var lightForm = ConfigureTheusLightForm(scene);
                var emergencyTransition = ConfigureEmergencyTransition(scene);
                ConfigureQuestGuidanceAndRestart(scene, emergencyTransition.transform);

                var integration = Require(scene, "C_Corridor_Integration");
                var marker = GetOrCreateChild(integration.transform, CompletionMarkerName);
                marker.SetActive(false);
                marker.transform.localPosition = Vector3.zero;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAppliedScene(scene);
                Debug.Log(
                    $"[sragon000][C02] 적용 완료: 테우스 빛 형태({lightForm.name}), " +
                    "훈련장 습격 암전·달리기 발소리·복도 2차 방문 전환을 연결했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static TutorialTheusLightFormHost ConfigureTheusLightForm(Scene scene)
        {
            var guide = Require(scene, "TutorialGuideCompanion");
            var normalVisual = RequireChild(guide.transform, "Visual").gameObject;
            var passkeyTarget = Require(scene, "PasskeyTarget").transform;
            var oldBeam = Require(scene, "TheusFlashlight_ART_SLOT");
            var sourceMaterial = oldBeam.GetComponent<Renderer>()?.sharedMaterial;

            var lightRoot = GetOrCreateChild(guide.transform, "TheusLightFormRoot");
            lightRoot.transform.localPosition = Vector3.zero;
            lightRoot.transform.localRotation = Quaternion.identity;
            lightRoot.transform.localScale = Vector3.one;

            oldBeam.name = "LightBeam_ART_SLOT";
            oldBeam.transform.SetParent(lightRoot.transform, true);

            var core = CreateOrUpdateCube(
                lightRoot.transform,
                "LightCore_ART_SLOT",
                guide.transform.position,
                new Vector3(0.72f, 0.72f, 0.12f),
                sourceMaterial);
            var halo = CreateOrUpdateCube(
                lightRoot.transform,
                "LightHalo_ART_SLOT",
                guide.transform.position + new Vector3(0f, 0f, 0.04f),
                new Vector3(1.15f, 1.15f, 0.08f),
                sourceMaterial);
            halo.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

            var host = guide.GetComponent<TutorialTheusLightFormHost>();
            if (host == null) host = guide.AddComponent<TutorialTheusLightFormHost>();
            var serialized = new SerializedObject(host);
            serialized.FindProperty("normalVisualRoot").objectReferenceValue = normalVisual;
            serialized.FindProperty("lightFormRoot").objectReferenceValue = lightRoot;
            serialized.FindProperty("lightCoreVisual").objectReferenceValue = core.transform;
            serialized.FindProperty("lightBeamVisual").objectReferenceValue = oldBeam.transform;
            serialized.FindProperty("passkeyTarget").objectReferenceValue = passkeyTarget;
            serialized.FindProperty("beamThickness").floatValue = 0.32f;
            serialized.FindProperty("corePulseAmount").floatValue = 0.12f;
            serialized.FindProperty("corePulseSpeed").floatValue = 5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var intro = FindSceneComponent<TutorialChapter0IntroFlowHost>(scene);
            if (intro == null) throw new InvalidOperationException("TutorialChapter0IntroFlowHost를 찾지 못했습니다.");
            var introSerialized = new SerializedObject(intro);
            introSerialized.FindProperty("theusLightForm").objectReferenceValue = host;
            introSerialized.FindProperty("theusFlashlightVisual").objectReferenceValue = oldBeam;
            introSerialized.ApplyModifiedPropertiesWithoutUndo();

            normalVisual.SetActive(true);
            lightRoot.SetActive(false);
            return host;
        }

        private static TutorialEmergencyZoneTransitionHost ConfigureEmergencyTransition(Scene scene)
        {
            var trainingIntegration = Require(scene, "D_Training_Integration");
            var trainingRoot = Require(scene, "훈련장 수정버전");
            var corridorRoot = Require(scene, "복도");
            var destinationSpawn = Require(scene, "C02_Spawn_TrainingSide").transform;
            var corridorExit = Require(scene, "C02_Exit_MeetingSide").transform;
            var stageSystems = Require(scene, "StageSystems");
            var player = Require(scene, "PlayerRoot");
            var guide = Require(scene, "TutorialGuideCompanion");
            var fade = Require(scene, "TutorialZoneFadeOverlay").GetComponent<CanvasGroup>();
            var worldAudio = Require(scene, "WorldAudioSource").GetComponent<AudioSource>();

            var triggerObject = GetOrCreateChild(trainingIntegration.transform, "D02_EmergencyExit_To_C02");
            triggerObject.transform.SetPositionAndRotation(new Vector3(216f, -3.8f, 0f), Quaternion.identity);
            triggerObject.transform.localScale = Vector3.one;
            var collider = triggerObject.GetComponent<BoxCollider2D>();
            if (collider == null) collider = triggerObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.4f, 3f);

            var host = triggerObject.GetComponent<TutorialEmergencyZoneTransitionHost>();
            if (host == null) host = triggerObject.AddComponent<TutorialEmergencyZoneTransitionHost>();
            var serialized = new SerializedObject(host);
            serialized.FindProperty("serviceRoot").objectReferenceValue =
                stageSystems.GetComponent<Narthex.Core.ServiceRoot>();
            serialized.FindProperty("questSequenceHost").objectReferenceValue =
                stageSystems.GetComponent<TutorialQuestSequenceHost>();
            serialized.FindProperty("dialoguePresenter").objectReferenceValue =
                stageSystems.GetComponent<TutorialDialoguePresenter>();
            serialized.FindProperty("playerInputHost").objectReferenceValue = player.GetComponent<PlayerInputHost>();
            serialized.FindProperty("playerMotor").objectReferenceValue = player.GetComponent<PlayerMotorHost>();
            serialized.FindProperty("player").objectReferenceValue = player.transform;
            serialized.FindProperty("playerBody").objectReferenceValue = player.GetComponent<Rigidbody2D>();
            serialized.FindProperty("guideCompanion").objectReferenceValue =
                guide.GetComponent<TutorialGuideCompanionHost>();
            serialized.FindProperty("cameraFollowHost").objectReferenceValue =
                Require(scene, "Main Camera").GetComponent<CameraFollowHost>();
            serialized.FindProperty("objectiveBeacon").objectReferenceValue =
                FindSceneComponent<TutorialObjectiveBeaconHost>(scene);
            serialized.FindProperty("fadeCanvasGroup").objectReferenceValue = fade;
            serialized.FindProperty("runningAudioSource").objectReferenceValue = worldAudio;
            serialized.FindProperty("currentZoneRoot").objectReferenceValue = trainingRoot;
            serialized.FindProperty("nextZoneRoot").objectReferenceValue = corridorRoot;
            serialized.FindProperty("destinationSpawn").objectReferenceValue = destinationSpawn;
            serialized.FindProperty("corridorExitTarget").objectReferenceValue = corridorExit;
            serialized.FindProperty("requiredQuestId").stringValue = "QST-TUTO-007";
            serialized.FindProperty("portalSignalTargetId").stringValue = "TUTORIAL-TRAINING-EMERGENCY-TO-C02";
            serialized.FindProperty("destinationCameraMinX").floatValue = 118.5f;
            serialized.FindProperty("destinationCameraMaxX").floatValue = 141.5f;
            serialized.FindProperty("destinationCameraY").floatValue = 0f;
            serialized.FindProperty("fadeOutDuration").floatValue = 0.22f;
            serialized.FindProperty("blackoutRunDuration").floatValue = 1.45f;
            serialized.FindProperty("fadeInDuration").floatValue = 0.35f;
            serialized.FindProperty("fallbackFootstepInterval").floatValue = 0.24f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return host;
        }

        private static void ConfigureQuestGuidanceAndRestart(Scene scene, Transform trainingExit)
        {
            var beacon = FindSceneComponent<TutorialObjectiveBeaconHost>(scene);
            if (beacon == null) throw new InvalidOperationException("TutorialObjectiveBeaconHost를 찾지 못했습니다.");
            var beaconSerialized = new SerializedObject(beacon);
            var targets = beaconSerialized.FindProperty("targets");
            SetBeaconTarget(targets, "QST-TUTO-007", trainingExit);
            beaconSerialized.ApplyModifiedPropertiesWithoutUndo();

            var restart = FindSceneComponent<TutorialRestartHost>(scene);
            if (restart == null) throw new InvalidOperationException("TutorialRestartHost를 찾지 못했습니다.");
            var restartSerialized = new SerializedObject(restart);
            var checkpoints = restartSerialized.FindProperty("questCheckpoints");
            var c02Spawn = Require(scene, "C02_Spawn_TrainingSide").transform;
            for (var index = 0; index < checkpoints.arraySize; index++)
            {
                var checkpoint = checkpoints.GetArrayElementAtIndex(index);
                if (checkpoint.FindPropertyRelative("questId").stringValue != "QST-TUTO-007") continue;
                checkpoint.FindPropertyRelative("spawnPoint").objectReferenceValue = c02Spawn;
            }
            restartSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateAppliedScene(Scene scene)
        {
            var intro = FindSceneComponent<TutorialChapter0IntroFlowHost>(scene);
            var light = FindSceneComponent<TutorialTheusLightFormHost>(scene);
            var emergency = FindSceneComponent<TutorialEmergencyZoneTransitionHost>(scene);
            var beacon = FindSceneComponent<TutorialObjectiveBeaconHost>(scene);
            if (intro == null || !intro.HasValidSetup || !intro.UsesTheusLightForm)
                throw new InvalidOperationException("챕터 0 도입부가 테우스 빛 형태를 사용하지 않습니다.");
            if (light == null || !light.HasValidSetup)
                throw new InvalidOperationException("테우스 빛 형태의 코어·빔·패스키 참조가 유효하지 않습니다.");
            if (emergency == null || !emergency.HasValidSetup || !emergency.UsesBlackoutRun)
                throw new InvalidOperationException("훈련장→C02 긴급 암전 전환 참조가 유효하지 않습니다.");
            if (beacon == null || !beacon.HasTarget("QST-TUTO-007"))
                throw new InvalidOperationException("습격 퀘스트의 훈련장 출구 방향 안내가 없습니다.");
            Debug.Log(
                "[sragon000][C02][검증 통과] 테우스 본체 빛 변환, 패스키 조명, " +
                "습격 암전, 절차형 달리기 발소리, C02 역방향 스폰과 카메라 참조 정상.");
        }

        private static GameObject CreateOrUpdateCube(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var existing = parent.Find(name)?.gameObject;
            var gameObject = existing != null ? existing : GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, true);
            gameObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            gameObject.transform.localScale = scale;
            var collider = gameObject.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return gameObject;
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
