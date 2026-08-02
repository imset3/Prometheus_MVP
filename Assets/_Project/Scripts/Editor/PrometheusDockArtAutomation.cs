using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Narthex.Tools
{
    public static class PrometheusDockArtAutomation
    {
        private const string ReviewScenePath = "Assets/Scenes/AIReview/TutorialScene_FPilot_Review.unity";
        private const string ArtRoot = "Assets/_Project/Art/AIConcepts/TutorialTileSets/ReviewBatch_v1/H_NadirDock";
        private const string PlatformPath = ArtRoot + "/Generated/TUTO_H_Dock_Platform_Middle_v1.png";
        private const string SupportPath = ArtRoot + "/Generated/TUTO_H_Dock_Support_Pillar_v1.png";
        private const string WinchPath = ArtRoot + "/Generated/TUTO_H_Dock_Mooring_Winch_v1.png";
        private const string CranePath = ArtRoot + "/Generated/TUTO_H_Dock_Docking_Crane_v1.png";
        private const string VisualRootName = "AI_DockArtRoot";

        private static readonly Dictionary<string, string> BoundarySpritePaths = new(StringComparer.Ordinal)
        {
            { "Square", PlatformPath },
            { "Square (3)", PlatformPath },
            { "Square (1)", SupportPath },
            { "Square (2)", SupportPath }
        };

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (scene.path != ReviewScenePath)
                throw new InvalidOperationException("Dock art is restricted to " + ReviewScenePath + "; active=" + scene.path);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying dock art.");

            ValidateAssets();
            var dock = RequireOne(scene, "선착장");
            var boundaries = BoundarySpritePaths.Keys.ToDictionary(name => name, name => RequireDirectChild(dock.transform, name));
            var changes = DescribeChanges(dock, boundaries);
            if (dryRun || changes.Count == 0) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply H Nadir dock art");
            try
            {
                var platform = ImportSprite(PlatformPath, 256f, new Vector2(0.5f, 0.5f));
                var support = ImportSprite(SupportPath, 256f, new Vector2(0.5f, 0.5f));
                var winch = ImportSprite(WinchPath, 256f, new Vector2(0.5f, 0f));
                var crane = ImportSprite(CranePath, 256f, new Vector2(0.5f, 0f));
                UpsertTileAssets(platform, support);

                ApplyHorizontalBoundary(boundaries["Square"], platform);
                ApplyHorizontalBoundary(boundaries["Square (3)"], platform);
                ApplyVerticalBoundary(boundaries["Square (1)"], support);
                ApplyVerticalBoundary(boundaries["Square (2)"], support);

                var visualRoot = UpsertChild(dock.transform, VisualRootName);
                UpsertProp(visualRoot, "MooringWinch_ART", winch, new Vector3(847f, -7.05f, 0f), 3.1f, 3);
                UpsertProp(visualRoot, "DockingCrane_ART", crane, new Vector3(876.5f, -7.05f, 0f), 7.2f, 2);

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

        private static List<PrometheusAiChange> DescribeChanges(
            GameObject dock, IReadOnlyDictionary<string, GameObject> boundaries)
        {
            var changes = new List<PrometheusAiChange>();
            foreach (var pair in BoundarySpritePaths)
            {
                var target = boundaries[pair.Key];
                var renderer = target.GetComponent<SpriteRenderer>();
                var current = renderer == null || renderer.sprite == null
                    ? "no sprite"
                    : AssetDatabase.GetAssetPath(renderer.sprite);
                if (current == pair.Value && BoundaryLayoutMatches(pair.Key, renderer, target.transform)) continue;
                changes.Add(Change("replace-dock-boundary-art", target, current, pair.Value));
            }

            var visualRoot = dock.transform.Find(VisualRootName);
            DescribeProp(changes, dock, visualRoot, "MooringWinch_ART", WinchPath,
                new Vector3(847f, -7.05f, 0f), 3.1f);
            DescribeProp(changes, dock, visualRoot, "DockingCrane_ART", CranePath,
                new Vector3(876.5f, -7.05f, 0f), 7.2f);
            return changes;
        }

        private static bool BoundaryLayoutMatches(string name, SpriteRenderer renderer, Transform transform)
        {
            if (renderer == null) return false;
            if (name == "Square" || name == "Square (3)")
            {
                var expected = name == "Square" ? new Vector2(60f, 1f) : new Vector2(30f, 1f);
                return renderer.drawMode == SpriteDrawMode.Tiled && Approximately(renderer.size, expected) &&
                       ApproximatelyAbs(transform.localScale, Vector3.one);
            }

            return renderer.drawMode == SpriteDrawMode.Simple && ApproximatelyAbs(transform.localScale,
                new Vector3(1f / Mathf.Max(0.01f, renderer.sprite.bounds.size.x),
                    6f / Mathf.Max(0.01f, renderer.sprite.bounds.size.y), 1f));
        }

        private static void DescribeProp(List<PrometheusAiChange> changes, GameObject dock, Transform visualRoot,
            string name, string spritePath, Vector3 worldPosition, float worldHeight)
        {
            var prop = visualRoot == null ? null : visualRoot.Find(name);
            var renderer = prop == null ? null : prop.GetComponent<SpriteRenderer>();
            var current = renderer == null || renderer.sprite == null ? "missing" : AssetDatabase.GetAssetPath(renderer.sprite);
            var matches = current == spritePath && Approximately(prop.position, worldPosition) &&
                          Mathf.Abs(renderer.bounds.size.y - worldHeight) <= 0.05f;
            if (!matches) changes.Add(Change("upsert-dock-prop", prop == null ? dock : prop.gameObject, current, spritePath));
        }

        private static PrometheusAiChange Change(string action, GameObject target, string before, string after) =>
            new()
            {
                action = action,
                objectId = PrometheusSceneQuery.ObjectId(target),
                hierarchyPath = PrometheusSceneQuery.Path(target),
                before = before,
                after = after
            };

        private static void ApplyHorizontalBoundary(GameObject target, Sprite sprite)
        {
            var renderer = RequireRenderer(target);
            var box = target.GetComponent<BoxCollider2D>();
            var worldSize = target.name == "Square" ? new Vector2(60f, 1f) : new Vector2(30f, 1f);
            var signs = ScaleSigns(target.transform.localScale);
            Undo.RecordObject(renderer, "Apply H dock platform sprite");
            Undo.RecordObject(target.transform, "Normalize H dock platform transform");
            if (box != null) Undo.RecordObject(box, "Preserve H dock platform collider");
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = worldSize;
            target.transform.localScale = signs;
            if (box != null)
            {
                box.size = worldSize;
                box.offset = Vector2.zero;
            }
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(target.transform);
            if (box != null) EditorUtility.SetDirty(box);
        }

        private static void ApplyVerticalBoundary(GameObject target, Sprite sprite)
        {
            var renderer = RequireRenderer(target);
            var signs = ScaleSigns(target.transform.localScale);
            Undo.RecordObject(renderer, "Apply H dock support sprite");
            Undo.RecordObject(target.transform, "Fit H dock support sprite");
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            target.transform.localScale = new Vector3(
                signs.x / Mathf.Max(0.01f, sprite.bounds.size.x),
                signs.y * 6f / Mathf.Max(0.01f, sprite.bounds.size.y),
                signs.z);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(target.transform);
        }

        private static Transform UpsertChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create H dock art root");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void UpsertProp(Transform parent, string name, Sprite sprite,
            Vector3 worldPosition, float worldHeight, int sortingOrder)
        {
            var prop = parent.Find(name) ?? UpsertChild(parent, name);
            var renderer = prop.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(prop.gameObject);
            Undo.RecordObject(renderer, "Configure H dock prop");
            Undo.RecordObject(prop, "Place H dock prop");
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingOrder = sortingOrder;
            prop.position = worldPosition;
            prop.rotation = Quaternion.identity;
            var scale = worldHeight / Mathf.Max(0.01f, sprite.bounds.size.y);
            var parentScale = parent.lossyScale;
            prop.localScale = new Vector3(
                scale / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)), 1f);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(prop);
        }

        private static Sprite ImportSprite(string path, float ppu, Vector2 pivot)
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
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Dock sprite import failed: " + path);
            return sprite;
        }

        private static void UpsertTileAssets(Sprite platform, Sprite support)
        {
            var output = ArtRoot + "/Tiles";
            EnsureFolder(output);
            UpsertTile(output + "/TUTO_H_Dock_Platform_Middle_v1.asset", "TUTO_H_Dock_Platform_Middle_v1", platform);
            UpsertTile(output + "/TUTO_H_Dock_Support_Pillar_v1.asset", "TUTO_H_Dock_Support_Pillar_v1", support);
        }

        private static void UpsertTile(string path, string name, Sprite sprite)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.name = name;
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            tile.colliderType = Tile.ColliderType.Sprite;
            tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
            EditorUtility.SetDirty(tile);
        }

        private static SpriteRenderer RequireRenderer(GameObject target) =>
            target.GetComponent<SpriteRenderer>() ??
            throw new InvalidOperationException("Missing SpriteRenderer: " + PrometheusSceneQuery.Path(target));

        private static GameObject RequireDirectChild(Transform parent, string name)
        {
            var child = parent.Cast<Transform>().SingleOrDefault(item => item.name == name);
            return child == null
                ? throw new InvalidOperationException("Missing direct child " + name + " under " + PrometheusSceneQuery.Path(parent.gameObject))
                : child.gameObject;
        }

        private static GameObject RequireOne(Scene scene, string name)
        {
            var matches = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene == scene && item.name == name).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected one " + name + ", found " + matches.Length);
            return matches[0];
        }

        private static Vector3 ScaleSigns(Vector3 scale) => new(
            Mathf.Approximately(scale.x, 0f) ? 1f : Mathf.Sign(scale.x),
            Mathf.Approximately(scale.y, 0f) ? 1f : Mathf.Sign(scale.y),
            Mathf.Approximately(scale.z, 0f) ? 1f : Mathf.Sign(scale.z));

        private static bool Approximately(Vector2 a, Vector2 b) => (a - b).sqrMagnitude <= 0.0001f;
        private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= 0.0025f;
        private static bool ApproximatelyAbs(Vector3 a, Vector3 b) =>
            Approximately(new Vector3(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z)),
                new Vector3(Mathf.Abs(b.x), Mathf.Abs(b.y), Mathf.Abs(b.z)));

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
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var path in new[] { PlatformPath, SupportPath, WinchPath, CranePath })
                if (!File.Exists(Path.Combine(projectRoot, path)))
                    throw new FileNotFoundException("Missing H dock art asset", path);
        }
    }
}
