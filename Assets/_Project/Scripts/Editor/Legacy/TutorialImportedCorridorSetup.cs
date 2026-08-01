using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    /// <summary>
    /// Legacy migration that connects the imported corridor without modifying
    /// the level designer's geometry children. The generated integration hierarchy
    /// can be rebuilt from the sragon000 menu.
    /// </summary>
    public static class TutorialImportedCorridorSetup
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string CompletionMarkerName = "C01_연동완료";
        private const string DashQuestId = "QST-TUTO-004";

        private static readonly (string koreanName, string technicalName)[] IntegrationNames =
        {
            ("A_회의장_연동", "A_Meeting_Integration"),
            ("B_숨겨진방_연동", "B_HiddenRoom_Integration"),
            ("C_복도_연동", "C_Corridor_Integration"),
            ("D_훈련장_연동", "D_Training_Integration"),
            ("E_외부_연동", "E_Exterior_Integration"),
            ("F_전투스테이지1_연동", "F_Encounter01_Integration"),
            ("G_전투스테이지2_연동", "G_Encounter02_Integration"),
            ("H_선착장_헬테_연동", "H_Helte_Integration")
        };

        [MenuItem(PrometheusToolMenuPaths.Legacy + "Apply Corridor Integration")]
        public static void ApplyFromMenu()
        {
            Apply();
        }

        [MenuItem(PrometheusToolMenuPaths.Legacy + "Repair Hidden Room Updraft")]
        public static void ApplyHiddenRoomRecoveryUpdraftFromMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"[sragon000][튜토리얼] '{TargetScenePath}' 씬을 연 뒤 다시 실행하세요.");
                return;
            }

            EnsureHiddenRoomRecoveryUpdraft(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[sragon000][B04] 숨겨진 방 상승기류 복귀 보정 완료: " +
                "X 69.5~81, Y -4.1~15.5, 상승 가속도 6.5, 최대 상승 속도 4.5.");
        }

        private static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"[sragon000][튜토리얼] '{TargetScenePath}' 씬을 연 뒤 다시 실행하세요.");
                return;
            }

            try
            {
                var runtimeRoot = Require(scene, "TutorialRuntimeRoot", "튜토리얼 런타임");
                var integrationRoot = Require(scene, "GameplayIntegrationRoot", "게임플레이 연동");
                var corridorRoot = Require(scene, "복도");
                var trainingRoot = Require(scene, "훈련장 수정버전");
                var corridorIntegration = Require(scene, "C_Corridor_Integration", "C_복도_연동");
                var trainingIntegration = Require(scene, "D_Training_Integration", "D_훈련장_연동");

                NormalizeTechnicalHierarchy(runtimeRoot, integrationRoot, scene);

                corridorIntegration = Require(scene, "C_Corridor_Integration");
                trainingIntegration = Require(scene, "D_Training_Integration");

                CreateColliderProxies(corridorRoot, corridorIntegration.transform, "복도 충돌체", false);
                CreateColliderProxies(trainingRoot, trainingIntegration.transform, "훈련장 충돌체", true);

                var trainingSpawn = GetOrCreateAnchor(
                    trainingIntegration.transform,
                    "D01_훈련장_진입스폰",
                    new Vector3(185f, -3.9f, 0f));
                CreateCorridorLore(scene, corridorIntegration.transform);
                CreateTrainingTransition(
                    scene,
                    corridorRoot,
                    trainingRoot,
                    corridorIntegration.transform,
                    trainingSpawn);
                ConfigureObjectiveBeacon(scene, trainingSpawn);
                EnsureHiddenRoomRecoveryUpdraft(scene);

                var marker = GetOrCreateChild(corridorIntegration.transform, CompletionMarkerName);
                marker.transform.localPosition = Vector3.zero;
                marker.SetActive(false);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAppliedScene(scene);
                Debug.Log(
                    "[sragon000][튜토리얼] 복도 1차 연동 완료: 기술 계층명, 복도 자막, 수정 훈련장 전환, 흰 도형 충돌체를 적용했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void ValidateAppliedScene(Scene scene)
        {
            var requiredTechnicalParents = new[]
            {
                "TutorialRuntimeRoot",
                "GameplayIntegrationRoot",
                "A_Meeting_Integration",
                "B_HiddenRoom_Integration",
                "C_Corridor_Integration",
                "D_Training_Integration",
                "E_Exterior_Integration",
                "F_Encounter01_Integration",
                "G_Encounter02_Integration",
                "H_Helte_Integration"
            };
            foreach (var parentName in requiredTechnicalParents)
                if (FindSceneObject(scene, parentName) == null)
                    throw new InvalidOperationException($"기술 연동 부모가 없습니다: {parentName}");

            var corridorIntegration = Require(scene, "C_Corridor_Integration");
            var transition = Require(scene, "C01_훈련장_이동").GetComponent<TutorialZoneTransitionHost>();
            if (transition == null || !transition.HasValidSetup)
                throw new InvalidOperationException("복도→훈련장 구역 전환 설정이 유효하지 않습니다.");

            var loreRoot = corridorIntegration.transform.Find("테우스 세계관 자막");
            var loreHosts = loreRoot != null
                ? loreRoot.GetComponentsInChildren<TutorialLoreSubtitleTriggerHost>(true)
                : Array.Empty<TutorialLoreSubtitleTriggerHost>();
            if (loreHosts.Length != 3 || loreHosts.Any(host => host == null || !host.HasValidSetup))
                throw new InvalidOperationException("C01 복도 테우스 자막 트리거 3개의 설정이 유효하지 않습니다.");

            var corridorColliderRoot = Require(scene, "복도 충돌체");
            var trainingColliderRoot = Require(scene, "훈련장 충돌체");
            var corridorColliderCount = corridorColliderRoot.GetComponentsInChildren<BoxCollider2D>(true).Length;
            var trainingColliderCount = trainingColliderRoot.GetComponentsInChildren<BoxCollider2D>(true).Length;
            if (corridorColliderCount == 0 || trainingColliderCount == 0)
                throw new InvalidOperationException("복도 또는 훈련장 충돌 프록시가 비어 있습니다.");

            Debug.Log(
                $"[sragon000][튜토리얼][검증 통과] 기술 부모 {requiredTechnicalParents.Length}개, " +
                $"복도 충돌체 {corridorColliderCount}개, 훈련장 흰 도형 충돌체 {trainingColliderCount}개, " +
                $"자막 트리거 {loreHosts.Length}개, 구역 전환 참조 정상.");
        }

        private static void NormalizeTechnicalHierarchy(GameObject runtimeRoot, GameObject integrationRoot, Scene scene)
        {
            runtimeRoot.name = "TutorialRuntimeRoot";
            integrationRoot.name = "GameplayIntegrationRoot";

            foreach (var (koreanName, technicalName) in IntegrationNames)
            {
                var target = FindSceneObject(scene, koreanName) ?? FindSceneObject(scene, technicalName);
                if (target != null) target.name = technicalName;
            }
        }

        private static void CreateColliderProxies(
            GameObject levelRoot,
            Transform integrationParent,
            string proxyRootName,
            bool whiteShapesOnly)
        {
            var proxyRoot = GetOrCreateChild(integrationParent, proxyRootName);
            proxyRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            proxyRoot.transform.localScale = Vector3.one;

            var existing = proxyRoot.GetComponentsInChildren<BoxCollider2D>(true);
            foreach (var collider in existing)
                UnityEngine.Object.DestroyImmediate(collider.gameObject);

            var allRenderers = levelRoot.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer != null && renderer.bounds.size.x > 0.02f && renderer.bounds.size.y > 0.02f)
                .ToArray();
            if (allRenderers.Length == 0)
                throw new InvalidOperationException($"{levelRoot.name}에 충돌 프록시를 만들 렌더러가 없습니다.");

            var levelBounds = allRenderers[0].bounds;
            for (var index = 1; index < allRenderers.Length; index++)
                levelBounds.Encapsulate(allRenderers[index].bounds);

            var renderers = allRenderers
                .Where(renderer =>
                    !whiteShapesOnly ||
                    IsWhiteBlockout(renderer) && IsStructuralShell(renderer, levelBounds))
                .OrderBy(renderer => renderer.transform.GetSiblingIndex())
                .ThenBy(renderer => renderer.name, StringComparer.Ordinal)
                .ToArray();

            for (var index = 0; index < renderers.Length; index++)
            {
                var source = renderers[index];
                var bounds = source.bounds;
                var proxy = new GameObject($"충돌체_{index + 1:00}_{source.name}");
                proxy.transform.SetParent(proxyRoot.transform, true);
                proxy.transform.SetPositionAndRotation(
                    new Vector3(bounds.center.x, bounds.center.y, 0f),
                    Quaternion.identity);
                proxy.transform.localScale = Vector3.one;
                var collider = proxy.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(bounds.size.x, bounds.size.y);
                collider.isTrigger = false;
            }
        }

        private static bool IsStructuralShell(Renderer renderer, Bounds levelBounds)
        {
            var bounds = renderer.bounds;
            return bounds.size.x >= levelBounds.size.x * 0.7f ||
                   bounds.size.y >= levelBounds.size.y * 0.7f;
        }

        private static bool IsWhiteBlockout(Renderer renderer)
        {
            Color color;
            if (renderer is SpriteRenderer spriteRenderer)
            {
                color = spriteRenderer.color;
            }
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

        private static void CreateCorridorLore(Scene scene, Transform parent)
        {
            var presenter = FindSceneComponent<TutorialLoreSubtitlePresenter>(scene);
            var questSequence = FindSceneComponent<TutorialQuestSequenceHost>(scene);
            var player = Require(scene, "PlayerRoot").transform;
            if (presenter == null || questSequence == null)
                throw new InvalidOperationException("복도 자막에 필요한 런타임 구성요소를 찾지 못했습니다.");

            var loreRoot = GetOrCreateChild(parent, "테우스 세계관 자막");
            CreateLoreTrigger(
                loreRoot.transform,
                "C01_자막_01_아다마스",
                new Vector3(120f, -3.5f, 0f),
                "테우스: 아다마스는 나디르의 저항 거점이야. 제니스의 감시를 피해 움직이고 있어.",
                presenter,
                questSequence,
                player);
            CreateLoreTrigger(
                loreRoot.transform,
                "C01_자막_02_판도라공장",
                new Vector3(130f, -3.5f, 0f),
                "테우스: 제니스의 판도라 공장이 일주일째 평소와 다르게 분주해. 그래서 이번 작전을 서두르는 거고.",
                presenter,
                questSequence,
                player);
            CreateLoreTrigger(
                loreRoot.transform,
                "C01_자막_03_훈련장안내",
                new Vector3(139f, -3.5f, 0f),
                "테우스: 먼저 훈련장에서 움직임을 점검하자. 끝나면 바로 밖으로 나갈 거야.",
                presenter,
                questSequence,
                player);
        }

        private static void CreateLoreTrigger(
            Transform parent,
            string objectName,
            Vector3 position,
            string text,
            TutorialLoreSubtitlePresenter presenter,
            TutorialQuestSequenceHost questSequence,
            Transform player)
        {
            var trigger = GetOrCreateChild(parent, objectName);
            trigger.transform.SetPositionAndRotation(position, Quaternion.identity);
            trigger.transform.localScale = Vector3.one;
            var collider = trigger.GetComponent<BoxCollider2D>();
            if (collider == null) collider = trigger.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2f, 8f);

            var host = trigger.GetComponent<TutorialLoreSubtitleTriggerHost>();
            if (host == null) host = trigger.AddComponent<TutorialLoreSubtitleTriggerHost>();
            var serialized = new SerializedObject(host);
            serialized.FindProperty("presenter").objectReferenceValue = presenter;
            serialized.FindProperty("questSequenceHost").objectReferenceValue = questSequence;
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.FindProperty("requiredQuestId").stringValue = DashQuestId;
            serialized.FindProperty("subtitleText").stringValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTrainingTransition(
            Scene scene,
            GameObject corridorRoot,
            GameObject trainingRoot,
            Transform parent,
            Transform trainingSpawn)
        {
            var source = Require(scene, "A02_Exit_ToCorridor");
            var transitionObject = parent.Find("C01_훈련장_이동")?.gameObject;
            if (transitionObject == null)
            {
                transitionObject = UnityEngine.Object.Instantiate(source);
                transitionObject.name = "C01_훈련장_이동";
                transitionObject.transform.SetParent(parent, true);
            }

            transitionObject.SetActive(true);
            transitionObject.transform.SetPositionAndRotation(new Vector3(147f, -4f, 0f), Quaternion.identity);
            transitionObject.transform.localScale = Vector3.one;
            var collider = transitionObject.GetComponent<BoxCollider2D>();
            if (collider == null) collider = transitionObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.enabled = true;
            collider.size = new Vector2(1.6f, 2.4f);

            var host = transitionObject.GetComponent<TutorialZoneTransitionHost>();
            if (host == null)
                throw new InvalidOperationException("복제한 구역 전환 오브젝트에 TutorialZoneTransitionHost가 없습니다.");

            var serialized = new SerializedObject(host);
            serialized.FindProperty("currentZoneRoot").objectReferenceValue = corridorRoot;
            serialized.FindProperty("nextZoneRoot").objectReferenceValue = trainingRoot;
            serialized.FindProperty("destinationSpawn").objectReferenceValue = trainingSpawn;
            serialized.FindProperty("requiredQuestId").stringValue = DashQuestId;
            serialized.FindProperty("portalSignalTargetId").stringValue = "TUTORIAL-CORRIDOR-TO-TRAINING";
            serialized.FindProperty("useLadderSequence").boolValue = false;
            serialized.FindProperty("destinationCameraMinX").floatValue = 191.5f;
            serialized.FindProperty("destinationCameraMaxX").floatValue = 208.5f;
            serialized.FindProperty("destinationCameraFixedY").floatValue = 0f;
            serialized.FindProperty("destinationCameraTracksVertical").boolValue = true;
            serialized.FindProperty("destinationCameraMinY").floatValue = -1f;
            serialized.FindProperty("destinationCameraMaxY").floatValue = 6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureObjectiveBeacon(Scene scene, Transform trainingSpawn)
        {
            var beacon = FindSceneComponent<TutorialObjectiveBeaconHost>(scene);
            if (beacon == null) throw new InvalidOperationException("TutorialObjectiveBeaconHost를 찾지 못했습니다.");

            var serialized = new SerializedObject(beacon);
            var targets = serialized.FindProperty("targets");
            var targetIndex = -1;
            for (var index = 0; index < targets.arraySize; index++)
            {
                var candidate = targets.GetArrayElementAtIndex(index);
                if (candidate.FindPropertyRelative("questId").stringValue == DashQuestId)
                {
                    targetIndex = index;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                targetIndex = targets.arraySize;
                targets.InsertArrayElementAtIndex(targetIndex);
            }

            var target = targets.GetArrayElementAtIndex(targetIndex);
            target.FindPropertyRelative("questId").stringValue = DashQuestId;
            target.FindPropertyRelative("target").objectReferenceValue = trainingSpawn;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool EnsureHiddenRoomRecoveryUpdraft(Scene scene)
        {
            var changed = false;
            var flow = FindSceneComponent<TutorialChapter0IntroFlowHost>(scene);
            if (flow == null) return false;

            var serialized = new SerializedObject(flow);
            var minimum = serialized.FindProperty("updraftMin");
            var maximum = serialized.FindProperty("updraftMax");
            var liftSpeed = serialized.FindProperty("updraftLiftSpeed");
            var maximumRiseSpeed = serialized.FindProperty("updraftMaxRiseSpeed");
            var requiredMinimum = new Vector2(69.5f, -4.1f);
            var requiredMaximum = new Vector2(81f, 15.5f);

            if (minimum.vector2Value != requiredMinimum)
            {
                minimum.vector2Value = requiredMinimum;
                changed = true;
            }
            if (maximum.vector2Value != requiredMaximum)
            {
                maximum.vector2Value = requiredMaximum;
                changed = true;
            }
            if (!Mathf.Approximately(liftSpeed.floatValue, 6.5f))
            {
                liftSpeed.floatValue = 6.5f;
                changed = true;
            }
            if (!Mathf.Approximately(maximumRiseSpeed.floatValue, 4.5f))
            {
                maximumRiseSpeed.floatValue = 4.5f;
                changed = true;
            }
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(flow);
            }

            var visualRoot = FindSceneObject(scene, "B_Updraft_ART_SLOT");
            if (visualRoot == null) return changed;
            foreach (Transform strip in visualRoot.transform)
            {
                if (strip == null || !strip.name.StartsWith("WindStrip_", StringComparison.Ordinal)) continue;
                var position = strip.localPosition;
                var scale = strip.localScale;
                if (Mathf.Approximately(position.y, 5.7f) && Mathf.Approximately(scale.y, 19.6f)) continue;
                position.y = 5.7f;
                scale.y = 19.6f;
                strip.localPosition = position;
                strip.localScale = scale;
                EditorUtility.SetDirty(strip);
                changed = true;
            }

            return changed;
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
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var candidate in transforms)
                    if (candidate != null && candidate.name == objectName)
                        return candidate.gameObject;
            }
            return null;
        }

        private static T FindSceneComponent<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var candidate = root.GetComponentInChildren<T>(true);
                if (candidate != null) return candidate;
            }
            return null;
        }
    }
}
