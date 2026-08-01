using System;
using System.Collections.Generic;
using UnityEngine;

namespace Narthex.Tools
{
    [Serializable]
    public sealed class PrometheusAiCommandRequest
    {
        public string version = "1";
        public string requestId;
        public string command;
        public string scenePath;
        public bool dryRun = true;
        public List<PrometheusAiArgument> arguments = new();

        public string Get(string key, string fallback = "")
        {
            if (arguments == null) return fallback;
            foreach (var argument in arguments)
                if (argument != null && string.Equals(argument.key, key, StringComparison.OrdinalIgnoreCase))
                    return argument.value ?? fallback;
            return fallback;
        }

        public float GetFloat(string key, float fallback = 0f) =>
            float.TryParse(Get(key), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
    }

    [Serializable]
    public sealed class PrometheusAiArgument
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class PrometheusAiCommandResponse
    {
        public string version = "1";
        public string requestId;
        public string command;
        public bool success;
        public bool changed;
        public string scenePath;
        public string message;
        public List<PrometheusAiIssue> issues = new();
        public List<PrometheusAiChange> changes = new();
        public List<PrometheusAiRecord> records = new();
        public string artifactPath;
    }

    public enum PrometheusIssueSeverity
    {
        Info,
        Warning,
        Error
    }

    [Serializable]
    public sealed class PrometheusAiIssue
    {
        public string id;
        public PrometheusIssueSeverity severity;
        public string rule;
        public string message;
        public string objectId;
        public string hierarchyPath;
        public bool canAutoRepair;
    }

    [Serializable]
    public sealed class PrometheusAiChange
    {
        public string action;
        public string objectId;
        public string hierarchyPath;
        public string before;
        public string after;
    }

    [Serializable]
    public sealed class PrometheusAiRecord
    {
        public string id;
        public string kind;
        public string hierarchyPath;
        public string value;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public bool active;
    }

    [Serializable]
    public sealed class PrometheusSceneSnapshot
    {
        public string version = "1";
        public string scenePath;
        public string sceneGuid;
        public string capturedAtUtc;
        public List<PrometheusSceneObjectSnapshot> objects = new();
    }

    [Serializable]
    public sealed class PrometheusSceneObjectSnapshot
    {
        public string objectId;
        public string hierarchyPath;
        public string name;
        public bool activeSelf;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale;
        public string markerId;
        public string markerKind;
        public List<string> components = new();
        public List<PrometheusColliderSnapshot> colliders = new();
    }

    [Serializable]
    public sealed class PrometheusColliderSnapshot
    {
        public string type;
        public bool enabled;
        public bool isTrigger;
        public Vector2 offset;
        public Vector2 size;
    }

    [Serializable]
    public sealed class PrometheusSceneDiff
    {
        public string beforePath;
        public string afterPath;
        public List<PrometheusAiChange> added = new();
        public List<PrometheusAiChange> removed = new();
        public List<PrometheusAiChange> modified = new();
    }
}
