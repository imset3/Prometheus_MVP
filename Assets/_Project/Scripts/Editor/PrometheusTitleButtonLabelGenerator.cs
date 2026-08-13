using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Narthex.Tools
{
    /// <summary>
    /// Renders the fixed Korean title-menu labels into transparent PNG sprites.
    /// Dynamic settings values remain regular UI text, while every button receives
    /// a deterministic art asset that can be reviewed and replaced independently.
    /// </summary>
    public static class PrometheusTitleButtonLabelGenerator
    {
        public const string OutputFolder = "Assets/_Project/Resources/UI/Title/Labels";
        private const string FontPath = "Assets/_Project/Art/Fonts/GoogleFonts/DoHyeon-Regular.ttf";
        private const int TextureWidth = 512;
        private const int TextureHeight = 96;
        private const int RenderLayer = 31;

        public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
        {
            ["TITLE_LABEL_NewGame_v1"] = "새 게임 시작",
            ["TITLE_LABEL_Continue_v1"] = "이어하기",
            ["TITLE_LABEL_Boss_v1"] = "보스전",
            ["TITLE_LABEL_Settings_v1"] = "설정",
            ["TITLE_LABEL_Quit_v1"] = "나가기",
            ["TITLE_LABEL_Apply_v1"] = "설정 적용",
            ["TITLE_LABEL_Back_v1"] = "돌아가기",
            ["TITLE_LABEL_Reset_v1"] = "초기화",
            ["TITLE_LABEL_ResetConfirm_v1"] = "한 번 더 눌러 초기화",
            ["PAUSE_LABEL_Resume_v1"] = "계속하기",
            ["PAUSE_LABEL_SaveAndExit_v1"] = "저장 및 나가기",
            ["PAUSE_LABEL_Apply_v1"] = "적용",
            ["PAUSE_LABEL_Cancel_v1"] = "취소"
        };

        [MenuItem(PrometheusToolMenuPaths.Ai + "Regenerate Title Button Label Sprites")]
        public static void GenerateAllMenu()
        {
            GenerateAll();
            Debug.Log($"[sragon000][Title] Regenerated {Labels.Count} title button label sprites.");
        }

        public static void GenerateAll()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) throw new InvalidOperationException($"Title label font is missing: {FontPath}");

            Directory.CreateDirectory(OutputFolder);
            foreach (var pair in Labels)
            {
                var path = $"{OutputFolder}/{pair.Key}.png";
                File.WriteAllBytes(path, RenderLabel(pair.Value, font));
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var pair in Labels)
            {
                var path = $"{OutputFolder}/{pair.Key}.png";
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
        }

        public static string GetAssetPath(string assetName) => $"{OutputFolder}/{assetName}.png";

        private static byte[] RenderLabel(string value, Font font)
        {
            GameObject cameraObject = null;
            GameObject canvasObject = null;
            RenderTexture renderTexture = null;
            Texture2D output = null;
            var previousActive = RenderTexture.active;
            try
            {
                cameraObject = new GameObject("TitleLabelRenderCamera", typeof(Camera));
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                cameraObject.layer = RenderLayer;
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.orthographicSize = TextureHeight * 0.5f;
                camera.aspect = TextureWidth / (float)TextureHeight;
                camera.cullingMask = 1 << RenderLayer;
                camera.transform.position = new Vector3(0f, 0f, -10f);

                renderTexture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 4,
                    filterMode = FilterMode.Bilinear
                };
                renderTexture.Create();
                camera.targetTexture = renderTexture;

                canvasObject = new GameObject("TitleLabelRenderCanvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.hideFlags = HideFlags.HideAndDontSave;
                canvasObject.layer = RenderLayer;
                var canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(TextureWidth, TextureHeight);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.sortingOrder = 32760;

                var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(Shadow));
                textObject.hideFlags = HideFlags.HideAndDontSave;
                textObject.layer = RenderLayer;
                textObject.transform.SetParent(canvasObject.transform, false);
                var textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(18f, 8f);
                textRect.offsetMax = new Vector2(-18f, -8f);
                var text = textObject.GetComponent<Text>();
                text.text = value;
                text.font = font;
                text.fontSize = 46;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(0.97f, 0.99f, 1f, 1f);
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 30;
                text.resizeTextMaxSize = 46;

                var outline = textObject.GetComponent<Outline>();
                outline.effectColor = new Color(0.005f, 0.02f, 0.035f, 0.96f);
                outline.effectDistance = new Vector2(2f, -2f);
                var shadow = textObject.GetComponent<Shadow>();
                shadow.effectColor = new Color(0.2f, 0.88f, 0.94f, 0.42f);
                shadow.effectDistance = new Vector2(0f, -1f);

                font.RequestCharactersInTexture(value, text.fontSize, text.fontStyle);
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture.active = renderTexture;
                output = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                output.ReadPixels(new Rect(0f, 0f, TextureWidth, TextureHeight), 0, 0, false);
                output.Apply(false, false);
                CenterOpaquePixels(output);
                return output.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (cameraObject != null)
                {
                    var camera = cameraObject.GetComponent<Camera>();
                    if (camera != null && camera.targetTexture == renderTexture) camera.targetTexture = null;
                }
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                if (output != null) UnityEngine.Object.DestroyImmediate(output);
                if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void CenterOpaquePixels(Texture2D texture)
        {
            var source = texture.GetPixels32();
            var minX = TextureWidth;
            var minY = TextureHeight;
            var maxX = -1;
            var maxY = -1;
            for (var y = 0; y < TextureHeight; y++)
            for (var x = 0; x < TextureWidth; x++)
            {
                if (source[y * TextureWidth + x].a <= 8) continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            if (maxX < minX || maxY < minY) return;
            var sourceCenterX = (minX + maxX) * 0.5f;
            var sourceCenterY = (minY + maxY) * 0.5f;
            var targetCenterX = (TextureWidth - 1) * 0.5f;
            var targetCenterY = (TextureHeight - 1) * 0.5f;
            var offsetX = Mathf.RoundToInt(targetCenterX - sourceCenterX);
            var offsetY = Mathf.RoundToInt(targetCenterY - sourceCenterY);
            if (offsetX == 0 && offsetY == 0) return;

            var centered = new Color32[source.Length];
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var targetX = x + offsetX;
                var targetY = y + offsetY;
                if (targetX < 0 || targetX >= TextureWidth || targetY < 0 || targetY >= TextureHeight) continue;
                centered[targetY * TextureWidth + targetX] = source[y * TextureWidth + x];
            }
            texture.SetPixels32(centered);
            texture.Apply(false, false);
        }
    }
}
