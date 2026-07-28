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
        private const string TargetScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string CompletionMarkerName = "C06_기존패스키단일화완료";

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
                ConfigureHiddenRoomMarkerLayout(scene);
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
            var oldBeam = Require(scene, "TheusFlashlight_ART_SLOT", "LightBeam_ART_SLOT");
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

        private static void ConfigureHiddenRoomMarkerLayout(Scene scene)
        {
            var migrateToExistingRoom = FindSceneObject(scene, CompletionMarkerName) == null;
            var hiddenRoot = Require(scene, "숨겨진방");
            hiddenRoot.SetActive(false);
            var player = Require(scene, "PlayerRoot");

            var spawn = ConvertToFunctionalMarker(
                Require(scene, "HiddenRoomSpawn"),
                new Vector3(85f, 4f, 0f),
                TutorialFunctionMarkerKind.Checkpoint,
                migrateToExistingRoom);
            var ledge = ConvertToFunctionalMarker(
                Require(scene, "LedgeStop"),
                new Vector3(85f, 4f, 0f),
                TutorialFunctionMarkerKind.Objective,
                migrateToExistingRoom);
            var importedPasskeyTarget = Require(scene, "B03_PasskeyTarget", "PasskeyTarget");
            var duplicatePasskeyTarget = FindSceneObject(scene, "PasskeyTarget");
            var passkey = ConvertToFunctionalMarker(
                importedPasskeyTarget,
                new Vector3(68.5f, 12.25f, 0f),
                TutorialFunctionMarkerKind.Objective,
                migrateToExistingRoom);
            passkey.name = "PasskeyTarget";
            var hiddenReturn = ConvertToFunctionalMarker(
                Require(scene, "HiddenReturnTarget"),
                new Vector3(85f, 4f, 0f),
                TutorialFunctionMarkerKind.Interaction,
                migrateToExistingRoom);
            spawn.SetParent(hiddenRoot.transform, true);
            ledge.SetParent(hiddenRoot.transform, true);
            passkey.SetParent(hiddenRoot.transform, true);
            hiddenReturn.SetParent(hiddenRoot.transform, true);

            var hiddenEntry = ConvertToFunctionalMarker(
                Require(scene, "HiddenRoomEntryTarget"),
                Require(scene, "HiddenRoomEntryTarget").transform.position,
                TutorialFunctionMarkerKind.Transition);
            ConvertToFunctionalMarker(
                Require(scene, "MeetingReturnSpawn"),
                Require(scene, "MeetingReturnSpawn").transform.position,
                TutorialFunctionMarkerKind.Checkpoint);

            var passkeyVisual = Require(
                scene,
                "B03_AirshipPasskey_ART_SLOT",
                "AirshipPasskey_ART_SLOT");
            passkeyVisual.name = "AirshipPasskey_ART_SLOT";
            passkeyVisual.transform.SetParent(passkey, true);
            passkeyVisual.transform.position = passkey.position;
            passkeyVisual.SetActive(false);
            var passkeyTrigger = Require(
                scene,
                "B03_PasskeyPickupTrigger",
                "PasskeyPickupTrigger");
            passkeyTrigger.name = "PasskeyPickupTrigger";
            passkeyTrigger.transform.SetParent(passkey, true);
            passkeyTrigger.transform.localPosition = Vector3.zero;
            passkeyTrigger.transform.localRotation = Quaternion.identity;

            if (duplicatePasskeyTarget != null &&
                duplicatePasskeyTarget.transform != passkey)
                UnityEngine.Object.DestroyImmediate(duplicatePasskeyTarget);

            var entryTrigger = Require(scene, "A01_HiddenRoomEntryTrigger");
            entryTrigger.transform.SetParent(hiddenEntry, true);
            entryTrigger.transform.localPosition = Vector3.zero;
            entryTrigger.transform.localRotation = Quaternion.identity;

            var ledgeTrigger = Require(scene, "B02_LedgeBriefingTrigger");
            ledgeTrigger.transform.SetParent(ledge, true);
            ledgeTrigger.transform.localPosition = Vector3.zero;
            ledgeTrigger.transform.localRotation = Quaternion.identity;

            var returnTrigger = Require(scene, "HiddenRoomReturnTrigger");
            returnTrigger.transform.SetParent(hiddenReturn, true);
            returnTrigger.transform.localPosition = Vector3.zero;
            returnTrigger.transform.localRotation = Quaternion.identity;

            var generatedHighPlatform = FindSceneObject(scene, "HiddenRoom_HighPasskeyPlatform_ART_SLOT");
            if (generatedHighPlatform != null)
                UnityEngine.Object.DestroyImmediate(generatedHighPlatform);

            var windRoot = FindSceneObject(scene, "숨겨진방 기능마커") ??
                           GetOrCreateChild(hiddenRoot.transform, "숨겨진방 기능마커");
            windRoot.transform.SetParent(hiddenRoot.transform, true);
            var windTransform = windRoot.transform.Find("HiddenRoom_Updraft_MARKER");
            var windMarker = windTransform != null
                ? windTransform.gameObject
                : GetOrCreateChild(windRoot.transform, "HiddenRoom_Updraft_MARKER");
            if (migrateToExistingRoom || windTransform == null)
            {
                windMarker.transform.SetPositionAndRotation(
                    new Vector3(75.5f, 3.5f, 0f),
                    Quaternion.identity);
                windMarker.transform.localScale = Vector3.one;
            }

            var windCollider = windMarker.GetComponent<BoxCollider2D>();
            if (windCollider == null) windCollider = windMarker.AddComponent<BoxCollider2D>();
            windCollider.isTrigger = true;
            if (migrateToExistingRoom || windTransform == null)
                windCollider.size = new Vector2(9f, 16.5f);
            ConfigureMarkerComponent(
                windMarker,
                "HIDDEN-ROOM-UPDRAFT",
                TutorialFunctionMarkerKind.Wind);
            var wind = windMarker.GetComponent<TutorialWindHazardHost>();
            if (wind == null) wind = windMarker.AddComponent<TutorialWindHazardHost>();
            var windSerialized = new SerializedObject(wind);
            windSerialized.FindProperty("playerBody").objectReferenceValue =
                player.GetComponent<Rigidbody2D>();
            windSerialized.FindProperty("player").objectReferenceValue = player.transform;
            windSerialized.FindProperty("playerMotor").objectReferenceValue =
                player.GetComponent<PlayerMotorHost>();
            windSerialized.FindProperty("liftAcceleration").floatValue = 28f;
            windSerialized.FindProperty("maximumRiseSpeed").floatValue = 9f;
            windSerialized.ApplyModifiedPropertiesWithoutUndo();

            var windVisual = Require(scene, "Updraft_ART_SLOT");
            windVisual.transform.SetParent(windMarker.transform, true);

            EnsureExistingRoomColliders(hiddenRoot.transform);

            var exitAvailability = GetOrCreateChild(hiddenReturn, "HiddenRoomExitAvailable");
            exitAvailability.SetActive(false);

            var intro = FindSceneComponent<TutorialChapter0IntroFlowHost>(scene);
            if (intro == null) throw new InvalidOperationException("TutorialChapter0IntroFlowHost를 찾지 못했습니다.");
            var introSerialized = new SerializedObject(intro);
            introSerialized.FindProperty("hiddenRoomRoot").objectReferenceValue = hiddenRoot;
            introSerialized.FindProperty("hiddenRoomSpawn").objectReferenceValue = spawn;
            introSerialized.FindProperty("ledgeTarget").objectReferenceValue = ledge;
            introSerialized.FindProperty("passkeyTarget").objectReferenceValue = passkey;
            introSerialized.FindProperty("hiddenRoomReturnTarget").objectReferenceValue = hiddenReturn;
            introSerialized.FindProperty("passkeyTrigger").objectReferenceValue =
                passkeyTrigger.GetComponent<Collider2D>();
            introSerialized.FindProperty("hiddenRoomReturnTrigger").objectReferenceValue =
                returnTrigger.GetComponent<Collider2D>();
            introSerialized.FindProperty("hiddenRoomExitAvailabilityRoot").objectReferenceValue =
                exitAvailability;
            introSerialized.FindProperty("hiddenRoomWindMarker").objectReferenceValue = wind;
            introSerialized.FindProperty("passkeyVisual").objectReferenceValue = passkeyVisual;
            introSerialized.FindProperty("hiddenCameraMinX").floatValue = 60.5f;
            introSerialized.FindProperty("hiddenCameraMaxX").floatValue = 90.5f;
            introSerialized.FindProperty("hiddenCameraMinY").floatValue = -5f;
            introSerialized.FindProperty("hiddenCameraMaxY").floatValue = 21.5f;
            introSerialized.FindProperty("updraftMin").vector2Value = new Vector2(71f, -4.75f);
            introSerialized.FindProperty("updraftMax").vector2Value = new Vector2(80f, 11.75f);
            introSerialized.FindProperty("glideRetryBelowY").floatValue = -6.5f;
            introSerialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureHiddenExitPrompt(scene, returnTrigger.GetComponent<Collider2D>(), exitAvailability);

            var generatedRoom = FindSceneObject(scene, "Z01B_HiddenGlideRoom");
            if (generatedRoom != null)
                UnityEngine.Object.DestroyImmediate(generatedRoom);
        }

        private static void EnsureExistingRoomColliders(Transform hiddenRoom)
        {
            foreach (Transform child in hiddenRoom)
            {
                if (child.GetComponent<SpriteRenderer>() == null || child.name == "Square (65)") continue;
                var collider = child.GetComponent<BoxCollider2D>();
                if (collider == null) collider = child.gameObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = false;
                collider.size = Vector2.one;
                collider.offset = Vector2.zero;
            }
        }

        private static Transform ConvertToFunctionalMarker(
            GameObject target,
            Vector3 suggestedPosition,
            TutorialFunctionMarkerKind kind,
            bool forcePosition = false)
        {
            if (forcePosition || target.GetComponent<TutorialFunctionMarkerHost>() == null)
                target.transform.SetPositionAndRotation(suggestedPosition, Quaternion.identity);
            ConfigureMarkerComponent(target, target.name, kind);
            return target.transform;
        }

        private static void ConfigureMarkerComponent(
            GameObject target,
            string markerId,
            TutorialFunctionMarkerKind kind)
        {
            var marker = target.GetComponent<TutorialFunctionMarkerHost>();
            if (marker == null) marker = target.AddComponent<TutorialFunctionMarkerHost>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("markerId").stringValue = markerId;
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureHiddenExitPrompt(
            Scene scene,
            Collider2D returnTrigger,
            GameObject availabilityRoot)
        {
            var prompt = FindSceneComponent<TutorialInteractionPromptHost>(scene);
            if (prompt == null)
                throw new InvalidOperationException("숨겨진 방 출구에 사용할 TutorialInteractionPromptHost가 없습니다.");
            var serialized = new SerializedObject(prompt);
            var targets = serialized.FindProperty("targets");
            SerializedProperty entry = null;
            for (var index = 0; index < targets.arraySize; index++)
            {
                var candidate = targets.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("trigger").objectReferenceValue != returnTrigger) continue;
                entry = candidate;
                break;
            }
            if (entry == null)
            {
                var index = targets.arraySize;
                targets.InsertArrayElementAtIndex(index);
                entry = targets.GetArrayElementAtIndex(index);
            }
            entry.FindPropertyRelative("trigger").objectReferenceValue = returnTrigger;
            entry.FindPropertyRelative("availabilityRoot").objectReferenceValue = availabilityRoot;
            entry.FindPropertyRelative("promptText").stringValue = "나가기  [ F ]";
            entry.FindPropertyRelative("requiredQuestId").stringValue = "QST-TUTO-001";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureHiddenRoomDarkness(Transform geometryRoot)
        {
            var backdrop = geometryRoot.Find("HiddenRoom_Backdrop_ART_SLOT")?.GetComponent<Renderer>();
            if (backdrop == null) return;
            const string path = "Assets/_Project/Art/Materials/TutorialHiddenRoomDarkBackdrop.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = backdrop.sharedMaterial != null
                    ? new Material(backdrop.sharedMaterial)
                    : new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }
            var darkColor = new Color(0.035f, 0.04f, 0.055f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", darkColor);
            if (material.HasProperty("_Color")) material.color = darkColor;
            EditorUtility.SetDirty(material);
            backdrop.sharedMaterial = material;
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
            if (intro == null || !intro.HasValidSetup || !intro.UsesTheusLightForm ||
                !intro.UsesMarkerDrivenUpdraft)
                throw new InvalidOperationException(
                    "챕터 0 도입부가 테우스 빛 형태 또는 마커 기반 상승기류를 사용하지 않습니다.");
            if (light == null || !light.HasValidSetup)
                throw new InvalidOperationException("테우스 빛 형태의 코어·빔·패스키 참조가 유효하지 않습니다.");
            if (emergency == null || !emergency.HasValidSetup || !emergency.UsesBlackoutRun)
                throw new InvalidOperationException("훈련장→C02 긴급 암전 전환 참조가 유효하지 않습니다.");
            if (beacon == null || !beacon.HasTarget("QST-TUTO-007"))
                throw new InvalidOperationException("습격 퀘스트의 훈련장 출구 방향 안내가 없습니다.");
            Debug.Log(
                "[sragon000][C02][검증 통과] 테우스 본체 빛 변환, 높은 패스키·낮은 F 출구·마커 상승기류, " +
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
