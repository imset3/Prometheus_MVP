using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusPromeArtAutomation
    {
        private const string FaceRoot = "Assets/_Project/Art/Motions/Prome/Face";

        private static readonly (string Field, string File)[] ExpressionFiles =
        {
            ("defaultClosed", "프로메 표정_기본.png"),
            ("defaultOpen", "프로메 표정_기본_입벌림.png"),
            ("sternClosed", "프로메 표정_정색.png"),
            ("sternVShape", "프로메 표정_정색_V자입.png"),
            ("sternOpen", "프로메 표정_정색_입벌림.png"),
            ("sighClosed", "프로메 표정_한숨.png"),
            ("sighOpen", "프로메 표정_한숨_입벌림.png")
        };

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            var changes = new List<PrometheusAiChange>
            {
                new PrometheusAiChange
                {
                    action = "build-animation",
                    hierarchyPath = "Assets/_Project/Art/Generated/Characters/PlayerVisual",
                    after = "Dash=30 frames@120fps; Jump=30 frames@30fps"
                }
            };

            var dialogueViews = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DialogueViewModule>(true))
                .ToArray();
            foreach (var view in dialogueViews)
                changes.Add(new PrometheusAiChange
                {
                    action = "assign-prome-expressions",
                    hierarchyPath = PrometheusSceneQuery.Path(view.gameObject),
                    after = "7 dialogue expression sprites"
                });

            if (dryRun) return changes;

            CharacterPngSequenceSetupWindow.BuildPromeDashAndJumpClips();
            var sprites = ImportExpressionSprites();
            foreach (var view in dialogueViews)
            {
                var serialized = new SerializedObject(view);
                var expressions = serialized.FindProperty("promeExpressions");
                foreach (var entry in ExpressionFiles)
                    expressions.FindPropertyRelative(entry.Field).objectReferenceValue = sprites[entry.Field];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
            }

            if (dialogueViews.Length > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            return changes;
        }

        private static Dictionary<string, Sprite> ImportExpressionSprites()
        {
            var result = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var entry in ExpressionFiles)
            {
                var path = $"{FaceRoot}/{entry.File}";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException($"표정 PNG를 찾을 수 없습니다: {path}");

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();

                result[entry.Field] = AssetDatabase.LoadAssetAtPath<Sprite>(path) ??
                                      throw new InvalidOperationException($"표정 Sprite 임포트 실패: {path}");
            }
            return result;
        }
    }
}
