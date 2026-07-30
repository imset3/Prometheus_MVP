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
