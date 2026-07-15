using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Narthex.Tools
{
    public sealed class SpriteSheetAnimationBuilderWindow : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/_Project/Art/Generated";
        private static readonly Regex GridSuffix = new Regex(@"(?:^|[_-])(?<columns>\d+)[xX](?<rows>\d+)(?:$|[_-])", RegexOptions.Compiled);

        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private int columns = 4;
        [SerializeField] private int rows = 4;
        [SerializeField] private float framesPerSecond = 12f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool removeEdgeConnectedBackground = true;
        [SerializeField, Range(0f, 1f)] private float backgroundTolerance = 0.08f;
        [SerializeField] private string outputFolder = DefaultOutputFolder;
        [SerializeField] private string clipName = string.Empty;

        [MenuItem("Narthex/Art/Sprite Sheet Animation Builder")]
        public static void Open()
        {
            GetWindow<SpriteSheetAnimationBuilderWindow>("Sprite Sheet Animator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sprite Sheet Animation Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Sheet", sourceTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
                ApplySourceDefaults();

            using (new EditorGUI.DisabledScope(sourceTexture == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    columns = EditorGUILayout.IntField("Columns", Mathf.Max(1, columns));
                    rows = EditorGUILayout.IntField("Rows", Mathf.Max(1, rows));
                }

                if (GUILayout.Button("Read Grid From File Name"))
                    TryApplyGridFromSourceName(true);

                framesPerSecond = EditorGUILayout.FloatField("Frames Per Second", Mathf.Max(0.01f, framesPerSecond));
                loop = EditorGUILayout.Toggle("Loop", loop);
                removeEdgeConnectedBackground = EditorGUILayout.Toggle("Remove Edge Background", removeEdgeConnectedBackground);
                using (new EditorGUI.DisabledScope(!removeEdgeConnectedBackground))
                    backgroundTolerance = EditorGUILayout.Slider("Background Tolerance", backgroundTolerance, 0f, 1f);

                outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
                clipName = EditorGUILayout.TextField("Clip Name", clipName);

                DrawSummary();
                EditorGUILayout.Space();
                if (GUILayout.Button("Build Frames And Animation Clip", GUILayout.Height(32f)))
                    Build();
            }

            if (sourceTexture == null)
                EditorGUILayout.HelpBox("Use a PNG sprite sheet. Include a suffix such as _4x4 or _6x10 to infer the grid automatically.", MessageType.Info);
            else if (removeEdgeConnectedBackground)
                EditorGUILayout.HelpBox("Only background connected to the frame edge and similar to the top-left pixel is removed. Use transparent source art for gradients or non-uniform backgrounds.", MessageType.None);
        }

        private void DrawSummary()
        {
            if (sourceTexture == null) return;

            var frameCount = columns * rows;
            var isDivisible = sourceTexture.width % columns == 0 && sourceTexture.height % rows == 0;
            var frameWidth = isDivisible ? sourceTexture.width / columns : 0;
            var frameHeight = isDivisible ? sourceTexture.height / rows : 0;
            var message = isDivisible
                ? string.Format("{0} frames, {1} x {2} pixels per frame", frameCount, frameWidth, frameHeight)
                : "Image width and height must divide exactly by the grid values.";
            EditorGUILayout.HelpBox(message, isDivisible ? MessageType.Info : MessageType.Error);
        }

        private void ApplySourceDefaults()
        {
            if (sourceTexture == null) return;
            TryApplyGridFromSourceName(false);
            if (string.IsNullOrWhiteSpace(clipName))
                clipName = StripGridSuffix(sourceTexture.name);
        }

        private void TryApplyGridFromSourceName(bool showFailure)
        {
            if (sourceTexture == null) return;

            var match = GridSuffix.Match(sourceTexture.name);
            if (!match.Success)
            {
                if (showFailure)
                    ShowNotification(new GUIContent("No _columnsxrows suffix found."));
                return;
            }

            columns = Mathf.Max(1, int.Parse(match.Groups["columns"].Value));
            rows = Mathf.Max(1, int.Parse(match.Groups["rows"].Value));
            if (string.IsNullOrWhiteSpace(clipName))
                clipName = StripGridSuffix(sourceTexture.name);
        }

        private void Build()
        {
            try
            {
                ValidateBuildInput();
                var sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
                var sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
                if (sourceImporter == null)
                    throw new InvalidOperationException("The selected source does not have a TextureImporter.");

                var wasReadable = sourceImporter.isReadable;
                if (!wasReadable)
                {
                    sourceImporter.isReadable = true;
                    sourceImporter.SaveAndReimport();
                }

                try
                {
                    var readableSource = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                    var sprites = WriteFrameSprites(readableSource);
                    CreateOrUpdateAnimationClip(sprites);
                }
                finally
                {
                    if (!wasReadable)
                    {
                        sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
                        sourceImporter.isReadable = false;
                        sourceImporter.SaveAndReimport();
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ShowNotification(new GUIContent("Frames and animation clip created."));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Sprite Sheet Animation Builder", exception.Message, "OK");
            }
        }

        private void ValidateBuildInput()
        {
            if (sourceTexture == null)
                throw new InvalidOperationException("Choose a source sprite sheet.");
            if (columns < 1 || rows < 1)
                throw new InvalidOperationException("Columns and rows must be at least 1.");
            if (framesPerSecond <= 0f)
                throw new InvalidOperationException("Frames per second must be greater than 0.");
            if (sourceTexture.width % columns != 0 || sourceTexture.height % rows != 0)
                throw new InvalidOperationException("Source dimensions must divide exactly by the configured grid.");
            if (string.IsNullOrWhiteSpace(outputFolder) || (outputFolder != "Assets" && !outputFolder.StartsWith("Assets/", StringComparison.Ordinal)))
                throw new InvalidOperationException("Output Folder must be inside the project's Assets folder.");
        }

        private List<Sprite> WriteFrameSprites(Texture2D readableSource)
        {
            var sourceBaseName = SanitizeName(StripGridSuffix(sourceTexture.name));
            var frameFolder = string.Format("{0}/{1}/Frames", outputFolder.TrimEnd('/'), sourceBaseName);
            EnsureFolder(frameFolder);

            var frameWidth = readableSource.width / columns;
            var frameHeight = readableSource.height / rows;
            var sprites = new List<Sprite>(columns * rows);
            var frameIndex = 0;

            for (var row = 0; row < rows; row++)
            {
                var sourceY = (rows - 1 - row) * frameHeight;
                for (var column = 0; column < columns; column++)
                {
                    var pixels = readableSource.GetPixels(column * frameWidth, sourceY, frameWidth, frameHeight);
                    if (removeEdgeConnectedBackground)
                        RemoveEdgeConnectedBackground(pixels, frameWidth, frameHeight, backgroundTolerance);

                    var frameTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
                    frameTexture.SetPixels(pixels);
                    frameTexture.Apply();
                    var framePath = string.Format("{0}/{1}_{2:D3}.png", frameFolder, sourceBaseName, frameIndex++);
                    File.WriteAllBytes(framePath, frameTexture.EncodeToPNG());
                    DestroyImmediate(frameTexture);
                    sprites.Add(null);
                }
            }

            AssetDatabase.Refresh();
            for (var index = 0; index < sprites.Count; index++)
            {
                var framePath = string.Format("{0}/{1}_{2:D3}.png", frameFolder, sourceBaseName, index);
                ConfigureFrameImporter(framePath);
                sprites[index] = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
                if (sprites[index] == null)
                    throw new InvalidOperationException("Could not import frame: " + framePath);
            }

            return sprites;
        }

        private void CreateOrUpdateAnimationClip(IReadOnlyList<Sprite> sprites)
        {
            var sourceBaseName = SanitizeName(StripGridSuffix(sourceTexture.name));
            var animationFolder = string.Format("{0}/{1}/Animations", outputFolder.TrimEnd('/'), sourceBaseName);
            EnsureFolder(animationFolder);

            var resolvedClipName = SanitizeName(string.IsNullOrWhiteSpace(clipName) ? sourceBaseName : clipName);
            var clipPath = string.Format("{0}/{1}.anim", animationFolder, resolvedClipName);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = framesPerSecond;
            var keyframes = new ObjectReferenceKeyframe[sprites.Count];
            for (var index = 0; index < sprites.Count; index++)
            {
                keyframes[index] = new ObjectReferenceKeyframe
                {
                    time = index / framesPerSecond,
                    value = sprites[index]
                };
            }

            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureFrameImporter(string framePath)
        {
            var importer = AssetImporter.GetAtPath(framePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Could not configure frame importer: " + framePath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void RemoveEdgeConnectedBackground(Color[] pixels, int width, int height, float tolerance)
        {
            if (pixels.Length == 0) return;

            var background = pixels[(height - 1) * width];
            if (background.a <= 0f) return;

            var visited = new bool[pixels.Length];
            var pending = new Queue<int>();
            for (var x = 0; x < width; x++)
            {
                EnqueueIfBackground(x, 0, width, height, pixels, background, tolerance, visited, pending);
                EnqueueIfBackground(x, height - 1, width, height, pixels, background, tolerance, visited, pending);
            }

            for (var y = 1; y < height - 1; y++)
            {
                EnqueueIfBackground(0, y, width, height, pixels, background, tolerance, visited, pending);
                EnqueueIfBackground(width - 1, y, width, height, pixels, background, tolerance, visited, pending);
            }

            while (pending.Count > 0)
            {
                var index = pending.Dequeue();
                pixels[index].a = 0f;
                var x = index % width;
                var y = index / width;
                EnqueueIfBackground(x - 1, y, width, height, pixels, background, tolerance, visited, pending);
                EnqueueIfBackground(x + 1, y, width, height, pixels, background, tolerance, visited, pending);
                EnqueueIfBackground(x, y - 1, width, height, pixels, background, tolerance, visited, pending);
                EnqueueIfBackground(x, y + 1, width, height, pixels, background, tolerance, visited, pending);
            }
        }

        private static void EnqueueIfBackground(int x, int y, int width, int height, Color[] pixels, Color background, float tolerance, bool[] visited, Queue<int> pending)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            var index = y * width + x;
            if (visited[index] || !IsSimilarToBackground(pixels[index], background, tolerance)) return;

            visited[index] = true;
            pending.Enqueue(index);
        }

        private static bool IsSimilarToBackground(Color pixel, Color background, float tolerance)
        {
            if (pixel.a <= 0f) return true;

            var red = pixel.r - background.r;
            var green = pixel.g - background.g;
            var blue = pixel.b - background.b;
            return red * red + green * green + blue * blue <= tolerance * tolerance * 3f;
        }

        private static void EnsureFolder(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var absolutePath = Path.Combine(projectRoot, assetPath);
            Directory.CreateDirectory(absolutePath);
        }

        private static string StripGridSuffix(string value)
        {
            return GridSuffix.Replace(value, "_").Trim('_', '-');
        }

        private static string SanitizeName(string value)
        {
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidCharacter.ToString(), string.Empty);
            return string.IsNullOrWhiteSpace(value) ? "SpriteSheet" : value;
        }
    }
}
