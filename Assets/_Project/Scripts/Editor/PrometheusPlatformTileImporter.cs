using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Narthex.Tools
{
    public static class PrometheusPlatformTileImporter
    {
        private const string GeneratedFolder =
            "Assets/_Project/Art/PlatformTiles/AI/Generated";
        private const string TileAssetFolder =
            "Assets/_Project/Art/PlatformTiles/AI/Tiles";
        private const string PalettePath =
            "Assets/TileMap/PZ_PlatformTilesPalette.prefab";
        private const float PixelsPerUnit = 256f;

        private static readonly string[] TileNames =
        {
            "Platform_Isolated",
            "Platform_Left",
            "Platform_Middle",
            "Platform_Right",
            "Block_TopLeft",
            "Block_Top",
            "Block_TopRight",
            "Block_Fill",
            "Wall_Left",
            "Block_FillAlt",
            "Wall_Right",
            "Support_Pillar"
        };

        [MenuItem(PrometheusToolMenuPaths.Root + "Art/Build AI Platform Tile Palette")]
        public static void Build()
        {
            EnsureFolder(TileAssetFolder);
            var tiles = new List<Tile>(TileNames.Length);

            foreach (var tileName in TileNames)
            {
                var texturePath = $"{GeneratedFolder}/PZ_{tileName}_v1.png";
                ConfigureSpriteImporter(texturePath);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                if (sprite == null)
                    throw new InvalidOperationException($"Sprite import failed: {texturePath}");

                var tilePath = $"{TileAssetFolder}/PZ_{tileName}_v1.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }

                tile.name = $"PZ_{tileName}_v1";
                tile.sprite = sprite;
                tile.color = Color.white;
                tile.transform = Matrix4x4.identity;
                tile.colliderType = Tile.ColliderType.Sprite;
                tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
                EditorUtility.SetDirty(tile);
                tiles.Add(tile);
            }

            CreatePalettePrefab(tiles);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath);
            Debug.Log(
                $"[sragon000][AI Platform Tiles] Built {tiles.Count} tiles and palette: {PalettePath}");
        }

        private static void ConfigureSpriteImporter(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                              throw new InvalidOperationException("Unity project root is unavailable.");
            var absolutePath = Path.Combine(projectRoot, assetPath);
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException("Platform tile PNG is missing.", assetPath);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                throw new InvalidOperationException($"Texture importer is unavailable: {assetPath}");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void CreatePalettePrefab(IReadOnlyList<Tile> tiles)
        {
            var root = new GameObject("PZ_PlatformTilesPalette");
            try
            {
                root.AddComponent<Grid>();
                var tilemapObject = new GameObject(
                    "Tiles",
                    typeof(Tilemap),
                    typeof(TilemapRenderer));
                tilemapObject.transform.SetParent(root.transform, false);
                var tilemap = tilemapObject.GetComponent<Tilemap>();

                for (var index = 0; index < tiles.Count; index++)
                {
                    var column = index % 4;
                    var row = index / 4;
                    tilemap.SetTile(new Vector3Int(column, -row, 0), tiles[index]);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            var segments = folderPath.Split('/');
            var current = segments[0];
            foreach (var segment in segments.Skip(1))
            {
                var next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }
    }
}
