using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public enum PrometheusFlowCondition
    {
        None,
        MarkerReached,
        Interaction,
        DialogueCompleted,
        EnemiesDefeated,
        ItemAcquired,
        BossPhase,
        BossDefeated
    }

    [Serializable]
    public sealed class PrometheusZoneFlowNode
    {
        public string id;
        public string displayName;
        public string zoneId;
        public string markerId;
        public PrometheusFlowCondition condition;
        public string conditionValue;
        public List<string> nextNodeIds = new();
    }

    public sealed class PrometheusZoneFlowAsset : ScriptableObject
    {
        [SerializeField] private string scenePath;
        [SerializeField] private string entryNodeId;
        [SerializeField] private List<PrometheusZoneFlowNode> nodes = new();

        public string ScenePath => scenePath;
        public string EntryNodeId => entryNodeId;
        public IReadOnlyList<PrometheusZoneFlowNode> Nodes => nodes;

        public void Configure(string targetScenePath, string entry, IEnumerable<PrometheusZoneFlowNode> definitions)
        {
            scenePath = targetScenePath;
            entryNodeId = entry;
            nodes = definitions?.ToList() ?? new List<PrometheusZoneFlowNode>();
        }

        public List<PrometheusAiIssue> Validate(Scene scene)
        {
            var issues = new List<PrometheusAiIssue>();
            var duplicateIds = nodes.Where(node => node != null && !string.IsNullOrWhiteSpace(node.id))
                .GroupBy(node => node.id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1);
            foreach (var group in duplicateIds)
                issues.Add(FlowIssue("flow-node-duplicate", $"흐름 노드 ID '{group.Key}'가 중복됩니다."));

            var ids = new HashSet<string>(
                nodes.Where(node => node != null && !string.IsNullOrWhiteSpace(node.id)).Select(node => node.id),
                StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(entryNodeId) || !ids.Contains(entryNodeId))
                issues.Add(FlowIssue("flow-entry-missing", "유효한 시작 노드가 필요합니다."));

            foreach (var node in nodes.Where(node => node != null))
            {
                if (string.IsNullOrWhiteSpace(node.id))
                    issues.Add(FlowIssue("flow-node-id-missing", "ID가 없는 흐름 노드가 있습니다."));
                foreach (var next in node.nextNodeIds ?? new List<string>())
                    if (!ids.Contains(next))
                        issues.Add(FlowIssue("flow-edge-broken",
                            $"'{node.id}'가 존재하지 않는 다음 노드 '{next}'를 참조합니다."));
                if (!string.IsNullOrWhiteSpace(node.markerId) &&
                    PrometheusMarkerAuthoring.FindById(scene, node.markerId) == null)
                    issues.Add(FlowIssue("flow-marker-missing",
                        $"'{node.id}'가 존재하지 않는 마커 '{node.markerId}'를 참조합니다."));
            }
            return issues;
        }

        private static PrometheusAiIssue FlowIssue(string rule, string message) =>
            new()
            {
                id = $"{rule}:{Guid.NewGuid():N}",
                severity = PrometheusIssueSeverity.Error,
                rule = rule,
                message = message
            };
    }
}
