using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusSceneSnapshotService
    {
        public const string DefaultDirectory = "Temp/PrometheusSceneToolkit/Snapshots";

        public static PrometheusSceneSnapshot Capture(Scene scene)
        {
            var snapshot = new PrometheusSceneSnapshot
            {
                scenePath = scene.path,
                sceneGuid = AssetDatabase.AssetPathToGUID(scene.path),
                capturedAtUtc = DateTime.UtcNow.ToString("O")
            };

            foreach (var gameObject in PrometheusSceneQuery.All(scene)
                         .OrderBy(PrometheusSceneQuery.Path, StringComparer.Ordinal))
            {
                var marker = gameObject.GetComponent<TutorialFunctionMarkerHost>();
                var item = new PrometheusSceneObjectSnapshot
                {
                    objectId = PrometheusSceneQuery.ObjectId(gameObject),
                    hierarchyPath = PrometheusSceneQuery.Path(gameObject),
                    name = gameObject.name,
                    activeSelf = gameObject.activeSelf,
                    localPosition = gameObject.transform.localPosition,
                    localEulerAngles = gameObject.transform.localEulerAngles,
                    localScale = gameObject.transform.localScale,
                    markerId = marker != null ? marker.MarkerId : string.Empty,
                    markerKind = marker != null ? marker.Kind.ToString() : string.Empty,
                    components = gameObject.GetComponents<Component>()
                        .Select(component => component == null ? "MissingScript" : component.GetType().FullName)
                        .ToList()
                };

                foreach (var collider in gameObject.GetComponents<Collider2D>())
                {
                    var colliderSnapshot = new PrometheusColliderSnapshot
                    {
                        type = collider.GetType().Name,
                        enabled = collider.enabled,
                        isTrigger = collider.isTrigger,
                        offset = collider.offset
                    };
                    if (collider is BoxCollider2D box) colliderSnapshot.size = box.size;
                    item.colliders.Add(colliderSnapshot);
                }
                snapshot.objects.Add(item);
            }
            return snapshot;
        }

        public static string Save(PrometheusSceneSnapshot snapshot, string outputPath = null)
        {
            Directory.CreateDirectory(DefaultDirectory);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                var sceneName = string.IsNullOrWhiteSpace(snapshot.scenePath)
                    ? "Untitled"
                    : Path.GetFileNameWithoutExtension(snapshot.scenePath);
                outputPath = $"{DefaultDirectory}/{sceneName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            }
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(snapshot, true));
            return outputPath;
        }

        public static PrometheusSceneSnapshot Load(string path) =>
            JsonUtility.FromJson<PrometheusSceneSnapshot>(File.ReadAllText(path));

        public static PrometheusSceneDiff Compare(
            PrometheusSceneSnapshot before,
            PrometheusSceneSnapshot after,
            string beforePath = "",
            string afterPath = "")
        {
            var diff = new PrometheusSceneDiff { beforePath = beforePath, afterPath = afterPath };
            var beforeById = before.objects.ToDictionary(item => item.objectId);
            var afterById = after.objects.ToDictionary(item => item.objectId);

            foreach (var pair in afterById)
            {
                if (!beforeById.TryGetValue(pair.Key, out var oldItem))
                {
                    diff.added.Add(Change("add", pair.Value, "", Describe(pair.Value)));
                    continue;
                }
                var oldDescription = Describe(oldItem);
                var newDescription = Describe(pair.Value);
                if (!string.Equals(oldDescription, newDescription, StringComparison.Ordinal))
                    diff.modified.Add(Change("modify", pair.Value, oldDescription, newDescription));
            }
            foreach (var pair in beforeById)
                if (!afterById.ContainsKey(pair.Key))
                    diff.removed.Add(Change("remove", pair.Value, Describe(pair.Value), ""));
            return diff;
        }

        private static PrometheusAiChange Change(
            string action,
            PrometheusSceneObjectSnapshot item,
            string before,
            string after) =>
            new()
            {
                action = action,
                objectId = item.objectId,
                hierarchyPath = item.hierarchyPath,
                before = before,
                after = after
            };

        private static string Describe(PrometheusSceneObjectSnapshot item) =>
            JsonUtility.ToJson(item, false);
    }
}
