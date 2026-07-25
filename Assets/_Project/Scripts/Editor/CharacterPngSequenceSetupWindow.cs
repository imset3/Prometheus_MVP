using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Narthex.Tools
{
    public sealed class CharacterPngSequenceSetupWindow : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/_Project/Art/Generated/Characters";
        private const string GeneratedVisualName = "CharacterSprite_ART";
        private static readonly Regex TrailingNumber =
            new Regex(@"(?<number>\d+)(?=\.png$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] PromeRequired = { "Idle", "Run", "Attack01", "Attack02", "Attack03" };
        private static readonly string[] PromeOptional = { "Jump", "Fall", "Glide", "Dash", "Hit", "Death" };
        private static readonly string[] HelteRequired =
        {
            "Idle", "BasicLeftSlash", "BasicRightSlash", "BlinkVanish", "BlinkReappear",
            "DashApproach", "CrossSlash", "SwordFocus", "SwordVolley", "PhaseTransition", "Recover"
        };
        private static readonly string[] HelteOptional = { "BasicWindup", "Hit", "Death" };

        [SerializeField] private CharacterPngAnimationPreset preset = CharacterPngAnimationPreset.Prome;
        [SerializeField] private GameObject targetActor;
        [SerializeField] private DefaultAsset sequenceFolder;
        [SerializeField] private string outputFolder = DefaultOutputFolder;
        [SerializeField] private float framesPerSecond = 24f;
        [SerializeField] private float pixelsPerUnit = 100f;
        [SerializeField] private Vector2 pivot = new Vector2(0.5f, 0f);
        [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;
        [SerializeField] private TextureImporterCompression compression = TextureImporterCompression.Uncompressed;
        [SerializeField] private bool fitStableBodyCollider = true;
        [SerializeField] private float colliderWidthRatio = 0.55f;
        [SerializeField] private float colliderHeightRatio = 0.9f;
        [SerializeField] private bool saveSceneAfterApply = true;
        [SerializeField] private bool showAdvanced;

        private Vector2 scroll;
        private SequenceScanResult scanResult;
        private Texture2D previewTexture;

        [MenuItem("Narthex/Art/Character PNG Sequence Setup")]
        public static void Open()
        {
            GetWindow<CharacterPngSequenceSetupWindow>("PNG Sequence Setup");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Character PNG Sequence Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "캐릭터와 PNG 상위 폴더를 선택한 뒤 검사하고 적용하세요. 씬은 적용 버튼을 누를 때만 변경됩니다.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            preset = (CharacterPngAnimationPreset)EditorGUILayout.EnumPopup("Character Preset", preset);
            targetActor = (GameObject)EditorGUILayout.ObjectField("Target Actor", targetActor, typeof(GameObject), true);
            sequenceFolder = (DefaultAsset)EditorGUILayout.ObjectField("PNG Sequence Folder", sequenceFolder, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                scanResult = null;
                previewTexture = null;
                ApplyPresetDefaults();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("검사 및 미리보기", GUILayout.Height(30f)))
                    Scan();
                using (new EditorGUI.DisabledScope(sequenceFolder == null))
                {
                    if (GUILayout.Button("폴더 템플릿 생성", GUILayout.Height(30f)))
                        CreateFolderTemplate();
                }
            }

            DrawScanResult();

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "고급 설정", true);
            if (showAdvanced) DrawAdvancedSettings();

            using (new EditorGUI.DisabledScope(scanResult == null || scanResult.HasErrors || targetActor == null))
            {
                if (GUILayout.Button("계층에 생성 및 적용", GUILayout.Height(38f)))
                    BuildAndApply();
            }
        }

        private void DrawAdvancedSettings()
        {
            using (new EditorGUI.IndentLevelScope())
            {
                framesPerSecond = Mathf.Max(0.01f, EditorGUILayout.FloatField("Frames Per Second", framesPerSecond));
                pixelsPerUnit = Mathf.Max(0.01f, EditorGUILayout.FloatField("Pixels Per Unit", pixelsPerUnit));
                pivot = EditorGUILayout.Vector2Field("Pivot", pivot);
                filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", filterMode);
                compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", compression);
                outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
                fitStableBodyCollider = EditorGUILayout.Toggle("Fit Stable Body Collider", fitStableBodyCollider);
                using (new EditorGUI.DisabledScope(!fitStableBodyCollider))
                {
                    colliderWidthRatio = EditorGUILayout.Slider("Collider Width Ratio", colliderWidthRatio, 0.1f, 1f);
                    colliderHeightRatio = EditorGUILayout.Slider("Collider Height Ratio", colliderHeightRatio, 0.1f, 1f);
                }
                saveSceneAfterApply = EditorGUILayout.Toggle("Save Scene After Apply", saveSceneAfterApply);
            }
        }

        private void DrawScanResult()
        {
            if (scanResult == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("검사 결과", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(280f));
            foreach (var motion in scanResult.Motions)
            {
                var icon = motion.Errors.Count > 0 ? "✕" : motion.Warnings.Count > 0 ? "!" : "✓";
                EditorGUILayout.LabelField($"{icon} {motion.Name}: {motion.Frames.Count}장");
                foreach (var error in motion.Errors)
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                foreach (var warning in motion.Warnings)
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }
            foreach (var error in scanResult.Errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (var warning in scanResult.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            EditorGUILayout.EndScrollView();

            previewTexture = scanResult.Motions.SelectMany(motion => motion.Frames)
                .Select(frame => AssetDatabase.LoadAssetAtPath<Texture2D>(frame.AssetPath))
                .FirstOrDefault(texture => texture != null);
            if (previewTexture != null)
            {
                var rect = GUILayoutUtility.GetAspectRect(
                    Mathf.Clamp((float)previewTexture.width / Mathf.Max(1, previewTexture.height), 0.5f, 2f),
                    GUILayout.MaxHeight(180f));
                EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);
            }
        }

        private void ApplyPresetDefaults()
        {
            if (targetActor == null) return;
            if (targetActor.GetComponent<HelteBossPatternHost>() != null)
                preset = CharacterPngAnimationPreset.Helte;
            else if (targetActor.GetComponent<PlayerMotorHost>() != null)
                preset = CharacterPngAnimationPreset.Prome;
        }

        private void Scan()
        {
            try
            {
                scanResult = ScanSequenceFolder(sequenceFolder, preset);
                Repaint();
            }
            catch (Exception exception)
            {
                scanResult = new SequenceScanResult();
                scanResult.Errors.Add(exception.Message);
                Debug.LogException(exception);
            }
        }

        private void CreateFolderTemplate()
        {
            var rootPath = GetAssetFolderPath(sequenceFolder);
            if (string.IsNullOrWhiteSpace(rootPath)) return;

            var motionNames = preset switch
            {
                CharacterPngAnimationPreset.Prome => PromeRequired.Concat(PromeOptional),
                CharacterPngAnimationPreset.Helte => HelteRequired.Concat(HelteOptional),
                _ => new[] { "Idle" }
            };

            foreach (var motionName in motionNames.Distinct(StringComparer.OrdinalIgnoreCase))
                EnsureFolder($"{rootPath}/{motionName}");
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(sequenceFolder);
            ShowNotification(new GUIContent("폴더 템플릿을 생성했습니다."));
        }

        private void BuildAndApply()
        {
            try
            {
                ValidateBuildInput();
                var characterName = SanitizeName(targetActor.name);
                var characterOutput = $"{outputFolder.TrimEnd('/')}/{characterName}";
                var animationFolder = $"{characterOutput}/Animations";
                var controllerFolder = $"{characterOutput}/Controllers";
                EnsureFolder(animationFolder);
                EnsureFolder(controllerFolder);

                var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
                foreach (var motion in scanResult.Motions.Where(item => item.Errors.Count == 0 && item.Frames.Count > 0))
                {
                    var sprites = ImportFrames(motion);
                    clips[motion.Name] = CreateOrUpdateClip(
                        sprites,
                        $"{animationFolder}/{SanitizeName(motion.Name)}.anim",
                        motion.Name,
                        IsLoopingMotion(motion.Name));
                }

                var controller = CreateOrUpdateController(
                    $"{controllerFolder}/{characterName}.controller",
                    clips);
                ApplyToHierarchy(controller, clips);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ShowNotification(new GUIContent("PNG 시퀀스를 계층에 적용했습니다."));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Character PNG Sequence Setup", exception.Message, "확인");
            }
        }

        private void ValidateBuildInput()
        {
            if (targetActor == null) throw new InvalidOperationException("대상 캐릭터를 선택하세요.");
            if (EditorUtility.IsPersistent(targetActor))
                throw new InvalidOperationException("씬 계층에 있는 캐릭터 오브젝트를 선택하세요.");
            if (scanResult == null) throw new InvalidOperationException("먼저 PNG 폴더를 검사하세요.");
            if (scanResult.HasErrors) throw new InvalidOperationException("검사 오류를 해결한 뒤 적용하세요.");
            if (framesPerSecond <= 0f) throw new InvalidOperationException("FPS는 0보다 커야 합니다.");
            if (pixelsPerUnit <= 0f) throw new InvalidOperationException("Pixels Per Unit은 0보다 커야 합니다.");
            if (string.IsNullOrWhiteSpace(outputFolder) ||
                (outputFolder != "Assets" && !outputFolder.StartsWith("Assets/", StringComparison.Ordinal)))
                throw new InvalidOperationException("출력 폴더는 Assets 내부여야 합니다.");
        }

        private List<Sprite> ImportFrames(MotionSequence motion)
        {
            var sprites = new List<Sprite>(motion.Frames.Count);
            foreach (var frame in motion.Frames)
            {
                var importer = AssetImporter.GetAtPath(frame.AssetPath) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException($"PNG Importer를 찾을 수 없습니다: {frame.AssetPath}");

                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                var changed = importer.textureType != TextureImporterType.Sprite ||
                              importer.spriteImportMode != SpriteImportMode.Single ||
                              !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                              importer.filterMode != filterMode ||
                              importer.textureCompression != compression ||
                              textureSettings.spriteAlignment != (int)SpriteAlignment.Custom ||
                              textureSettings.spritePivot != pivot ||
                              importer.mipmapEnabled;
                if (changed)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = pixelsPerUnit;
                    textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
                    textureSettings.spritePivot = pivot;
                    importer.SetTextureSettings(textureSettings);
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.filterMode = filterMode;
                    importer.textureCompression = compression;
                    importer.SaveAndReimport();
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(frame.AssetPath);
                if (sprite == null)
                    throw new InvalidOperationException($"Sprite로 가져오지 못했습니다: {frame.AssetPath}");
                sprites.Add(sprite);
            }
            return sprites;
        }

        private AnimationClip CreateOrUpdateClip(
            IReadOnlyList<Sprite> sprites,
            string clipPath,
            string clipName,
            bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = clipName };
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
            return clip;
        }

        private static AnimatorController CreateOrUpdateController(
            string controllerPath,
            IReadOnlyDictionary<string, AnimationClip> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            var stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = null;
            foreach (var pair in clips)
            {
                var state = stateMachine.states
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => candidate.name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
                if (state == null) state = stateMachine.AddState(pair.Key);
                state.motion = pair.Value;
                if (pair.Key.Equals("Idle", StringComparison.OrdinalIgnoreCase)) idleState = state;
            }

            if (idleState != null) stateMachine.defaultState = idleState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private void ApplyToHierarchy(
            RuntimeAnimatorController controller,
            IReadOnlyDictionary<string, AnimationClip> clips)
        {
            Undo.RegisterFullObjectHierarchyUndo(targetActor, "Apply Character PNG Sequence");

            var visualBind = FindDescendant(targetActor.transform, "Visual_ART_BIND") ?? targetActor.transform;
            var generatedVisual = FindDirectChild(visualBind, GeneratedVisualName);
            if (generatedVisual == null)
            {
                var generatedObject = new GameObject(GeneratedVisualName);
                Undo.RegisterCreatedObjectUndo(generatedObject, "Create Character Sprite Visual");
                generatedObject.transform.SetParent(visualBind, false);
                generatedVisual = generatedObject.transform;
            }

            var spriteRenderer = generatedVisual.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = Undo.AddComponent<SpriteRenderer>(generatedVisual.gameObject);
            var animator = generatedVisual.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(generatedVisual.gameObject);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            if (clips.TryGetValue("Idle", out var idleClip))
            {
                var idleSprite = GetFirstSprite(idleClip);
                if (idleSprite != null) spriteRenderer.sprite = idleSprite;
            }

            foreach (var renderer in visualBind.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == spriteRenderer) continue;
                Undo.RecordObject(renderer, "Disable Placeholder Renderer");
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }

            var oldMotion = targetActor.GetComponent<CombatVisualMotionHost>();
            if (oldMotion != null)
            {
                Undo.RecordObject(oldMotion, "Disable Placeholder Visual Motion");
                oldMotion.enabled = false;
                EditorUtility.SetDirty(oldMotion);
            }

            var bridge = targetActor.GetComponent<CharacterPngAnimationBridge>();
            if (bridge == null) bridge = Undo.AddComponent<CharacterPngAnimationBridge>(targetActor);
            var playerMotor = targetActor.GetComponent<PlayerMotorHost>();
            var playerInput = targetActor.GetComponent<PlayerInputHost>();
            var melee = targetActor.GetComponent<MeleeAttackHost>();
            var actor = targetActor.GetComponent<CombatActorHost>();
            var helte = targetActor.GetComponent<HelteBossPatternHost>();
            var body = targetActor.GetComponent<Rigidbody2D>();
            var facingTarget = preset == CharacterPngAnimationPreset.Helte
                ? GameObject.FindGameObjectWithTag("Player")?.transform
                : null;
            bridge.Configure(
                preset,
                animator,
                spriteRenderer,
                body,
                playerMotor,
                playerInput,
                melee,
                actor,
                helte,
                facingTarget,
                GetClipDuration(clips, "Attack01"),
                GetClipDuration(clips, "Attack02"),
                GetClipDuration(clips, "Attack03"));
            EditorUtility.SetDirty(bridge);

            UpdateArtReplacementContract(targetActor, visualBind, spriteRenderer);
            if (fitStableBodyCollider && spriteRenderer.sprite != null)
                FitStableCollider(targetActor, generatedVisual, spriteRenderer.sprite);

            EditorUtility.SetDirty(targetActor);
            EditorSceneManager.MarkSceneDirty(targetActor.scene);
            if (saveSceneAfterApply) EditorSceneManager.SaveScene(targetActor.scene);
            Selection.activeGameObject = generatedVisual.gameObject;
            EditorGUIUtility.PingObject(generatedVisual.gameObject);
        }

        private void FitStableCollider(GameObject actorObject, Transform visual, Sprite sprite)
        {
            var bounds = sprite.bounds;
            var scale = visual.localScale;
            var width = Mathf.Abs(bounds.size.x * scale.x) * colliderWidthRatio;
            var height = Mathf.Abs(bounds.size.y * scale.y) * colliderHeightRatio;
            var center = new Vector2(
                bounds.center.x * scale.x + visual.localPosition.x,
                bounds.min.y * scale.y + visual.localPosition.y + height * 0.5f);

            var capsule = actorObject.GetComponent<CapsuleCollider2D>();
            if (capsule != null)
            {
                Undo.RecordObject(capsule, "Fit Stable Character Collider");
                capsule.size = new Vector2(Mathf.Max(0.1f, width), Mathf.Max(0.2f, height));
                capsule.offset = center;
                EditorUtility.SetDirty(capsule);
                return;
            }

            var box = actorObject.GetComponent<BoxCollider2D>();
            if (box == null) return;
            Undo.RecordObject(box, "Fit Stable Character Collider");
            box.size = new Vector2(Mathf.Max(0.1f, width), Mathf.Max(0.2f, height));
            box.offset = center;
            EditorUtility.SetDirty(box);
        }

        private static void UpdateArtReplacementContract(
            GameObject actorObject,
            Transform visualBind,
            SpriteRenderer spriteRenderer)
        {
            var contract = actorObject.GetComponent<ArtReplacementContractHost>();
            if (contract == null) return;
            Undo.RecordObject(contract, "Update Art Replacement Contract");
            var serialized = new SerializedObject(contract);
            serialized.FindProperty("visualRoot").objectReferenceValue = visualBind;
            var renderers = serialized.FindProperty("renderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = spriteRenderer;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(contract);
        }

        private static SequenceScanResult ScanSequenceFolder(
            DefaultAsset folderAsset,
            CharacterPngAnimationPreset configuredPreset)
        {
            var result = new SequenceScanResult();
            var rootPath = GetAssetFolderPath(folderAsset);
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                result.Errors.Add("Assets 내부의 PNG 상위 폴더를 지정하세요.");
                return result;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            var absoluteRoot = Path.Combine(projectRoot ?? string.Empty, rootPath);
            if (!Directory.Exists(absoluteRoot))
            {
                result.Errors.Add($"폴더를 찾을 수 없습니다: {rootPath}");
                return result;
            }

            foreach (var absoluteMotionFolder in Directory.GetDirectories(absoluteRoot))
            {
                var motionName = Path.GetFileName(absoluteMotionFolder);
                var motionAssetPath = AbsoluteToAssetPath(absoluteMotionFolder);
                var motion = new MotionSequence(motionName);
                var pngPaths = AssetDatabase.FindAssets("t:Texture2D", new[] { motionAssetPath })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    .Where(path => string.Equals(
                        Path.GetDirectoryName(path)?.Replace('\\', '/'),
                        motionAssetPath,
                        StringComparison.Ordinal))
                    .ToList();

                var numberedFrames = new List<SequenceFrame>();
                foreach (var pngPath in pngPaths)
                {
                    var match = TrailingNumber.Match(pngPath);
                    if (!match.Success)
                    {
                        motion.Errors.Add($"{Path.GetFileName(pngPath)}: 파일명 끝에 000 형식의 번호가 필요합니다.");
                        continue;
                    }
                    numberedFrames.Add(new SequenceFrame(
                        int.Parse(match.Groups["number"].Value),
                        pngPath));
                }

                foreach (var frame in numberedFrames.OrderBy(frame => frame.Index).ThenBy(frame => frame.AssetPath))
                    motion.Frames.Add(frame);
                ValidateMotion(motion);
                result.Motions.Add(motion);
            }

            result.Motions.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            ValidateRequiredMotions(result, configuredPreset);
            if (result.Motions.Count == 0)
                result.Errors.Add("하위 모션 폴더와 PNG 파일을 찾지 못했습니다.");
            return result;
        }

        private static void ValidateMotion(MotionSequence motion)
        {
            if (motion.Frames.Count == 0)
            {
                motion.Warnings.Add("PNG 프레임이 없습니다.");
                return;
            }

            var duplicates = motion.Frames.GroupBy(frame => frame.Index).Where(group => group.Count() > 1).ToArray();
            foreach (var duplicate in duplicates)
                motion.Errors.Add($"{duplicate.Key:D3} 번호가 {duplicate.Count()}개 있습니다.");

            var expected = 0;
            foreach (var frame in motion.Frames.Select(item => item.Index).Distinct().OrderBy(index => index))
            {
                if (frame != expected)
                {
                    motion.Errors.Add($"{expected:D3}.png 프레임이 누락됐습니다.");
                    expected = frame;
                }
                expected++;
            }

            var firstTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(motion.Frames[0].AssetPath);
            if (firstTexture == null) return;
            foreach (var frame in motion.Frames.Skip(1))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(frame.AssetPath);
                if (texture == null) continue;
                if (texture.width == firstTexture.width && texture.height == firstTexture.height) continue;
                motion.Errors.Add(
                    $"{Path.GetFileName(frame.AssetPath)} 크기가 다릅니다. 기준 {firstTexture.width}x{firstTexture.height}, 현재 {texture.width}x{texture.height}");
            }
        }

        private static void ValidateRequiredMotions(
            SequenceScanResult result,
            CharacterPngAnimationPreset configuredPreset)
        {
            var required = configuredPreset switch
            {
                CharacterPngAnimationPreset.Prome => PromeRequired,
                CharacterPngAnimationPreset.Helte => HelteRequired,
                _ => new[] { "Idle" }
            };
            var available = new HashSet<string>(
                result.Motions.Where(motion => motion.Frames.Count > 0).Select(motion => motion.Name),
                StringComparer.OrdinalIgnoreCase);
            foreach (var requiredMotion in required)
                if (!available.Contains(requiredMotion))
                    result.Errors.Add($"필수 모션 폴더가 없습니다: {requiredMotion}");
        }

        private static string GetAssetFolderPath(DefaultAsset folderAsset)
        {
            if (folderAsset == null) return string.Empty;
            var path = AssetDatabase.GetAssetPath(folderAsset);
            return AssetDatabase.IsValidFolder(path) ? path : string.Empty;
        }

        private static string AbsoluteToAssetPath(string absolutePath)
        {
            var normalized = absolutePath.Replace('\\', '/');
            var normalizedDataPath = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(normalizedDataPath, StringComparison.Ordinal))
                throw new InvalidOperationException("폴더는 프로젝트 Assets 내부에 있어야 합니다.");
            return "Assets" + normalized.Substring(normalizedDataPath.Length);
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root.name == targetName) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var result = FindDescendant(root.GetChild(index), targetName);
                if (result != null) return result;
            }
            return null;
        }

        private static Transform FindDirectChild(Transform parent, string targetName)
        {
            for (var index = 0; index < parent.childCount; index++)
                if (parent.GetChild(index).name == targetName) return parent.GetChild(index);
            return null;
        }

        private static Sprite GetFirstSprite(AnimationClip clip)
        {
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).FirstOrDefault();
            if (binding.Equals(default(EditorCurveBinding))) return null;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            return keys != null && keys.Length > 0 ? keys[0].value as Sprite : null;
        }

        private static float GetClipDuration(
            IReadOnlyDictionary<string, AnimationClip> clips,
            string motionName)
        {
            return clips.TryGetValue(motionName, out var clip) && clip != null
                ? Mathf.Max(0.01f, clip.length)
                : 0.22f;
        }

        private static bool IsLoopingMotion(string motionName)
        {
            return motionName.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
                   motionName.Equals("Run", StringComparison.OrdinalIgnoreCase) ||
                   motionName.Equals("Glide", StringComparison.OrdinalIgnoreCase) ||
                   motionName.Equals("SwordFocus", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureFolder(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (projectRoot == null) throw new InvalidOperationException("프로젝트 루트를 찾을 수 없습니다.");
            Directory.CreateDirectory(Path.Combine(projectRoot, assetPath));
        }

        private static string SanitizeName(string value)
        {
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidCharacter.ToString(), string.Empty);
            return string.IsNullOrWhiteSpace(value) ? "Character" : value;
        }

        private sealed class SequenceScanResult
        {
            public readonly List<MotionSequence> Motions = new List<MotionSequence>();
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public bool HasErrors => Errors.Count > 0 || Motions.Any(motion => motion.Errors.Count > 0);
        }

        private sealed class MotionSequence
        {
            public MotionSequence(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public List<SequenceFrame> Frames { get; } = new List<SequenceFrame>();
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
        }

        private readonly struct SequenceFrame
        {
            public SequenceFrame(int index, string assetPath)
            {
                Index = index;
                AssetPath = assetPath;
            }

            public int Index { get; }
            public string AssetPath { get; }
        }
    }
}
