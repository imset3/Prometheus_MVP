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
    public static class PrometheusTutorialLavaArtAutomation
    {
        private const string VisualName = "LavaAnimatedVisual_ART";
        private const string FrameRoot = "Assets/_Project/Art/AIConcept/Tutorial/Lava/TUTO_G_LavaLoop_";

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            var hazards = Resources.FindObjectsOfTypeAll<TutorialLavaHazardHost>()
                .Where(item => item != null && item.gameObject.scene == scene)
                .ToArray();
            if (hazards.Length == 0)
                throw new InvalidOperationException("TutorialScene has no G-stage lava hazard.");

            var frames = Enumerable.Range(0, 4)
                .Select(index => AssetDatabase.LoadAssetAtPath<Sprite>(FrameRoot + index.ToString("000") + ".png"))
                .ToArray();
            if (frames.Any(frame => frame == null))
                throw new InvalidOperationException("The four lava animation sprites are not imported as Sprite assets.");

            var changes = new List<PrometheusAiChange>();
            foreach (var hazard in hazards)
            {
                var collider = hazard.GetComponent<BoxCollider2D>();
                if (collider == null) continue;
                changes.Add(new PrometheusAiChange
                {
                    action = "apply-animated-lava-art",
                    hierarchyPath = GetPath(hazard.transform) + "/" + VisualName,
                    before = hazard.transform.Find(VisualName) == null ? "missing" : "existing lava visual",
                    after = "4-frame emissive lava loop fitted to hazard bounds"
                });
                if (dryRun) continue;

                var visual = hazard.transform.Find(VisualName);
                if (visual == null)
                {
                    var go = new GameObject(VisualName, typeof(SpriteRenderer), typeof(TutorialLavaAnimatedVisualHost));
                    Undo.RegisterCreatedObjectUndo(go, "Create animated lava visual");
                    visual = go.transform;
                    visual.SetParent(hazard.transform, false);
                }

                var renderer = visual.GetComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sortingOrder = 145;
                renderer.color = Color.white;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = new Vector2(collider.size.x, Mathf.Max(1.15f, collider.size.y + 0.75f));
                visual.localPosition = new Vector3(collider.offset.x, collider.offset.y + 0.34f, -0.01f);
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;

                var host = visual.GetComponent<TutorialLavaAnimatedVisualHost>();
                var serialized = new SerializedObject(host);
                serialized.FindProperty("targetRenderer").objectReferenceValue = renderer;
                var frameProperty = serialized.FindProperty("frames");
                frameProperty.arraySize = frames.Length;
                for (var index = 0; index < frames.Length; index++)
                    frameProperty.GetArrayElementAtIndex(index).objectReferenceValue = frames[index];
                serialized.FindProperty("framesPerSecond").floatValue = 5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(host);
                EditorUtility.SetDirty(renderer);
            }

            if (!dryRun)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return changes;
        }

        private static string GetPath(Transform target)
        {
            var path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }
    }
}
