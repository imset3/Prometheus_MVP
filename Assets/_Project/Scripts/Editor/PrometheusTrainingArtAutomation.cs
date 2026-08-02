using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using Narthex.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Narthex.Tools
{
    public static class PrometheusTrainingArtAutomation
    {
        private const string ReviewScenePath = "Assets/Scenes/AIReview/TutorialScene_FPilot_Review.unity";
        private const string BackgroundPath = "Assets/_Project/Art/AIConcepts/TutorialBackgrounds/TUTO_D_Training_Backplate_v2.png";
        private const string TileRoot = "Assets/_Project/Art/AIConcepts/TutorialTileSets/ReviewBatch_v1/D_Training";
        private const string PropRoot = "Assets/_Project/Art/AIConcepts/TutorialTrainingProps/Generated";
        private const string VisualSuffix = "_ART";

        private static readonly string[] TileRoles =
        {
            "Platform_Isolated", "Platform_Left", "Platform_Middle", "Platform_Right",
            "Block_TopLeft", "Block_Top", "Block_TopRight", "Block_Fill",
            "Wall_Left", "Block_FillAlt", "Wall_Right", "Support_Pillar"
        };

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (scene.path != ReviewScenePath)
                throw new InvalidOperationException("Training art is restricted to " + ReviewScenePath + "; active=" + scene.path);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying training art.");

            ValidateAssets();
            var room = RequireOne(scene, "훈련장 수정버전");
            var integration = RequireOne(scene, "D_Training_Integration");
            var spawn = RequireComponentInChildren<TutorialTrainingSpawnHost>(integration.transform);
            var flow = RequireComponentInChildren<TutorialImportedTrainingFlowHost>(integration.transform);
            var phases = RequireChild(spawn.transform, "TrainingPhaseContents");

            var changes = DescribeChanges(room, phases);
            changes.Insert(0, PrometheusBackgroundAutomation.Apply(
                scene, "D", BackgroundPath, 0.85f, -1000, 20f, true));
            if (dryRun) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply D training hall art");
            try
            {
                var tiles = BuildTileAssets();
                ApplyRoomPlatforms(room, tiles);
                PrometheusBackgroundAutomation.Apply(scene, "D", BackgroundPath, 0.85f, -1000, 20f, false);

                var dashSprite = ImportProp("TUTO_D_DashDropper_v2.png", new Vector2(0.5f, 0.5f));
                var jumpPadSprite = ImportProp("TUTO_D_DoubleJumpPad_v2.png", new Vector2(0.5f, 0f));
                var launcherSprite = ImportProp("TUTO_D_JumpLauncher_v2.png", new Vector2(0.5f, 0f));
                var projectileSprite = ImportProp("TUTO_D_JumpProjectile_v2.png", new Vector2(0.5f, 0.5f));
                var dummySprite = ImportProp("TUTO_D_TrainingDummy_v2.png", new Vector2(0.5f, 0f));

                ApplyDashPhase(RequireChild(phases, "01_대시"), dashSprite);
                ApplyDoubleJumpPhase(RequireChild(phases, "02_더블점프"), jumpPadSprite);
                ApplyJumpPhase(RequireChild(phases, "03_점프"), launcherSprite, projectileSprite);
                ApplyMeleePhase(RequireChild(phases, "04_근접공격"), spawn, dummySprite);
                ApplyRangedPhase(RequireChild(phases, "05_원거리공격"), flow, dummySprite);

                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return changes;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static List<PrometheusAiChange> DescribeChanges(GameObject room, Transform phases)
        {
            return new List<PrometheusAiChange>
            {
                Change("apply-training-platform-art", room, "4 room boundary renderers -> D tiled sprites"),
                Change("apply-dash-device-art", RequireChild(phases, "01_대시").gameObject, "3 falling hazards -> dash dropper sprite"),
                Change("apply-double-jump-art", RequireChild(phases, "02_더블점프").gameObject, "3 blockout platforms -> guided jump pads"),
                Change("apply-jump-projectile-art", RequireChild(phases, "03_점프").gameObject, "launcher plus 5 projectile visuals"),
                Change("apply-melee-dummy-art", RequireChild(phases, "04_근접공격").gameObject, "tutorial enemy -> training dummy"),
                Change("apply-ranged-dummy-art", RequireChild(phases, "05_원거리공격").gameObject, "3 ranged targets -> training dummies")
            };
        }

        private static PrometheusAiChange Change(string action, GameObject target, string after)
        {
            return new PrometheusAiChange
            {
                action = action,
                objectId = PrometheusSceneQuery.ObjectId(target),
                hierarchyPath = PrometheusSceneQuery.Path(target),
                before = "existing blockout visual",
                after = after
            };
        }

        private static Dictionary<string, Sprite> BuildTileAssets()
        {
            var output = TileRoot + "/Tiles";
            EnsureFolder(output);
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var role in TileRoles)
            {
                var png = TileRoot + "/Generated/TUTO_D_" + role + "_v1.png";
                ConfigureImporter(png, 256f, new Vector2(0.5f, 0.5f), TextureWrapMode.Clamp);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
                if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + png);
                sprites[role] = sprite;

                var tilePath = output + "/TUTO_D_" + role + "_v1.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                tile.name = "TUTO_D_" + role + "_v1";
                tile.sprite = sprite;
                tile.color = Color.white;
                tile.transform = Matrix4x4.identity;
                tile.colliderType = Tile.ColliderType.Sprite;
                tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
                EditorUtility.SetDirty(tile);
            }
            return sprites;
        }

        private static void ApplyRoomPlatforms(GameObject room, IReadOnlyDictionary<string, Sprite> sprites)
        {
            var renderers = room.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length != 4)
                throw new InvalidOperationException("Expected four D room boundary renderers, found " + renderers.Length);

            foreach (var renderer in renderers)
            {
                Undo.RecordObject(renderer, "Apply D platform sprite");
                Undo.RecordObject(renderer.transform, "Normalize D platform transform");
                var scale = renderer.transform.localScale;
                var isSideWall = Mathf.Abs(renderer.transform.localPosition.x) > 10f;
                var width = isSideWall ? 1f : 35f;
                var height = isSideWall ? 15f : 1f;
                renderer.sprite = sprites[width >= height ? "Platform_Middle" : "Support_Pillar"];
                renderer.color = Color.white;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(width, height);
                renderer.transform.localScale = new Vector3(Mathf.Sign(scale.x), Mathf.Sign(scale.y), scale.z);
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(renderer.transform);
            }
        }

        private static void ApplyDashPhase(Transform phase, Sprite sprite)
        {
            var hazards = phase.GetComponentsInChildren<TutorialTrainingDashFireHost>(true);
            if (hazards.Length != 3) throw new InvalidOperationException("Expected three dash hazards, found " + hazards.Length);
            for (var index = 0; index < hazards.Length; index++)
            {
                var host = hazards[index];
                DisableRenderer(host.GetComponent<SpriteRenderer>());
                UpsertWorldVisual(phase, "DashDropperVisual_" + (index + 1).ToString("00") + VisualSuffix,
                    sprite, host.transform.position, new Vector2(1.65f, 8.5f), 12);
            }
        }

        private static void ApplyDoubleJumpPhase(Transform phase, Sprite sprite)
        {
            var placeholders = phase.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(item => !item.gameObject.name.EndsWith(VisualSuffix, StringComparison.Ordinal))
                .ToArray();
            if (placeholders.Length != 3)
                throw new InvalidOperationException("Expected three double-jump blockout sprites, found " + placeholders.Length);
            for (var index = 0; index < placeholders.Length; index++)
            {
                var placeholder = placeholders[index];
                var width = Mathf.Max(2.6f, placeholder.bounds.size.x);
                var top = placeholder.bounds.max.y;
                DisableRenderer(placeholder);
                UpsertWorldVisual(phase, "DoubleJumpPadVisual_" + (index + 1).ToString("00") + VisualSuffix,
                    sprite, new Vector3(placeholder.transform.position.x, top - 0.25f, 0f),
                    new Vector2(width, width * 0.72f), 11);
            }
        }

        private static void ApplyJumpPhase(Transform phase, Sprite launcher, Sprite projectile)
        {
            var controller = RequireComponentInChildren<TutorialJumpTrainingHost>(phase);
            var serialized = new SerializedObject(controller);
            var launchPoint = serialized.FindProperty("launchPoint").objectReferenceValue as Transform;
            var pool = serialized.FindProperty("projectilePool");
            if (launchPoint == null || pool == null || pool.arraySize == 0)
                throw new InvalidOperationException("Jump training launcher references are incomplete.");

            var launcherPosition = new Vector3(launchPoint.position.x + 0.65f, -4.5f, 0f);
            UpsertWorldVisual(phase, "JumpLauncherVisual" + VisualSuffix,
                launcher, launcherPosition, new Vector2(2.7f, 2.2f), 11);

            for (var index = 0; index < pool.arraySize; index++)
            {
                var projectileObject = pool.GetArrayElementAtIndex(index).objectReferenceValue as GameObject;
                if (projectileObject == null) continue;
                DisableRenderers(projectileObject.transform);
                UpsertWorldVisual(projectileObject.transform,
                    "JumpProjectileVisual" + VisualSuffix, projectile,
                    projectileObject.transform.position, new Vector2(1.35f, 0.62f), 13);
            }

            foreach (var placeholder in phase.GetComponentsInChildren<SpriteRenderer>(true)
                         .Where(item => item.gameObject.name == "Circle"))
                DisableRenderer(placeholder);
        }

        private static void ApplyMeleePhase(Transform phase, TutorialTrainingSpawnHost spawn, Sprite dummy)
        {
            foreach (var placeholder in phase.GetComponentsInChildren<SpriteRenderer>(true))
                DisableRenderer(placeholder);

            var serialized = new SerializedObject(spawn);
            var enemy = serialized.FindProperty("tutorialEnemy").objectReferenceValue as GameObject;
            if (enemy == null) throw new InvalidOperationException("Training melee enemy reference is missing.");
            BindActorDummy(enemy, dummy, 1.9f);
        }

        private static void ApplyRangedPhase(Transform phase, TutorialImportedTrainingFlowHost flow, Sprite dummy)
        {
            var serialized = new SerializedObject(flow);
            var targets = serialized.FindProperty("rangedTargets");
            var renderers = serialized.FindProperty("rangedTargetRenderers");
            if (targets.arraySize != 3 || renderers.arraySize != 3)
                throw new InvalidOperationException("Ranged training must have exactly three linked targets.");

            for (var index = 0; index < 3; index++)
            {
                var target = targets.GetArrayElementAtIndex(index).objectReferenceValue as GameObject;
                var renderer = renderers.GetArrayElementAtIndex(index).objectReferenceValue as SpriteRenderer;
                if (target == null || renderer == null)
                    throw new InvalidOperationException("Ranged training target link " + index + " is missing.");
                DisableRenderers(target.transform);
                Undo.RecordObject(renderer, "Apply ranged training dummy");
                renderer.sprite = dummy;
                renderer.color = Color.white;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.sortingOrder = 12;
                var height = 1.9f;
                var scale = height / Mathf.Max(0.01f, dummy.bounds.size.y);
                Undo.RecordObject(renderer.transform, "Fit ranged training dummy");
                renderer.transform.localScale = new Vector3(scale, scale, 1f);
                renderer.transform.position = new Vector3(renderer.transform.position.x, -4.5f, 0f);
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(renderer.transform);
            }
        }

        private static void BindActorDummy(GameObject actor, Sprite dummy, float height)
        {
            var visualBind = FindDescendant(actor.transform, "Visual_ART_BIND") ?? actor.transform;
            DisableRenderers(visualBind);
            var box = actor.GetComponent<BoxCollider2D>();
            var bottom = actor.transform.position.y + (box == null ? -0.8f : box.offset.y - box.size.y * 0.5f);
            var visual = UpsertWorldVisual(visualBind, "TrainingDummyVisual" + VisualSuffix, dummy,
                new Vector3(actor.transform.position.x, bottom, actor.transform.position.z),
                new Vector2(height * dummy.bounds.size.x / dummy.bounds.size.y, height), 12);

            var contract = actor.GetComponent<ArtReplacementContractHost>();
            if (contract == null) return;
            Undo.RecordObject(contract, "Bind training dummy art contract");
            var serialized = new SerializedObject(contract);
            serialized.FindProperty("visualRoot").objectReferenceValue = visualBind;
            var renderers = serialized.FindProperty("renderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = visual.GetComponent<SpriteRenderer>();
            serialized.ApplyModifiedProperties();
        }

        private static Transform UpsertWorldVisual(
            Transform parent, string name, Sprite sprite, Vector3 worldPosition, Vector2 worldSize, int sortingOrder)
        {
            var visual = parent.Find(name);
            if (visual == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create training art visual");
                go.transform.SetParent(parent, false);
                visual = go.transform;
            }

            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(visual.gameObject);
            Undo.RecordObject(renderer, "Configure training art visual");
            Undo.RecordObject(visual, "Place training art visual");
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingOrder = sortingOrder;
            visual.position = worldPosition;
            visual.rotation = Quaternion.identity;
            var desired = new Vector3(
                worldSize.x / Mathf.Max(0.01f, sprite.bounds.size.x),
                worldSize.y / Mathf.Max(0.01f, sprite.bounds.size.y), 1f);
            var parentScale = parent.lossyScale;
            visual.localScale = new Vector3(
                desired.x / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                desired.y / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)), 1f);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(visual);
            return visual;
        }

        private static Sprite ImportProp(string fileName, Vector2 pivot)
        {
            var path = PropRoot + "/" + fileName;
            ConfigureImporter(path, 256f, pivot, TextureWrapMode.Clamp);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Training prop import failed: " + path);
            return sprite;
        }

        private static void ConfigureImporter(string path, float ppu, Vector2 pivot, TextureWrapMode wrapMode)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = wrapMode;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void DisableRenderer(Renderer renderer)
        {
            if (renderer == null) return;
            Undo.RecordObject(renderer, "Disable training blockout renderer");
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }

        private static void DisableRenderers(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if (!renderer.gameObject.name.EndsWith(VisualSuffix, StringComparison.Ordinal))
                    DisableRenderer(renderer);
        }

        private static T RequireComponentInChildren<T>(Transform root) where T : Component
        {
            var result = root.GetComponentInChildren<T>(true);
            if (result == null) throw new InvalidOperationException("Missing " + typeof(T).Name + " under " + root.name);
            return result;
        }

        private static GameObject RequireOne(Scene scene, string name)
        {
            var matches = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene == scene && item.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Expected one " + name + ", found " + matches.Length);
            return matches[0];
        }

        private static Transform RequireChild(Transform root, string name)
        {
            var child = root.Find(name);
            if (child == null) throw new InvalidOperationException("Missing child " + name + " under " + root.name);
            return child;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root.name == name) return root;
            for (var index = 0; index < root.childCount; index++)
            {
                var result = FindDescendant(root.GetChild(index), name);
                if (result != null) return result;
            }
            return null;
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

        private static void ValidateAssets()
        {
            RequireFile(BackgroundPath);
            foreach (var role in TileRoles)
                RequireFile(TileRoot + "/Generated/TUTO_D_" + role + "_v1.png");
            foreach (var file in new[]
                     {
                         "TUTO_D_DashDropper_v2.png", "TUTO_D_DoubleJumpPad_v2.png",
                         "TUTO_D_JumpLauncher_v2.png", "TUTO_D_JumpProjectile_v2.png",
                         "TUTO_D_TrainingDummy_v2.png"
                     })
                RequireFile(PropRoot + "/" + file);
        }

        private static void RequireFile(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            if (!File.Exists(Path.Combine(projectRoot, assetPath)))
                throw new FileNotFoundException("Missing training art asset", assetPath);
        }
    }
}
