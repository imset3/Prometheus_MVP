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
    /// <summary>
    /// Promotes the three level-designer-authored ranged dummies into the actual
    /// combat targets.  It intentionally does not create replacement enemies.
    /// </summary>
    public static class PrometheusTrainingDummyIntegration
    {
        [MenuItem(PrometheusToolMenuPaths.Ai + "Use Existing Ranged Training Dummies")]
        public static void ApplyActiveScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var changes = Apply(scene, false);
            Debug.Log($"[sragon000][Training Dummy] Applied {changes.Count} change(s).");
        }

        public static IReadOnlyList<string> Apply(Scene scene, bool dryRun)
        {
            var visualRoot = Find(scene, "원거리공격훈련");
            var flow = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TutorialImportedTrainingFlowHost>(true))
                .FirstOrDefault();
            if (visualRoot == null || flow == null)
                throw new InvalidOperationException("Ranged training visual root or flow host is missing.");

            var dummies = visualRoot.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.gameObject.name.StartsWith("Enemy", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();
            if (dummies.Length != 3)
                throw new InvalidOperationException($"Expected exactly 3 authored ranged dummies, found {dummies.Length}.");

            var flowSerialized = new SerializedObject(flow);
            var oldTargets = flowSerialized.FindProperty("rangedTargets");
            if (oldTargets.arraySize != 3)
                throw new InvalidOperationException("Ranged flow does not have exactly 3 legacy targets to replace.");

            var legacyActors = new CombatActorHost[3];
            for (var index = 0; index < legacyActors.Length; index++)
            {
                var legacy = oldTargets.GetArrayElementAtIndex(index).objectReferenceValue as GameObject;
                legacyActors[index] = legacy != null ? legacy.GetComponent<CombatActorHost>() : null;
                if (legacyActors[index] == null)
                    throw new InvalidOperationException("Legacy ranged target combat setup is incomplete.");
            }

            var changes = new List<string>();
            for (var index = 0; index < dummies.Length; index++)
                changes.Add($"Use authored dummy {dummies[index].name} as ranged target {index + 1:00}");
            changes.Add("Remove hidden duplicate RangedTarget_01~03 actors");
            if (dryRun) return changes;

            var targetProperty = flowSerialized.FindProperty("rangedTargets");
            var rendererProperty = flowSerialized.FindProperty("rangedTargetRenderers");
            targetProperty.arraySize = dummies.Length;
            rendererProperty.arraySize = dummies.Length;

            for (var index = 0; index < dummies.Length; index++)
            {
                var dummy = dummies[index].gameObject;
                var collider = dummy.GetComponent<CircleCollider2D>();
                if (collider == null) collider = Undo.AddComponent<CircleCollider2D>(dummy);
                collider.isTrigger = true;
                collider.radius = Mathf.Max(0.16f, Mathf.Min(dummies[index].bounds.extents.x, dummies[index].bounds.extents.y) * 0.78f);

                var actor = dummy.GetComponent<CombatActorHost>();
                if (actor == null) actor = Undo.AddComponent<CombatActorHost>(dummy);
                CopyActorSetup(legacyActors[index], actor, $"TRAINING-RANGED-{index + 1:00}");

                targetProperty.GetArrayElementAtIndex(index).objectReferenceValue = dummy;
                rendererProperty.GetArrayElementAtIndex(index).objectReferenceValue = dummies[index];

                var marker = Find(scene, $"훈련_원거리_{index + 1:00}");
                if (marker != null) marker.transform.position = dummy.transform.position;
            }
            flowSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);

            foreach (var legacyActor in legacyActors)
                if (legacyActor != null) Undo.DestroyObjectImmediate(legacyActor.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        private static void CopyActorSetup(CombatActorHost source, CombatActorHost destination, string actorId)
        {
            var sourceSerialized = new SerializedObject(source);
            var destinationSerialized = new SerializedObject(destination);
            foreach (var propertyName in new[]
                     { "combatSystemHost", "kind", "maxHealth", "stageId", "unlockTreeId", "hitRecoverySeconds" })
            {
                var sourceProperty = sourceSerialized.FindProperty(propertyName);
                var destinationProperty = destinationSerialized.FindProperty(propertyName);
                if (sourceProperty == null || destinationProperty == null) continue;
                destinationSerialized.CopyFromSerializedProperty(sourceProperty);
            }
            destinationSerialized.FindProperty("actorId").stringValue = actorId;
            destinationSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(destination);
        }

        private static GameObject Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == name)?.gameObject;
    }
}
