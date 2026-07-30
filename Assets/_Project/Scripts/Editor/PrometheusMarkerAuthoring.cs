using System;
using System.Linq;
using Narthex.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusMarkerAuthoring
    {
        public static GameObject Create(
            Scene scene,
            Transform parent,
            TutorialFunctionMarkerKind kind,
            string markerId,
            Vector3 position,
            Vector2 size,
            bool registerUndo = true)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("A loaded scene is required.");
            if (string.IsNullOrWhiteSpace(markerId))
                markerId = $"AUTO-{kind.ToString().ToUpperInvariant()}-{Guid.NewGuid():N}";
            if (FindById(scene, markerId) != null)
                throw new InvalidOperationException($"Marker ID already exists: {markerId}");

            var gameObject = new GameObject(markerId);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            if (registerUndo) Undo.RegisterCreatedObjectUndo(gameObject, "Create Prometheus Marker");
            gameObject.transform.SetParent(parent, true);
            gameObject.transform.position = position;

            var marker = gameObject.AddComponent<TutorialFunctionMarkerHost>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("markerId").stringValue = markerId;
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.FindProperty("gizmoSize").vector2Value = size;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (UsesArea(kind))
            {
                var collider = gameObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y));
            }
            EditorUtility.SetDirty(gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            return gameObject;
        }

        public static GameObject FindById(Scene scene, string markerId) =>
            PrometheusSceneQuery.All(scene).FirstOrDefault(item =>
            {
                var marker = item.GetComponent<TutorialFunctionMarkerHost>();
                return marker != null && string.Equals(marker.MarkerId, markerId, StringComparison.Ordinal);
            });

        public static bool Move(
            Scene scene,
            string markerId,
            Vector3 position,
            float rotationZ,
            Vector2? areaSize,
            bool dryRun,
            out PrometheusAiChange change)
        {
            var target = FindById(scene, markerId);
            if (target == null)
            {
                change = null;
                return false;
            }
            var before = $"position={target.transform.position}; rotationZ={target.transform.eulerAngles.z}";
            var after = $"position={position}; rotationZ={rotationZ}";
            change = new PrometheusAiChange
            {
                action = "move-marker",
                objectId = PrometheusSceneQuery.ObjectId(target),
                hierarchyPath = PrometheusSceneQuery.Path(target),
                before = before,
                after = after
            };
            if (dryRun) return true;

            Undo.RecordObject(target.transform, "Move Prometheus Marker");
            target.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotationZ));
            if (areaSize.HasValue && target.TryGetComponent<BoxCollider2D>(out var collider))
            {
                Undo.RecordObject(collider, "Resize Prometheus Marker");
                collider.size = areaSize.Value;
            }
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static bool UsesArea(TutorialFunctionMarkerKind kind) =>
            kind == TutorialFunctionMarkerKind.Wind ||
            kind == TutorialFunctionMarkerKind.Transition ||
            kind == TutorialFunctionMarkerKind.Interaction ||
            kind == TutorialFunctionMarkerKind.FallRecovery ||
            kind == TutorialFunctionMarkerKind.TrainingFinish;
    }
}
