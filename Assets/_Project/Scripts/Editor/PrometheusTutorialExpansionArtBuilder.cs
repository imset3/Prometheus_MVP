using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Narthex.Tools
{
    public static class PrometheusTutorialExpansionArtBuilder
    {
        private const string ScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string TileBase = "Assets/_Project/Art/AIConcepts/TutorialTileSets/ReviewBatch_v1";
        private const string BackgroundBase = "Assets/_Project/Art/AIConcepts/TutorialBackgrounds";
        private const string EnemyOut = "Assets/_Project/Art/AIConcepts/TutorialEnemies/ReviewBatch_v1/TutorialGuard/UnityGenerated";
        private const string VisualName = "CharacterSprite_ART";

        private static readonly string[] Roles =
        {
            "Platform_Isolated", "Platform_Left", "Platform_Middle", "Platform_Right",
            "Block_TopLeft", "Block_Top", "Block_TopRight", "Block_Fill",
            "Wall_Left", "Block_FillAlt", "Wall_Right", "Support_Pillar"
        };

        private static readonly Area[] Areas =
        {
            new Area("A", "회의장", "AC_WarmBrassWalnut", "AC", "TUTO_A_AdamasMeeting_Backplate_v2.png"),
            new Area("B", "숨겨진방", "B_CoolTealHiddenLab", "B", "TUTO_B_HiddenRoom_Backplate_v2.png"),
            new Area("C", "복도", "AC_WarmBrassWalnut", "AC", "TUTO_C_Corridor_Backplate_v2.png"),
            new Area("E", "외부", "EH_AirDeck", "EH", "TUTO_E_Exterior_Backplate_v2.png"),
            new Area("G", "G스테이지", "G_CharcoalViolet", "G", "TUTO_G_Combat02_Backplate_v2.png")
        };

        private static readonly string[] GEnemyNames =
        {
            "ExteriorB_Enemy_01_ART_SLOT", "ExteriorB_Enemy_02_ART_SLOT",
            "ExteriorB_Enemy_03_ART_SLOT", "ExteriorB_Enemy_04_ART_SLOT"
        };

        [MenuItem("sragon000/AI Toolkit/Expansion Art/Dry Run")]
        public static void DryRun() { Run(true); }

        [MenuItem("sragon000/AI Toolkit/Expansion Art/Apply")]
        public static void Apply() { Run(false); }

        private static void Run(bool dryRun)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Expansion art is restricted to " + ScenePath + "; active=" + scene.path);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");

            var roots = Areas.ToDictionary(area => area.Key, area => FindOne(scene, area.RootName));
            var enemies = GEnemyNames.Select(name => FindOne(scene, name)).ToArray();
            ValidateAssets();

            var summary = string.Join(", ", Areas.Select(area =>
                area.Key + "=" + roots[area.Key].GetComponentsInChildren<SpriteRenderer>(true).Length));
            Debug.Log("[Prometheus Expansion] " + (dryRun ? "DRY RUN" : "APPLY") +
                      " scene=" + scene.path + "; renderers{" + summary + "}; GEnemySlots=" + enemies.Length +
                      "; backgrounds=A,B,C,E,G; uniqueTileSets=4");

            if (dryRun) return;

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Tutorial Expansion Art");
            try
            {
                var tileSets = new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.Ordinal);
                foreach (var area in Areas)
                {
                    var setKey = area.TileFolder + "|" + area.Prefix;
                    if (!tileSets.ContainsKey(setKey))
                        tileSets[setKey] = BuildTiles(area);
                    ApplyPlatforms(roots[area.Key].GetComponentsInChildren<SpriteRenderer>(true), tileSets[setKey]);
                    PrometheusBackgroundAutomation.Apply(
                        scene, area.Key, BackgroundBase + "/" + area.BackgroundFile,
                        0.85f, -1000, 20f, false);
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(EnemyOut + "/TutorialGuard.controller");
                var work = AssetDatabase.LoadAssetAtPath<AnimationClip>(EnemyOut + "/TutorialGuard_Work.anim");
                var attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(EnemyOut + "/TutorialGuard_Attack.anim");
                if (controller == null || work == null || attack == null)
                    throw new InvalidOperationException("Approved TutorialGuard UnityGenerated assets are missing.");
                foreach (var enemy in enemies)
                    ApplyEnemy(enemy, controller, work, attack);

                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                Debug.Log("[Prometheus Expansion] Applied A/B/C/E/G backgrounds and platform art plus " +
                          enemies.Length + " G enemy visual bindings. Scene left unsaved for MCP verification.");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private static void ValidateAssets()
        {
            foreach (var area in Areas)
            {
                RequireFile(BackgroundBase + "/" + area.BackgroundFile);
                foreach (var role in Roles)
                    RequireFile(TilePng(area, role));
            }
            RequireFile(EnemyOut + "/TutorialGuard.controller");
            RequireFile(EnemyOut + "/TutorialGuard_Work.anim");
            RequireFile(EnemyOut + "/TutorialGuard_Attack.anim");
            RequireFile(EnemyOut + "/TutorialGuard_Death.anim");
        }

        private static Dictionary<string, Sprite> BuildTiles(Area area)
        {
            var output = TileBase + "/" + area.TileFolder + "/Tiles";
            EnsureFolder(output);
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var role in Roles)
            {
                var png = TilePng(area, role);
                ConfigureImporter(png, 256f, new Vector2(0.5f, 0.5f));
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
                if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + png);
                sprites[role] = sprite;

                var tilePath = output + "/TUTO_" + area.Prefix + "_" + role + "_v1.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                tile.name = "TUTO_" + area.Prefix + "_" + role + "_v1";
                tile.sprite = sprite;
                tile.color = Color.white;
                tile.transform = Matrix4x4.identity;
                tile.colliderType = Tile.ColliderType.Sprite;
                tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
                EditorUtility.SetDirty(tile);
            }
            return sprites;
        }

        private static void ApplyPlatforms(
            IReadOnlyList<SpriteRenderer> renderers,
            IReadOnlyDictionary<string, Sprite> sprites)
        {
            var centerX = renderers.Count == 0 ? 0f : renderers.Average(item => item.transform.localPosition.x);
            foreach (var renderer in renderers)
            {
                Undo.RecordObject(renderer, "Apply area platform art");
                Undo.RecordObject(renderer.transform, "Normalize area platform scale");
                var scale = renderer.transform.localScale;
                var width = Mathf.Max(0.01f, Mathf.Abs(scale.x));
                var height = Mathf.Max(0.01f, Mathf.Abs(scale.y));
                var role = SelectRole(width, height, renderer.transform.localPosition.x, centerX);
                renderer.sprite = sprites[role];
                renderer.color = Color.white;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(width, height);
                renderer.transform.localScale = new Vector3(scale.x < 0f ? -1f : 1f, scale.y < 0f ? -1f : 1f, scale.z);
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(renderer.transform);
            }
        }

        private static string SelectRole(float width, float height, float x, float centerX)
        {
            if (width >= height * 1.75f)
                return height <= 1.5f ? "Platform_Middle" : "Block_FillAlt";
            if (height >= width * 1.75f)
                return width <= 1.5f ? "Support_Pillar" : (x <= centerX ? "Wall_Left" : "Wall_Right");
            return height <= 1.5f ? "Block_Top" : "Block_Fill";
        }

        private static void ApplyEnemy(
            GameObject actorObject,
            RuntimeAnimatorController controller,
            AnimationClip work,
            AnimationClip attack)
        {
            Undo.RegisterFullObjectHierarchyUndo(actorObject, "Apply G Tutorial Guard");
            var visualBind = FindDescendant(actorObject.transform, "Visual_ART_BIND") ?? actorObject.transform;
            var oldRenderers = visualBind.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.gameObject.name != VisualName).ToArray();
            var oldStates = oldRenderers.Select(renderer => renderer.enabled).ToArray();

            var visual = FindDirectChild(visualBind, VisualName);
            if (visual == null)
            {
                var go = new GameObject(VisualName);
                Undo.RegisterCreatedObjectUndo(go, "Create G Tutorial Guard sprite");
                go.transform.SetParent(visualBind, false);
                visual = go.transform;
            }

            var spriteRenderer = visual.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
            if (animator == null) throw new InvalidOperationException("Animator creation failed: " + actorObject.name);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            spriteRenderer.sprite = FirstSprite(work);
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 10;
            FitEnemy(actorObject, visual, spriteRenderer, oldRenderers.FirstOrDefault());

            foreach (var renderer in oldRenderers)
            {
                Undo.RecordObject(renderer, "Disable G enemy placeholder");
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }

            var motion = actorObject.GetComponent<CombatVisualMotionHost>();
            var bridge = actorObject.GetComponent<CharacterPngAnimationBridge>();
            if (bridge == null) bridge = Undo.AddComponent<CharacterPngAnimationBridge>(actorObject);
            if (!bridge.HasSetupBackup)
                bridge.CaptureSetupBackup(oldRenderers, oldStates, motion, motion == null || motion.enabled,
                    actorObject.GetComponent<Collider2D>(), visualBind, oldRenderers);

            var player = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(item => item.scene == actorObject.scene && item.CompareTag("Player"));
            bridge.Configure(
                CharacterPngAnimationPreset.Generic, animator, spriteRenderer,
                actorObject.GetComponent<Rigidbody2D>(), null, null, null,
                actorObject.GetComponent<EnemyAttackHost>(), actorObject.GetComponent<CombatActorHost>(),
                null, motion, true, player != null ? player.transform : null,
                attack.length, attack.length, attack.length);
            EditorUtility.SetDirty(bridge);

            if (motion != null)
            {
                Undo.RecordObject(motion, "Disable G procedural enemy motion");
                motion.enabled = false;
                EditorUtility.SetDirty(motion);
            }

            var contract = actorObject.GetComponent<ArtReplacementContractHost>();
            if (contract != null)
            {
                Undo.RecordObject(contract, "Bind G Tutorial Guard art");
                var serialized = new SerializedObject(contract);
                serialized.FindProperty("visualRoot").objectReferenceValue = visualBind;
                var renderers = serialized.FindProperty("renderers");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = spriteRenderer;
                serialized.ApplyModifiedProperties();
            }
        }

        private static void FitEnemy(
            GameObject actorObject,
            Transform visual,
            SpriteRenderer spriteRenderer,
            Renderer reference)
        {
            var box = actorObject.GetComponent<BoxCollider2D>();
            var height = box != null ? Mathf.Max(0.5f, box.size.y) :
                reference != null ? Mathf.Max(0.5f, reference.bounds.size.y) : 2f;
            var scale = height / Mathf.Max(0.01f, spriteRenderer.sprite.bounds.size.y);
            visual.localScale = new Vector3(scale, scale, 1f);
            visual.localPosition = box != null
                ? new Vector3(box.offset.x,
                    box.offset.y - box.size.y * 0.5f - spriteRenderer.sprite.bounds.min.y * scale, 0f)
                : Vector3.zero;
        }

        private static Sprite FirstSprite(AnimationClip clip)
        {
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).First();
            return AnimationUtility.GetObjectReferenceCurve(clip, binding)[0].value as Sprite;
        }

        private static void ConfigureImporter(string path, float ppu, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter: " + path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static GameObject FindOne(Scene scene, string objectName)
        {
            var matches = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene == scene && item.name == objectName).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Expected one " + objectName + ", found " + matches.Length);
            return matches[0];
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var found = FindDescendant(root.GetChild(index), name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            for (var index = 0; index < root.childCount; index++)
                if (root.GetChild(index).name == name) return root.GetChild(index);
            return null;
        }

        private static string TilePng(Area area, string role)
        {
            return TileBase + "/" + area.TileFolder + "/Generated/TUTO_" + area.Prefix + "_" + role + "_v1.png";
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            foreach (var part in parts.Skip(1))
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }

        private static void RequireFile(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            if (!File.Exists(Path.Combine(projectRoot, assetPath)))
                throw new FileNotFoundException("Missing expansion art asset", assetPath);
        }

        private sealed class Area
        {
            public Area(string key, string rootName, string tileFolder, string prefix, string backgroundFile)
            {
                Key = key;
                RootName = rootName;
                TileFolder = tileFolder;
                Prefix = prefix;
                BackgroundFile = backgroundFile;
            }

            public string Key { get; private set; }
            public string RootName { get; private set; }
            public string TileFolder { get; private set; }
            public string Prefix { get; private set; }
            public string BackgroundFile { get; private set; }
        }
    }
}
