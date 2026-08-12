using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Narthex.Tools
{
    public static class PrometheusPlayerHealthBarAutomation
    {
        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            var changes = new List<PrometheusAiChange>();
            var presenter = Resources.FindObjectsOfTypeAll<CombatHealthTextPresenter>()
                .FirstOrDefault(item => item != null && item.gameObject.scene == scene && item.name == "PlayerHealthText");
            if (presenter == null) throw new InvalidOperationException("PlayerHealthText presenter was not found.");

            changes.Add(new PrometheusAiChange
            {
                action = "configure-player-health-bar",
                objectId = PrometheusSceneQuery.ObjectId(presenter.gameObject),
                hierarchyPath = PrometheusSceneQuery.Path(presenter.gameObject),
                before = "numeric text only",
                after = "themed fill bar + current / maximum"
            });
            if (dryRun) return changes;

            var root = presenter.GetComponent<RectTransform>();
            var sourceText = presenter.GetComponent<Text>();
            Undo.RecordObject(root, "Resize player health HUD");
            root.sizeDelta = new Vector2(330f, 48f);
            sourceText.enabled = false;

            var track = EnsureUiObject("PlayerHealthBarTrack", root);
            Stretch(track);
            var trackImage = EnsureComponent<Image>(track.gameObject);
            trackImage.color = new Color(0.025f, 0.06f, 0.085f, 0.96f);
            trackImage.raycastTarget = false;

            var fill = EnsureUiObject("PlayerHealthBarFill", track);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(4f, 4f);
            fill.offsetMax = new Vector2(-4f, -4f);
            var fillImage = EnsureComponent<Image>(fill.gameObject);
            fillImage.color = new Color(0.18f, 0.9f, 0.82f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            fillImage.raycastTarget = false;

            var valueRect = EnsureUiObject("PlayerHealthValueText", track);
            Stretch(valueRect);
            var valueText = EnsureComponent<Text>(valueRect.gameObject);
            EditorUtility.CopySerialized(sourceText, valueText);
            valueText.enabled = true;
            valueText.text = "500 / 500";
            valueText.alignment = TextAnchor.MiddleCenter;
            valueText.fontSize = 24;
            valueText.raycastTarget = false;
            var outline = EnsureComponent<Outline>(valueRect.gameObject);
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("healthText").objectReferenceValue = valueText;
            serialized.FindProperty("healthFill").objectReferenceValue = fillImage;
            serialized.FindProperty("showLabel").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(presenter);
            EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        private static RectTransform EnsureUiObject(string name, Transform parent)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            Undo.RegisterCreatedObjectUndo(gameObject, "Create player health HUD");
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent<T>(out var component) ? component : Undo.AddComponent<T>(target);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
