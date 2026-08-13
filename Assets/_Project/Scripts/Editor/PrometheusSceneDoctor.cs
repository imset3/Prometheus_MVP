using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using Narthex.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.Tools
{
    public static class PrometheusSceneDoctor
    {
        public static List<PrometheusAiIssue> Scan(Scene scene)
        {
            var issues = new List<PrometheusAiIssue>();
            var all = PrometheusSceneQuery.All(scene).ToArray();
            ScanMissingScripts(all, issues);
            ScanMarkerIds(all, issues);
            ScanInvisibleSolidColliders(all, issues);
            ScanInvalidColliders(all, issues);
            ScanDuplicateSceneObjects(all, issues, "Passkey", "패스키");
            ScanBrokenObjectReferences(all, issues);
            ScanAuthoredRuntimeContracts(scene, all, issues);
            return issues
                .OrderByDescending(issue => issue.severity)
                .ThenBy(issue => issue.hierarchyPath, StringComparer.Ordinal)
                .ToList();
        }

        public static List<PrometheusAiChange> RepairSafe(Scene scene, bool dryRun)
        {
            var changes = new List<PrometheusAiChange>();
            var markers = PrometheusSceneQuery.All(scene)
                .Select(item => item.GetComponent<TutorialFunctionMarkerHost>())
                .Where(marker => marker != null && string.IsNullOrWhiteSpace(marker.MarkerId))
                .ToArray();

            foreach (var marker in markers)
            {
                var generated = $"AUTO-{marker.Kind.ToString().ToUpperInvariant()}-{Guid.NewGuid():N}";
                changes.Add(new PrometheusAiChange
                {
                    action = "assign-marker-id",
                    objectId = PrometheusSceneQuery.ObjectId(marker.gameObject),
                    hierarchyPath = PrometheusSceneQuery.Path(marker.gameObject),
                    before = "",
                    after = generated
                });
                if (dryRun) continue;
                Undo.RecordObject(marker, "Assign stable marker ID");
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("markerId").stringValue = generated;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(marker);
            }

            if (!dryRun && changes.Count > 0) EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        public static bool TryFocus(PrometheusAiIssue issue)
        {
            if (issue == null || string.IsNullOrWhiteSpace(issue.objectId) ||
                !GlobalObjectId.TryParse(issue.objectId, out var id))
                return false;
            var target = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
            if (target == null) return false;
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
            SceneView.lastActiveSceneView?.FrameSelected();
            return true;
        }

        private static void ScanMissingScripts(
            IEnumerable<GameObject> objects,
            ICollection<PrometheusAiIssue> issues)
        {
            foreach (var item in objects)
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item) > 0)
                    issues.Add(Issue(item, PrometheusIssueSeverity.Error, "missing-script",
                        "Missing Script 컴포넌트가 있습니다."));
        }

        private static void ScanMarkerIds(
            IEnumerable<GameObject> objects,
            ICollection<PrometheusAiIssue> issues)
        {
            var markers = objects.Select(item => item.GetComponent<TutorialFunctionMarkerHost>())
                .Where(marker => marker != null)
                .ToArray();
            foreach (var marker in markers.Where(marker => string.IsNullOrWhiteSpace(marker.MarkerId)))
                issues.Add(Issue(marker.gameObject, PrometheusIssueSeverity.Error, "marker-id-missing",
                    "AI가 안정적으로 찾을 수 있도록 markerId가 필요합니다.", true));

            foreach (var group in markers.Where(marker => !string.IsNullOrWhiteSpace(marker.MarkerId))
                         .GroupBy(marker => marker.MarkerId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
                foreach (var marker in group)
                    issues.Add(Issue(marker.gameObject, PrometheusIssueSeverity.Error, "marker-id-duplicate",
                        $"markerId '{group.Key}'가 씬에서 중복됩니다."));
        }

        private static void ScanInvisibleSolidColliders(
            IEnumerable<GameObject> objects,
            ICollection<PrometheusAiIssue> issues)
        {
            foreach (var item in objects)
            {
                if (!item.activeInHierarchy || PrometheusSceneQuery.IsTechnicalCollider(item) ||
                    PrometheusSceneQuery.HasVisibleRenderer(item))
                    continue;
                foreach (var collider in item.GetComponents<Collider2D>())
                {
                    if (!collider.enabled || collider.isTrigger) continue;
                    issues.Add(Issue(item, PrometheusIssueSeverity.Warning, "invisible-solid-collider",
                        $"보이지 않는 {collider.GetType().Name}가 플레이 경로를 막을 수 있습니다."));
                    break;
                }
            }
        }

        private static void ScanInvalidColliders(
            IEnumerable<GameObject> objects,
            ICollection<PrometheusAiIssue> issues)
        {
            foreach (var item in objects)
            foreach (var collider in item.GetComponents<Collider2D>())
            {
                if (collider is BoxCollider2D box && (box.size.x <= 0.001f || box.size.y <= 0.001f))
                    issues.Add(Issue(item, PrometheusIssueSeverity.Error, "collider-zero-size",
                        "BoxCollider2D의 크기가 0에 가깝습니다."));
            }
        }

        private static void ScanDuplicateSceneObjects(
            IEnumerable<GameObject> objects,
            ICollection<PrometheusAiIssue> issues,
            params string[] tokens)
        {
            var candidates = objects.Where(item =>
                    tokens.Any(token => item.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                .Where(item => item.activeInHierarchy)
                .ToArray();
            if (candidates.Length <= 1) return;
            foreach (var candidate in candidates)
                issues.Add(Issue(candidate, PrometheusIssueSeverity.Info, "possible-duplicate-passkey",
                    $"활성 패스키 후보가 {candidates.Length}개입니다. 의도된 구성인지 확인하세요."));
        }

        private static void ScanBrokenObjectReferences(
            IEnumerable<GameObject> objects,
            ICollection<PrometheusAiIssue> issues)
        {
            foreach (var item in objects)
            foreach (var behaviour in item.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                var serialized = new SerializedObject(behaviour);
                var property = serialized.GetIterator();
                if (!property.NextVisible(true)) continue;
                do
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue != null ||
                        property.objectReferenceInstanceIDValue == 0)
                        continue;
                    issues.Add(Issue(item, PrometheusIssueSeverity.Error, "broken-object-reference",
                        $"{behaviour.GetType().Name}.{property.propertyPath} 참조가 손상되었습니다."));
                } while (property.NextVisible(false));
            }
        }

        private static void ScanAuthoredRuntimeContracts(
            Scene scene,
            IReadOnlyCollection<GameObject> objects,
            ICollection<PrometheusAiIssue> issues)
        {
            if (scene.name is not ("TitleScene" or "TutorialScene" or "BossDevelopmentScene")) return;
            var target = scene.GetRootGameObjects().FirstOrDefault();
            if (target == null) return;
            foreach (var required in new[] { "SafeAreaRoot", "PrimaryHudRoot", "ModalRoot", "DialogueRoot", "TransitionRoot" })
                if (!objects.Any(item => item.name == required))
                    issues.Add(Issue(target, PrometheusIssueSeverity.Error, "authored-ui-root-missing",
                        $"필수 계층 UI 루트 '{required}'가 없습니다. ui.readability.apply를 실행하세요."));

            if (scene.name == "TitleScene")
            {
                if (objects.Any(item => item.name == "보스전"))
                    issues.Add(Issue(target, PrometheusIssueSeverity.Error, "release-boss-route-present",
                        "릴리즈 타이틀에 보스전 버튼이 남아 있습니다."));
                var resolutionDropdown = objects.SelectMany(item => item.GetComponents<Dropdown>())
                    .FirstOrDefault(dropdown => dropdown.name == "ResolutionDropdown");
                if (resolutionDropdown == null)
                    issues.Add(Issue(target, PrometheusIssueSeverity.Error, "resolution-dropdown-missing",
                        "계층에 직렬화된 해상도 드롭다운이 없습니다."));
                if (!objects.Any(item => item.name == "ResolutionConfirmPanel"))
                    issues.Add(Issue(target, PrometheusIssueSeverity.Error, "resolution-confirm-missing",
                        "10초 해상도 확인 패널이 없습니다."));
            }

            if (scene.name == "TutorialScene")
            {
                var pause = objects.SelectMany(item => item.GetComponents<TutorialPauseMenuHost>()).FirstOrDefault();
                if (pause == null || !pause.HasAuthoredSetup())
                    issues.Add(Issue(target, PrometheusIssueSeverity.Error, "pause-ui-not-authored",
                        "일시정지 UI와 Inspector 참조가 계층에 완전히 작성되지 않았습니다."));
                var restart = objects.SelectMany(item => item.GetComponents<TutorialRestartHost>()).FirstOrDefault();
                if (restart == null || !restart.HasValidSceneRestartSetup)
                    issues.Add(Issue(target, PrometheusIssueSeverity.Error, "retry-contract-incomplete",
                        "패배 패널, 퀘스트, 체크포인트 또는 복구 참가자 참조가 누락됐습니다."));
            }
        }

        private static PrometheusAiIssue Issue(
            GameObject target,
            PrometheusIssueSeverity severity,
            string rule,
            string message,
            bool canAutoRepair = false) =>
            new()
            {
                id = $"{rule}:{PrometheusSceneQuery.ObjectId(target)}",
                severity = severity,
                rule = rule,
                message = message,
                objectId = PrometheusSceneQuery.ObjectId(target),
                hierarchyPath = PrometheusSceneQuery.Path(target),
                canAutoRepair = canAutoRepair
            };
    }
}
