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
    public static class PrometheusExteriorArtAutomation
    {
        private const string ReviewScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string TileRoot = "Assets/_Project/Art/AIConcepts/TutorialTileSets/ReviewBatch_v1/E_Exterior";
        private const string NaturalPlatformPath = TileRoot + "/Generated/TUTO_E_Natural_Platform_Middle_v2.png";

        private static readonly string[] TileRoles =
        {
            "Platform_Isolated", "Platform_Left", "Platform_Middle", "Platform_Right",
            "Block_TopLeft", "Block_Top", "Block_TopRight", "Block_Fill",
            "Wall_Left", "Block_FillAlt", "Wall_Right", "Support_Pillar"
        };

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (scene.path != ReviewScenePath)
                throw new InvalidOperationException("Exterior art is restricted to " + ReviewScenePath + "; active=" + scene.path);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying exterior art.");

            ValidateAssets();
            var exterior = RequireOne(scene, "외부");
            var renderers = exterior.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length != 7)
                throw new InvalidOperationException("Expected seven E exterior renderers, found " + renderers.Length);

            var changes = renderers.Select(renderer =>
                {
                    var before = renderer.sprite == null ? "no sprite" : AssetDatabase.GetAssetPath(renderer.sprite);
                    var after = ExpectedPath(renderer);
                    return new { renderer, before, after };
                })
                .Where(item => !string.Equals(item.before, item.after, StringComparison.Ordinal))
                .Select(item => new PrometheusAiChange
                {
                    action = "replace-exterior-platform-art",
                    objectId = PrometheusSceneQuery.ObjectId(item.renderer.gameObject),
                    hierarchyPath = PrometheusSceneQuery.Path(item.renderer.gameObject),
                    before = item.before,
                    after = item.after
                }).ToList();
            if (dryRun || changes.Count == 0) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply E exterior art");
            try
            {
                var sprites = BuildTileAssets();
                foreach (var renderer in renderers)
                {
                    Undo.RecordObject(renderer, "Apply E exterior tile sprite");
                    renderer.sprite = sprites[IsNaturalGround(renderer)
                        ? "Natural_Platform_Middle"
                        : IsVertical(renderer) ? "Support_Pillar" : "Platform_Middle"];
                    renderer.color = Color.white;
                    renderer.drawMode = SpriteDrawMode.Tiled;
                    renderer.tileMode = SpriteTileMode.Continuous;
                    EditorUtility.SetDirty(renderer);
                }

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

        private static bool IsVertical(SpriteRenderer renderer) => renderer.size.y > renderer.size.x;

        private static bool IsNaturalGround(SpriteRenderer renderer) => renderer.name == "외부바닥";

        private static string ExpectedPath(SpriteRenderer renderer) => IsNaturalGround(renderer)
            ? NaturalPlatformPath
            : TilePath(IsVertical(renderer) ? "Support_Pillar" : "Platform_Middle");

        private static Dictionary<string, Sprite> BuildTileAssets()
        {
            var output = TileRoot + "/Tiles";
            EnsureFolder(output);
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var role in TileRoles)
            {
                var path = TilePath(role);
                ConfigureImporter(path, 256f);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new InvalidOperationException("Exterior sprite import failed: " + path);
                sprites[role] = sprite;

                var tilePath = output + "/TUTO_E_" + role + "_v1.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                tile.name = "TUTO_E_" + role + "_v1";
                tile.sprite = sprite;
                tile.color = Color.white;
                tile.transform = Matrix4x4.identity;
                tile.colliderType = Tile.ColliderType.Sprite;
                tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
                EditorUtility.SetDirty(tile);
            }

            ConfigureImporter(NaturalPlatformPath, 1024f);
            var naturalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(NaturalPlatformPath);
            if (naturalSprite == null)
                throw new InvalidOperationException("Natural exterior sprite import failed: " + NaturalPlatformPath);
            sprites["Natural_Platform_Middle"] = naturalSprite;
            var naturalTilePath = output + "/TUTO_E_Natural_Platform_Middle_v2.asset";
            var naturalTile = AssetDatabase.LoadAssetAtPath<Tile>(naturalTilePath);
            if (naturalTile == null)
            {
                naturalTile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(naturalTile, naturalTilePath);
            }
            naturalTile.name = "TUTO_E_Natural_Platform_Middle_v2";
            naturalTile.sprite = naturalSprite;
            naturalTile.color = Color.white;
            naturalTile.transform = Matrix4x4.identity;
            naturalTile.colliderType = Tile.ColliderType.Sprite;
            naturalTile.flags = TileFlags.LockColor | TileFlags.LockTransform;
            EditorUtility.SetDirty(naturalTile);
            return sprites;
        }

        private static string TilePath(string role) =>
            TileRoot + "/Generated/TUTO_E_" + role + "_v1.png";

        private static void ConfigureImporter(string path, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static GameObject RequireOne(Scene scene, string name)
        {
            var matches = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene == scene && item.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Expected one " + name + ", found " + matches.Length);
            return matches[0];
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
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var role in TileRoles)
            {
                var path = TilePath(role);
                if (!File.Exists(Path.Combine(projectRoot, path)))
                    throw new FileNotFoundException("Missing exterior tile asset", path);
            }
            if (!File.Exists(Path.Combine(projectRoot, NaturalPlatformPath)))
                throw new FileNotFoundException("Missing natural exterior tile asset", NaturalPlatformPath);
        }
    }
}
