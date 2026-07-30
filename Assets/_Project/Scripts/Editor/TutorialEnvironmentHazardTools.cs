using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    [InitializeOnLoad]
    public static class TutorialEnvironmentHazardTools
    {
        private const string TargetScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string CompletionMarkerName = "G03_방향성마커연동완료";
        private const string RuntimeSmokeSessionKey = "sragon000.G02.RuntimeSmoke";

        static TutorialEnvironmentHazardTools()
        {
            EditorApplication.delayCall += TryCompleteRequestedRuntimeSmoke;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem(PrometheusToolMenuPaths.Legacy + "Apply G Environment Hazards")]
        public static void ApplyFromMenu()
        {
            Apply();
        }

        [MenuItem(PrometheusToolMenuPaths.Tests + "G Environment Runtime Smoke")]
        public static void RunRuntimeSmokeFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                CompleteRuntimeSmoke();
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(RuntimeSmokeSessionKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode ||
                !SessionState.GetBool(RuntimeSmokeSessionKey, false))
                return;
            EditorApplication.delayCall += CompleteRuntimeSmoke;
        }

        private static void TryCompleteRequestedRuntimeSmoke()
        {
            if (!SessionState.GetBool(RuntimeSmokeSessionKey, false)) return;
            if (EditorApplication.isPlaying)
            {
                CompleteRuntimeSmoke();
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += TryCompleteRequestedRuntimeSmoke;
        }

        private static void CompleteRuntimeSmoke()
        {
            try
            {
                var scene = EditorSceneManager.GetActiveScene();
                if (!scene.IsValid() || FindSceneObject(scene, "G스테이지") == null)
                    throw new InvalidOperationException("G 환경 런타임 검증 대상 오브젝트가 열려 있지 않습니다.");

                Require(scene, "G스테이지").SetActive(true);
                Require(scene, "G_Encounter02_Integration").SetActive(true);
                var coordinator = Require(scene, "G02_HazardController")
                    .GetComponent<TutorialEnvironmentHazardCoordinatorHost>();
                var fires = FindSceneComponents<TutorialFireHazardHost>(scene);
                var winds = Require(scene, "G02_EnvironmentHazards")
                    .GetComponentsInChildren<TutorialWindHazardHost>(true);
                var lavas = FindSceneComponents<TutorialLavaHazardHost>(scene);
                if (coordinator == null || !coordinator.enabled ||
                    fires.Length != 2 || fires.Any(host => !host.enabled) ||
                    winds.Length < 3 || winds.Any(host => !host.enabled) ||
                    lavas.Length != 1 || lavas.Any(host => !host.enabled))
                    throw new InvalidOperationException("G 환경 위험물 런타임 활성화 또는 Awake 검증에 실패했습니다.");

                Debug.Log(
                    "[sragon000][G02][런타임 검증 통과] G 활성화 시 화염 2, 바람 3개 이상, " +
                    "용암 1, 안전지점 조정자가 오류 없이 시작되었습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                SessionState.SetBool(RuntimeSmokeSessionKey, false);
                EditorApplication.ExitPlaymode();
            }
        }

        private static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScenePath)
            {
                Debug.LogWarning($"[sragon000][G02] '{TargetScenePath}' 씬을 연 뒤 실행하세요.");
                return;
            }

            try
            {
                var gRoot = Require(scene, "G스테이지");
                var gIntegration = Require(scene, "G_Encounter02_Integration");
                var player = Require(scene, "PlayerRoot");
                var stageSystems = Require(scene, "StageSystems");
                var renderers = gRoot.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer != null &&
                                       renderer.bounds.size.x > 0.02f &&
                                       renderer.bounds.size.y > 0.02f)
                    .ToArray();
                var analyzed = renderers
                    .Select(renderer => new ClassifiedRenderer(renderer, GetColor(renderer)))
                    .OrderBy(entry => entry.renderer.bounds.center.x)
                    .ThenBy(entry => entry.renderer.bounds.center.y)
                    .ToArray();
                var classified = analyzed
                    .Where(entry => entry.kind != HazardKind.None)
                    .ToArray();

                Debug.Log(
                    "[sragon000][G02][비흰색 분석] " +
                    string.Join(
                        " | ",
                        analyzed.Where(entry => !IsWhiteBlockout(entry.renderer))
                            .Select(entry =>
                            $"{entry.kind}:{entry.renderer.name} color={entry.color} " +
                            $"center={entry.renderer.bounds.center:F1} size={entry.renderer.bounds.size:F1}")));

                var fireSources = classified.Where(entry => entry.kind == HazardKind.Fire).ToArray();
                var windSources = classified.Where(entry => entry.kind == HazardKind.Wind).ToArray();
                var lavaSources = classified.Where(entry => entry.kind == HazardKind.Lava).ToArray();
                if (fireSources.Length == 0 || windSources.Length == 0 || lavaSources.Length == 0)
                    throw new InvalidOperationException(
                        $"G 색상 위험물 분석 실패: 화염 {fireSources.Length}, 바람 {windSources.Length}, 용암 {lavaSources.Length}");

                var hazardRoot = GetOrCreateChild(gIntegration.transform, "G02_EnvironmentHazards");
                var controllerObject = GetOrCreateChild(hazardRoot.transform, "G02_HazardController");
                var coordinator = ConfigureCoordinator(
                    scene,
                    controllerObject,
                    player,
                    stageSystems,
                    Require(scene, "G01_Spawn_FromF").transform);
                ConfigureFireHazards(hazardRoot.transform, fireSources, player, stageSystems, coordinator);
                ConfigureWindHazards(
                    hazardRoot.transform,
                    windSources,
                    renderers,
                    player,
                    Require(scene, "G01_Spawn_FromF").transform,
                    Require(scene, "G01_Exit_ToH").transform);
                ConfigureLavaHazards(
                    hazardRoot.transform,
                    lavaSources,
                    renderers,
                    player,
                    coordinator,
                    Require(scene, "G01_Spawn_FromF").transform);

                var marker = GetOrCreateChild(hazardRoot.transform, CompletionMarkerName);
                marker.SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                ValidateAppliedScene(scene);
                Debug.Log(
                    $"[sragon000][G02] 화염 {fireSources.Length}, 바람 {windSources.Length}, " +
                    $"용암 {lavaSources.Length}개를 G 원본 색상 도형에 연결했습니다.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static TutorialEnvironmentHazardCoordinatorHost ConfigureCoordinator(
            Scene scene,
            GameObject target,
            GameObject player,
            GameObject stageSystems,
            Transform defaultSafePoint)
        {
            var coordinator = target.GetComponent<TutorialEnvironmentHazardCoordinatorHost>();
            if (coordinator == null)
                coordinator = target.AddComponent<TutorialEnvironmentHazardCoordinatorHost>();
            var serialized = new SerializedObject(coordinator);
            SetObject(serialized, "combatSystemHost", stageSystems.GetComponent<CombatSystemHost>());
            SetObject(serialized, "playerActor", player.GetComponent<CombatActorHost>());
            SetObject(serialized, "playerBody", player.GetComponent<Rigidbody2D>());
            SetObject(serialized, "defaultSafePoint", defaultSafePoint);
            serialized.FindProperty("lavaDamageFraction").floatValue = 0.2f;
            serialized.FindProperty("lavaRetriggerDelay").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return coordinator;
        }

        private static void ConfigureFireHazards(
            Transform root,
            ClassifiedRenderer[] sources,
            GameObject player,
            GameObject stageSystems,
            TutorialEnvironmentHazardCoordinatorHost coordinator)
        {
            var parent = GetOrCreateChild(root, "화염 위험물");
            for (var index = 0; index < sources.Length; index++)
            {
                var source = sources[index].renderer;
                var proxy = CreateTriggerProxy(
                    parent.transform,
                    $"G02_화염_{index + 1:00}_PROXY",
                    source.bounds.center,
                    source.bounds.size);
                var hazard = proxy.GetComponent<TutorialFireHazardHost>();
                if (hazard == null) hazard = proxy.AddComponent<TutorialFireHazardHost>();
                var serialized = new SerializedObject(hazard);
                SetObject(serialized, "combatSystemHost", stageSystems.GetComponent<CombatSystemHost>());
                SetObject(serialized, "playerActor", player.GetComponent<CombatActorHost>());
                SetObject(serialized, "playerBody", player.GetComponent<Rigidbody2D>());
                SetObject(serialized, "player", player.transform);
                SetObject(serialized, "sourceRenderer", source);
                serialized.FindProperty("hazardId").stringValue = $"G-FIRE-{index + 1:00}";
                serialized.FindProperty("damageFraction").floatValue = 0.1f;
                serialized.FindProperty("warningDuration").floatValue = 0.65f;
                serialized.FindProperty("burstDuration").floatValue = 1f;
                serialized.FindProperty("restDuration").floatValue = 1.2f;
                serialized.FindProperty("horizontalKnockback").floatValue = 4.5f;
                serialized.FindProperty("verticalKnockback").floatValue = 5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigureWindHazards(
            Transform root,
            ClassifiedRenderer[] sources,
            Renderer[] allRenderers,
            GameObject player,
            Transform stageStart,
            Transform stageExit)
        {
            var parent = GetOrCreateChild(root, "바람 상승기류");
            var ascentTopY = stageExit.position.y + 2f;
            ConfigureWindMarker(
                parent.transform,
                "G02_바람_시작_MARKER",
                stageStart.position + Vector3.up * 5f,
                Quaternion.identity,
                new Vector2(6f, 12f),
                player);
            for (var index = 0; index < sources.Length; index++)
            {
                var source = sources[index].renderer;
                var bounds = source.bounds;
                var height = Mathf.Max(bounds.size.y, 8f);
                var localWidth = Mathf.Abs(source.localBounds.size.x * source.transform.lossyScale.x);
                var markerName = $"G02_바람_{index + 1:00}_MARKER";
                var legacy = parent.transform.Find($"G02_바람_{index + 1:00}_PROXY");
                if (parent.transform.Find(markerName) == null && legacy != null)
                {
                    legacy.name = markerName;
                    legacy.SetPositionAndRotation(
                        new Vector3(bounds.center.x, bounds.min.y + height * 0.5f, 0f),
                        source.transform.rotation);
                    var legacyCollider = legacy.GetComponent<BoxCollider2D>();
                    if (legacyCollider != null)
                        legacyCollider.size = new Vector2(Mathf.Max(localWidth, 6f), height);
                }
                var windMarker = ConfigureWindMarker(
                    parent.transform,
                    markerName,
                    new Vector3(bounds.center.x, bounds.min.y + height * 0.5f, 0f),
                    source.transform.rotation,
                    new Vector2(Mathf.Max(localWidth, 6f), height),
                    player);
                EnsureVerticalCoverage(windMarker, ascentTopY);
            }

            var leftWall = allRenderers.FirstOrDefault(renderer => renderer.name == "Square (21)");
            var rightWall = allRenderers.FirstOrDefault(renderer => renderer.name == "Square (28)");
            if (leftWall != null && rightWall != null)
            {
                var leftBounds = leftWall.bounds;
                var rightBounds = rightWall.bounds;
                var passageLeft = leftBounds.max.x;
                var passageRight = rightBounds.min.x;
                var passageBottom = Mathf.Min(leftBounds.min.y, rightBounds.min.y);
                var passageTop = Mathf.Max(leftBounds.max.y, rightBounds.max.y) + 1f;
                if (passageRight > passageLeft + 0.5f)
                {
                    ConfigureWindMarker(
                        parent.transform,
                        "G02_바람_중간통로_MARKER",
                        new Vector3(
                            (passageLeft + passageRight) * 0.5f,
                            (passageBottom + passageTop) * 0.5f,
                            0f),
                        Quaternion.identity,
                        new Vector2(
                            Mathf.Max(1f, passageRight - passageLeft - 0.2f),
                            passageTop - passageBottom),
                        player);
                }
            }
        }

        private static GameObject ConfigureWindMarker(
            Transform parent,
            string name,
            Vector3 suggestedCenter,
            Quaternion suggestedRotation,
            Vector2 suggestedSize,
            GameObject player)
        {
            var existing = parent.Find(name);
            var marker = existing != null ? existing.gameObject : GetOrCreateChild(parent, name);
            if (existing == null)
            {
                marker.transform.SetPositionAndRotation(suggestedCenter, suggestedRotation);
                marker.transform.localScale = Vector3.one;
            }

            var collider = marker.GetComponent<BoxCollider2D>();
            if (collider == null) collider = marker.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            if (existing == null) collider.size = suggestedSize;
            else if (collider.size.x < suggestedSize.x)
                collider.size = new Vector2(suggestedSize.x, collider.size.y);

            var functionMarker = marker.GetComponent<TutorialFunctionMarkerHost>();
            if (functionMarker == null) functionMarker = marker.AddComponent<TutorialFunctionMarkerHost>();
            var markerSerialized = new SerializedObject(functionMarker);
            markerSerialized.FindProperty("markerId").stringValue = name;
            markerSerialized.FindProperty("kind").enumValueIndex =
                (int)TutorialFunctionMarkerKind.Wind;
            markerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var hazard = marker.GetComponent<TutorialWindHazardHost>();
            if (hazard == null) hazard = marker.AddComponent<TutorialWindHazardHost>();
            var serialized = new SerializedObject(hazard);
            SetObject(serialized, "playerBody", player.GetComponent<Rigidbody2D>());
            SetObject(serialized, "player", player.transform);
            SetObject(serialized, "playerMotor", player.GetComponent<PlayerMotorHost>());
            serialized.FindProperty("liftAcceleration").floatValue = 32f;
            serialized.FindProperty("maximumRiseSpeed").floatValue = 12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        private static void EnsureVerticalCoverage(GameObject marker, float requiredTopY)
        {
            var collider = marker.GetComponent<BoxCollider2D>();
            if (collider == null || marker.transform.parent == null) return;

            // Collider2D.bounds is empty while G's integration root is inactive in edit mode.
            // Work in the marker parent's local space so setup remains deterministic.
            var requiredTopLocalY = marker.transform.parent
                .InverseTransformPoint(new Vector3(marker.transform.position.x, requiredTopY, 0f)).y;
            var verticalScale = Mathf.Max(0.0001f, Mathf.Abs(marker.transform.localScale.y));
            var colliderCenterLocalY =
                marker.transform.localPosition.y + collider.offset.y * verticalScale;
            var bottomLocalY = colliderCenterLocalY - collider.size.y * verticalScale * 0.5f;
            var currentTopLocalY = colliderCenterLocalY + collider.size.y * verticalScale * 0.5f;
            if (currentTopLocalY >= requiredTopLocalY - 0.05f) return;

            collider.size = new Vector2(
                collider.size.x,
                (requiredTopLocalY - bottomLocalY) / verticalScale);
            var requiredCenterLocalY = (bottomLocalY + requiredTopLocalY) * 0.5f;
            var localPosition = marker.transform.localPosition;
            localPosition.y = requiredCenterLocalY - collider.offset.y * verticalScale;
            marker.transform.localPosition = localPosition;
        }

        private static void ConfigureLavaHazards(
            Transform root,
            ClassifiedRenderer[] sources,
            Renderer[] allRenderers,
            GameObject player,
            TutorialEnvironmentHazardCoordinatorHost coordinator,
            Transform fallbackSafePoint)
        {
            var parent = GetOrCreateChild(root, "용암 위험물");
            for (var index = 0; index < sources.Length; index++)
            {
                var source = sources[index].renderer;
                var safePosition = FindSafePosition(source.bounds, allRenderers, fallbackSafePoint.position);
                var safePoint = GetOrCreateAnchor(
                    parent.transform,
                    $"G02_용암_{index + 1:00}_안전지점",
                    safePosition);
                var safeTrigger = CreateTriggerProxy(
                    parent.transform,
                    $"G02_용암_{index + 1:00}_안전지점_TRIGGER",
                    safePosition + Vector3.up * 0.35f,
                    new Vector3(1.4f, 2.8f, 0.1f));
                var safeHost = safeTrigger.GetComponent<TutorialSafePointTriggerHost>();
                if (safeHost == null) safeHost = safeTrigger.AddComponent<TutorialSafePointTriggerHost>();
                var safeSerialized = new SerializedObject(safeHost);
                SetObject(safeSerialized, "coordinator", coordinator);
                SetObject(safeSerialized, "player", player.transform);
                SetObject(safeSerialized, "safePoint", safePoint);
                safeSerialized.ApplyModifiedPropertiesWithoutUndo();

                var lavaProxy = CreateTriggerProxy(
                    parent.transform,
                    $"G02_용암_{index + 1:00}_PROXY",
                    source.bounds.center,
                    source.bounds.size);
                var lava = lavaProxy.GetComponent<TutorialLavaHazardHost>();
                if (lava == null) lava = lavaProxy.AddComponent<TutorialLavaHazardHost>();
                var lavaSerialized = new SerializedObject(lava);
                SetObject(lavaSerialized, "coordinator", coordinator);
                SetObject(lavaSerialized, "player", player.transform);
                lavaSerialized.FindProperty("hazardId").stringValue = $"G-LAVA-{index + 1:00}";
                lavaSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Vector3 FindSafePosition(
            Bounds hazardBounds,
            Renderer[] renderers,
            Vector3 fallback)
        {
            var floor = renderers
                .Where(IsWhiteBlockout)
                .Where(renderer => renderer.bounds.size.x >= 1.5f &&
                                   renderer.bounds.size.y <= 2.2f &&
                                   renderer.bounds.max.x <= hazardBounds.min.x + 0.5f)
                .OrderBy(renderer =>
                {
                    var horizontal = Mathf.Abs(hazardBounds.min.x - renderer.bounds.max.x);
                    var vertical = Mathf.Abs(hazardBounds.min.y - renderer.bounds.max.y);
                    return horizontal + vertical * 1.5f;
                })
                .FirstOrDefault();
            if (floor == null) return fallback;
            return new Vector3(
                Mathf.Min(floor.bounds.max.x - 0.6f, hazardBounds.min.x - 1f),
                floor.bounds.max.y + 0.8f,
                0f);
        }

        private static GameObject CreateTriggerProxy(
            Transform parent,
            string name,
            Vector3 worldCenter,
            Vector3 worldSize)
        {
            var proxy = GetOrCreateChild(parent, name);
            proxy.transform.SetPositionAndRotation(
                new Vector3(worldCenter.x, worldCenter.y, 0f),
                Quaternion.identity);
            proxy.transform.localScale = Vector3.one;
            var collider = proxy.GetComponent<BoxCollider2D>();
            if (collider == null) collider = proxy.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(
                Mathf.Max(0.1f, worldSize.x),
                Mathf.Max(0.1f, worldSize.y));
            return proxy;
        }

        private static void ClearGeneratedChildren(Transform hazardRoot)
        {
            var keep = hazardRoot.Find("G02_HazardController");
            for (var index = hazardRoot.childCount - 1; index >= 0; index--)
            {
                var child = hazardRoot.GetChild(index);
                if (child == keep) continue;
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Color GetColor(Renderer renderer)
        {
            if (renderer is SpriteRenderer spriteRenderer) return spriteRenderer.color;
            var material = renderer.sharedMaterial;
            if (material == null) return Color.white;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.color;
            return Color.white;
        }

        private static HazardKind Classify(Color color)
        {
            if (color.b >= 0.38f && color.b > color.r * 1.15f &&
                color.b >= color.g * 0.9f)
                return HazardKind.Wind;
            if (color.r >= 0.2f && color.r < 0.65f &&
                color.g <= 0.22f && color.b <= 0.22f &&
                color.r > color.g * 2.5f && color.r > color.b * 2.5f)
                return HazardKind.Lava;
            if (color.r >= 0.45f && color.r > color.g * 1.3f &&
                color.r > color.b * 1.2f)
                return HazardKind.Fire;
            return HazardKind.None;
        }

        private static bool IsWhiteBlockout(Renderer renderer)
        {
            var color = GetColor(renderer);
            var minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            var maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            return minimum >= 0.62f && maximum - minimum <= 0.18f;
        }

        private static void ValidateAppliedScene(Scene scene)
        {
            var coordinator = Require(scene, "G02_HazardController")
                .GetComponent<TutorialEnvironmentHazardCoordinatorHost>();
            if (coordinator == null || !coordinator.HasValidSetup ||
                !Mathf.Approximately(coordinator.LavaDamageFraction, 0.2f))
                throw new InvalidOperationException("G 환경 위험물 조정자 참조 또는 용암 피해율이 유효하지 않습니다.");

            var fires = FindSceneComponents<TutorialFireHazardHost>(scene);
            var winds = Require(scene, "G02_EnvironmentHazards")
                .GetComponentsInChildren<TutorialWindHazardHost>(true);
            var lavas = FindSceneComponents<TutorialLavaHazardHost>(scene);
            var safePoints = FindSceneComponents<TutorialSafePointTriggerHost>(scene);
            if (fires.Length == 0 || fires.Any(host => !host.HasValidSetup ||
                                                      !Mathf.Approximately(host.DamageFraction, 0.1f)))
                throw new InvalidOperationException("G 화염 위험물은 유효한 참조와 최대 체력 10% 피해를 가져야 합니다.");
            if (winds.Length == 0 || winds.Any(host => !host.HasValidSetup || !host.RequiresGlideInput))
                throw new InvalidOperationException("G 바람은 Space 활공 입력을 요구하는 유효한 상승기류여야 합니다.");
            if (lavas.Length == 0 || lavas.Any(host => !host.HasValidSetup || !host.ReturnsToLatestSafePoint))
                throw new InvalidOperationException("G 용암은 최근 안전지점 복귀 참조가 유효해야 합니다.");
            if (safePoints.Length < lavas.Length || safePoints.Any(host => !host.HasValidSetup))
                throw new InvalidOperationException("각 G 용암 앞에 유효한 안전지점 트리거가 있어야 합니다.");
            if (TutorialEnvironmentHazardPolicy.ResolveFractionalDamage(100, 0.1f) != 10 ||
                TutorialEnvironmentHazardPolicy.ResolveFractionalDamage(100, 0.2f) != 20 ||
                TutorialEnvironmentHazardPolicy.ShouldApplyWind(true, false) ||
                !TutorialEnvironmentHazardPolicy.ShouldApplyWind(true, true) ||
                TutorialEnvironmentHazardPolicy.ShouldReturnToSafePoint(false))
                throw new InvalidOperationException("G 위험물 피해·활공 입력·치명상 정책 검증에 실패했습니다.");

            Debug.Log(
                $"[sragon000][G02][검증 통과] 화염 {fires.Length}개 10% 피해·넉백, " +
                $"바람 {winds.Length}개 Space 조건, 용암 {lavas.Length}개 20% 피해·안전복귀 정상.");
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

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void SetObject(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    $"{serialized.targetObject.GetType().Name}.{propertyName} 필드가 없습니다.");
            property.objectReferenceValue = value;
        }

        private static Transform GetOrCreateAnchor(
            Transform parent,
            string name,
            Vector3 worldPosition)
        {
            var target = GetOrCreateChild(parent, name);
            target.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            target.transform.localScale = Vector3.one;
            return target.transform;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created;
        }

        private enum HazardKind
        {
            None,
            Fire,
            Wind,
            Lava
        }

        private readonly struct ClassifiedRenderer
        {
            public readonly Renderer renderer;
            public readonly Color color;
            public readonly HazardKind kind;

            public ClassifiedRenderer(Renderer renderer, Color color)
            {
                this.renderer = renderer;
                this.color = color;
                kind = Classify(color);
            }
        }
    }
}
