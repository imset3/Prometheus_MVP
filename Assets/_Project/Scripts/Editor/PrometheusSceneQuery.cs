using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    internal static class PrometheusSceneQuery
    {
        public static IEnumerable<GameObject> All(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                yield return transform.gameObject;
        }

        public static string Path(GameObject gameObject)
        {
            if (gameObject == null) return string.Empty;
            var names = new Stack<string>();
            for (var cursor = gameObject.transform; cursor != null; cursor = cursor.parent)
                names.Push(cursor.name);
            return string.Join("/", names);
        }

        public static string ObjectId(GameObject gameObject) =>
            gameObject == null ? string.Empty : GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString();

        public static GameObject Resolve(Scene scene, string markerId, string hierarchyPath, string objectId)
        {
            if (!string.IsNullOrWhiteSpace(objectId) &&
                GlobalObjectId.TryParse(objectId, out var globalId) &&
                GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) is GameObject byId &&
                byId.scene == scene)
                return byId;

            if (!string.IsNullOrWhiteSpace(markerId))
            {
                var matches = All(scene)
                    .Where(item =>
                    {
                        var marker = item.GetComponent<TutorialFunctionMarkerHost>();
                        return marker != null &&
                               string.Equals(marker.MarkerId, markerId, StringComparison.Ordinal);
                    })
                    .ToArray();
                if (matches.Length == 1) return matches[0];
            }

            return string.IsNullOrWhiteSpace(hierarchyPath)
                ? null
                : All(scene).FirstOrDefault(item =>
                    string.Equals(Path(item), hierarchyPath, StringComparison.Ordinal));
        }

        public static bool HasVisibleRenderer(GameObject gameObject)
        {
            if (gameObject == null) return false;
            return gameObject.GetComponentsInChildren<Renderer>(false)
                .Any(renderer => renderer != null && renderer.enabled);
        }

        public static bool IsTechnicalCollider(GameObject gameObject)
        {
            if (gameObject == null) return false;
            var name = gameObject.name;
            return name.IndexOf("Trigger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Marker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Boundary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   gameObject.GetComponent<TutorialFunctionMarkerHost>() != null;
        }
    }
}
