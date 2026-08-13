using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Narthex.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Narthex.Tools
{
    /// <summary>
    /// Re-authors the tutorial's presentation tilemaps without touching the authored
    /// gameplay colliders, markers or encounter objects.  Everything produced here is
    /// serialized into the scene so a level designer can inspect and move it.
    /// </summary>
    public static class PrometheusConnectedTilemapV2Automation
    {
        public const string RootName = "환경타일맵_v2";
        private const string LegacyRootName = "재구성_플랫폼타일맵";
        private const string GeneratedRoot = "Assets/_Project/Art/PlatformTiles/V2/Generated";
        private const int TextureSize = 256;
        private const float PixelsPerUnit = 256f;

        private sealed class ZoneDefinition
        {
            public string section;
            public string theme;
            public string sourceTilemap;
        }

        private sealed class ThemeDefinition
        {
            public string id;
            public Color baseColor;
            public Color shadow;
            public Color line;
            public Color trim;
            public Color accent;
        }

        private static readonly ZoneDefinition[] Zones =
        {
            new() { section = "회의장", theme = "Adamas", sourceTilemap = "회의장-타일맵" },
            new() { section = "숨겨진방", theme = "Hidden", sourceTilemap = "숨겨진방-타일맵" },
            new() { section = "복도", theme = "Corridor", sourceTilemap = null },
            new() { section = "훈련장 수정버전", theme = "Training", sourceTilemap = "훈련장-타일맵" },
            new() { section = "외부", theme = "Exterior", sourceTilemap = "외부-타일맵" },
            new() { section = "F스테이지", theme = "Exterior", sourceTilemap = "F스테이지-타일맵" },
            new() { section = "G스테이지", theme = "Thermal", sourceTilemap = "G스테이지-타일맵" },
            new() { section = "선착장", theme = "Nadir", sourceTilemap = "선착장-타일맵" }
        };

        private static readonly Dictionary<string, ThemeDefinition> Themes = new(StringComparer.Ordinal)
        {
            ["Adamas"] = Theme("Adamas", "C9B49A", "5C4B48", "897468", "C89448", "65D9D3"),
            ["Hidden"] = Theme("Hidden", "3D6765", "172E34", "274C4E", "A26C3B", "53C9C8"),
            ["Corridor"] = Theme("Corridor", "B8A68E", "4A4747", "75685D", "B87945", "64CBD0"),
            ["Training"] = Theme("Training", "B9B8B2", "454A50", "747A7E", "BA8246", "4FD4DA"),
            ["Exterior"] = Theme("Exterior", "ACA89D", "47494B", "717174", "AE824C", "71C9C9"),
            ["Thermal"] = Theme("Thermal", "4D4C49", "202124", "34383A", "806544", "E56E35"),
            ["Nadir"] = Theme("Nadir", "777B7B", "30383A", "525C5C", "9A774D", "71C6C3")
        };

        [MenuItem(PrometheusToolMenuPaths.Analysis + "Preview Connected Tutorial Tilemaps V2")]
        public static void PreviewMenu() => Apply(EditorSceneManager.GetActiveScene(), true);

        [MenuItem(PrometheusToolMenuPaths.Root + "Tilemap/Apply Connected Tutorial Tilemaps V2")]
        public static void ApplyMenu() => Apply(EditorSceneManager.GetActiveScene(), false);

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            var changes = new List<PrometheusAiChange>();
            foreach (var zone in Zones)
            {
                var section = FindExact(scene, zone.section);
                if (section == null)
                {
                    changes.Add(Change("tilemap-v2-missing-zone", zone.section, "missing", "section required"));
                    continue;
                }

                var legacy = section.transform.Find(LegacyRootName);
                var sources = legacy == null
                    ? BuildCorridorSources(section.transform)
                    : ReadLegacySources(legacy);
                var cells = sources.Sum(source => source.cells.Count);
                changes.Add(Change("author-connected-tilemap-v2", Path(section),
                    section.transform.Find(RootName) == null ? "missing" : "existing",
                    $"theme={zone.theme}; layers={sources.Count}; cells={cells}; hierarchy-authored"));
                if (dryRun || sources.Count == 0) continue;

                EnsureThemeTiles(Themes[zone.theme]);
                var tileAssets = LoadThemeTiles(zone.theme);
                ReplaceZoneVisuals(section, legacy, zone.sourceTilemap, sources, tileAssets);
            }

            if (!dryRun)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
            }
            return changes;
        }

        public static List<PrometheusAiChange> Validate(Scene scene)
        {
            var issues = new List<PrometheusAiChange>();
            foreach (var zone in Zones)
            {
                var section = FindExact(scene, zone.section);
                var root = section == null ? null : section.transform.Find(RootName);
                if (root == null)
                {
                    issues.Add(Change("tilemap-v2-missing-root", zone.section, "missing", RootName));
                    continue;
                }

                var tilemaps = root.GetComponentsInChildren<Tilemap>(true);
                if (tilemaps.Length == 0 || tilemaps.Sum(CountTiles) == 0)
                    issues.Add(Change("tilemap-v2-empty", Path(root.gameObject), "0 tiles", "at least one authored tile"));
                if (root.GetComponentInChildren<TilemapCollider2D>(true) != null)
                    issues.Add(Change("tilemap-v2-visual-collider", Path(root.gameObject), "TilemapCollider2D present", "visual-only; no collider"));
                if (root.Find("구조바디") == null || root.Find("보행면") == null ||
                    root.Find("벽과모서리") == null || root.Find("지지기둥") == null ||
                    root.Find("배관과장식") == null || root.Find("상태오버레이") == null)
                    issues.Add(Change("tilemap-v2-category-missing", Path(root.gameObject), "incomplete hierarchy", "six editable visual categories"));
            }

            foreach (var tile in AssetDatabase.FindAssets("t:Tile", new[] { GeneratedRoot })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Select(AssetDatabase.LoadAssetAtPath<Tile>))
            {
                if (tile == null || tile.sprite == null) continue;
                if (Mathf.Abs(tile.sprite.pixelsPerUnit - PixelsPerUnit) > 0.01f)
                    issues.Add(Change("tilemap-v2-ppu", AssetDatabase.GetAssetPath(tile),
                        tile.sprite.pixelsPerUnit.ToString("F1"), PixelsPerUnit.ToString("F0")));
            }
            return issues;
        }

        public static List<PrometheusAiChange> Rollback(Scene scene, bool dryRun)
        {
            var changes = new List<PrometheusAiChange>();
            foreach (var zone in Zones)
            {
                var section = FindExact(scene, zone.section);
                if (section == null) continue;
                var root = section.transform.Find(RootName);
                if (root != null)
                    changes.Add(Change("disable-tilemap-v2", Path(root.gameObject), "active", "inactive"));
                var legacy = section.transform.Find(LegacyRootName);
                if (legacy != null)
                    changes.Add(Change("restore-legacy-tilemap", Path(legacy.gameObject), "inactive", "active"));
                if (dryRun) continue;
                if (root != null) root.gameObject.SetActive(false);
                if (legacy != null) legacy.gameObject.SetActive(true);
            }
            if (!dryRun) EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        private sealed class TileLayerSource
        {
            public string name;
            public Vector3 localPosition;
            public HashSet<Vector3Int> cells;
        }

        private static List<TileLayerSource> ReadLegacySources(Transform legacy)
        {
            return legacy.GetComponentsInChildren<Tilemap>(true)
                .Select((tilemap, index) => new TileLayerSource
                {
                    name = $"연결레이어_{index + 1:00}",
                    localPosition = legacy.InverseTransformPoint(tilemap.transform.position),
                    cells = ReadCells(tilemap)
                })
                .Where(source => source.cells.Count > 0)
                .ToList();
        }

        private static List<TileLayerSource> BuildCorridorSources(Transform section)
        {
            var renderers = section.Cast<Transform>()
                .Where(child => child.name.Contains("바닥") || child.name.Contains("천장") || child.name.Contains("벽"))
                .Select(child => child.GetComponent<SpriteRenderer>())
                .Where(renderer => renderer != null)
                .ToArray();
            if (renderers.Length == 0) return new List<TileLayerSource>();

            var groups = renderers.GroupBy(renderer => new Vector2(
                Fraction(section.InverseTransformPoint(renderer.bounds.min).x),
                Fraction(section.InverseTransformPoint(renderer.bounds.min).y)));
            var result = new List<TileLayerSource>();
            var index = 0;
            foreach (var group in groups)
            {
                var offset = group.Key;
                var cells = new HashSet<Vector3Int>();
                foreach (var renderer in group)
                {
                    var bounds = LocalBounds(section, renderer.bounds);
                    var minX = Mathf.FloorToInt(bounds.min.x - offset.x + 0.001f);
                    var maxX = Mathf.CeilToInt(bounds.max.x - offset.x - 0.001f);
                    var minY = Mathf.FloorToInt(bounds.min.y - offset.y + 0.001f);
                    var maxY = Mathf.CeilToInt(bounds.max.y - offset.y - 0.001f);
                    for (var x = minX; x < maxX; x++)
                    for (var y = minY; y < maxY; y++)
                        cells.Add(new Vector3Int(x, y));
                }
                result.Add(new TileLayerSource
                {
                    name = $"연결레이어_{++index:00}",
                    localPosition = offset,
                    cells = cells
                });
            }
            return result;
        }

        private static void ReplaceZoneVisuals(GameObject section, Transform legacy, string sourceTilemapName,
            IReadOnlyList<TileLayerSource> sources, IReadOnlyDictionary<int, Tile> tiles)
        {
            var existing = section.transform.Find(RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var root = NewChild(section.transform, RootName);
            var body = NewChild(root.transform, "구조바디");
            NewChild(root.transform, "보행면");
            NewChild(root.transform, "벽과모서리");
            NewChild(root.transform, "지지기둥");
            NewChild(root.transform, "배관과장식");
            NewChild(root.transform, "상태오버레이");

            foreach (var source in sources)
            {
                var gridObject = NewChild(body.transform, source.name, typeof(Grid));
                gridObject.transform.localPosition = source.localPosition;
                var tilemapObject = NewChild(gridObject.transform, "연결형_플랫폼과벽");
                var map = Undo.AddComponent<Tilemap>(tilemapObject);
                var renderer = Undo.AddComponent<TilemapRenderer>(tilemapObject);
                renderer.sortingOrder = 0;
                foreach (var cell in source.cells)
                {
                    map.SetTile(cell, tiles[Mask(source.cells, cell)]);
                    var tintSeed = Mathf.Abs(cell.x * 73856093 ^ cell.y * 19349663);
                    var tint = 0.94f + (tintSeed % 11) * 0.006f;
                    map.SetColor(cell, new Color(tint, tint, tint, 1f));
                }
                map.CompressBounds();
            }

            if (legacy != null)
            {
                Undo.RecordObject(legacy.gameObject, "Disable Legacy Platform Tilemap");
                legacy.gameObject.SetActive(false);
            }
            if (!string.IsNullOrWhiteSpace(sourceTilemapName))
            {
                var source = FindContains(section.transform, sourceTilemapName);
                if (source != null)
                    foreach (var renderer in source.GetComponentsInChildren<TilemapRenderer>(true))
                    {
                        Undo.RecordObject(renderer, "Hide Source Team Tilemap");
                        renderer.enabled = false;
                    }
            }

            // Only direct blockout children are hidden. Props, hazards, doors and markers
            // are nested under their own authored roots and are intentionally untouched.
            foreach (Transform child in section.transform)
            {
                if (child == root.transform || child == legacy) continue;
                var renderer = child.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;
                if (section.name == "복도" && !(child.name.Contains("바닥") || child.name.Contains("천장") || child.name.Contains("벽")))
                    continue;
                if (renderer.bounds.size.x <= 0.3f && renderer.bounds.size.y >= 1.5f) continue;
                Undo.RecordObject(renderer, "Hide Replaced Blockout Visual");
                renderer.enabled = false;
            }
        }

        private static void EnsureThemeTiles(ThemeDefinition theme)
        {
            var folder = $"{GeneratedRoot}/{theme.id}";
            EnsureFolder(folder);
            for (var mask = 0; mask < 16; mask++)
            {
                var pngPath = $"{folder}/{theme.id}_Connected_{mask:00}.png";
                var tilePath = $"{folder}/{theme.id}_Connected_{mask:00}.asset";
                // Deterministic regeneration keeps the authored scene and checked-in
                // tile assets in sync when the theme generator is polished.
                var texture = BuildTexture(theme, mask);
                File.WriteAllBytes(pngPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureTexture(pngPath);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                tile.color = Color.white;
                EditorUtility.SetDirty(tile);
            }
        }

        private static Dictionary<int, Tile> LoadThemeTiles(string theme)
        {
            return Enumerable.Range(0, 16).ToDictionary(mask => mask, mask =>
                AssetDatabase.LoadAssetAtPath<Tile>($"{GeneratedRoot}/{theme}/{theme}_Connected_{mask:00}.asset"));
        }

        private static Texture2D BuildTexture(ThemeDefinition theme, int mask)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            for (var x = 0; x < TextureSize; x++)
            {
                var nx = x / (float)TextureSize;
                var ny = y / (float)TextureSize;
                var grain = Mathf.PerlinNoise(nx * 5.3f + theme.id.Length, ny * 5.3f + theme.id[0]) - 0.5f;
                var broad = Mathf.PerlinNoise(nx * 1.7f + 11f, ny * 1.7f + theme.id.Length) - 0.5f;
                var color = Tint(theme.baseColor, grain * 0.16f + broad * 0.12f);

                // Inset panel seams are shared across every tile, so adjacent tiles
                // read as one constructed wall rather than independent stickers.
                if (x < 4 || y < 4) color = Color.Lerp(color, theme.line, 0.42f);
                if (Mathf.Abs(x - TextureSize / 2) < 2 && y > 30 && y < TextureSize - 30)
                    color = Color.Lerp(color, theme.line, 0.18f);
                if (y > 42 && y < 48) color = Color.Lerp(color, theme.line, 0.2f);

                // Zone identity is baked into the sprite itself, not reconstructed
                // by a runtime material. The motifs intentionally cross a full cell
                // and therefore remain legible when many cells form one structure.
                if (theme.id is "Adamas" or "Exterior")
                {
                    var brickRow = y / 64;
                    var horizontalMortar = y % 64 < 4;
                    var verticalMortar = (x + (brickRow % 2) * 64) % 128 < 4;
                    if (horizontalMortar || verticalMortar)
                        color = Color.Lerp(color, theme.line, horizontalMortar ? 0.34f : 0.22f);
                }
                else if (theme.id is "Hidden" or "Corridor" or "Nadir")
                {
                    if (x % 86 < 4 || y % 86 < 4)
                        color = Color.Lerp(color, theme.line, 0.35f);
                    if ((x + y + theme.id.Length * 13) % 173 < 3)
                        color = Color.Lerp(color, theme.trim, 0.2f);
                }
                else if (theme.id == "Training")
                {
                    if (x % 64 < 3 || y % 64 < 3) color = Color.Lerp(color, theme.line, 0.28f);
                    if ((x + y) % 128 < 4) color = Color.Lerp(color, theme.accent, 0.34f);
                }
                else if (theme.id == "Thermal")
                {
                    var crack = Mathf.Abs((x * 3 + Mathf.RoundToInt(Mathf.Sin(y * 0.075f) * 24f)) % 137);
                    if (crack < 3 && y > 18) color = Color.Lerp(color, theme.accent, 0.5f);
                    if (y % 72 < 3) color = Color.Lerp(color, theme.line, 0.32f);
                }

                if ((mask & 1) == 0 && y >= TextureSize - 18) color = EdgeColor(theme, TextureSize - 1 - y, 18);
                if ((mask & 2) == 0 && x >= TextureSize - 13) color = EdgeColor(theme, TextureSize - 1 - x, 13);
                if ((mask & 4) == 0 && y < 13) color = EdgeColor(theme, y, 13);
                if ((mask & 8) == 0 && x < 13) color = EdgeColor(theme, x, 13);
                pixels[y * TextureSize + x] = color;
            }

            DrawRivet(pixels, 22, 22, theme.trim);
            DrawRivet(pixels, TextureSize - 23, 22, theme.trim);
            if ((mask & 1) == 0)
            {
                DrawRivet(pixels, 22, TextureSize - 23, theme.accent);
                DrawRivet(pixels, TextureSize - 23, TextureSize - 23, theme.accent);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Color EdgeColor(ThemeDefinition theme, int distance, int width)
        {
            var t = Mathf.Clamp01(distance / (float)Mathf.Max(1, width - 1));
            return Color.Lerp(theme.trim, theme.shadow, t * 0.72f);
        }

        private static void DrawRivet(Color[] pixels, int cx, int cy, Color color)
        {
            for (var y = -4; y <= 4; y++)
            for (var x = -4; x <= 4; x++)
                if (x * x + y * y <= 16)
                    pixels[(cy + y) * TextureSize + cx + x] = Tint(color, (x + y) * 0.015f);
        }

        private static void ConfigureTexture(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            var changed = importer.textureType != TextureImporterType.Sprite ||
                          Mathf.Abs(importer.spritePixelsPerUnit - PixelsPerUnit) > 0.01f ||
                          importer.filterMode != FilterMode.Bilinear || importer.mipmapEnabled;
            if (!changed) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static int Mask(HashSet<Vector3Int> cells, Vector3Int cell)
        {
            var mask = 0;
            if (cells.Contains(cell + Vector3Int.up)) mask |= 1;
            if (cells.Contains(cell + Vector3Int.right)) mask |= 2;
            if (cells.Contains(cell + Vector3Int.down)) mask |= 4;
            if (cells.Contains(cell + Vector3Int.left)) mask |= 8;
            return mask;
        }

        private static HashSet<Vector3Int> ReadCells(Tilemap tilemap)
        {
            var cells = new HashSet<Vector3Int>();
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
                if (tilemap.HasTile(position)) cells.Add(position);
            return cells;
        }

        private static int CountTiles(Tilemap tilemap) => ReadCells(tilemap).Count;

        private static GameObject NewChild(Transform parent, string name, params Type[] components)
        {
            var gameObject = components == null || components.Length == 0
                ? new GameObject(name)
                : new GameObject(name, components);
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static ThemeDefinition Theme(string id, string baseHex, string shadowHex, string lineHex, string trimHex, string accentHex) =>
            new()
            {
                id = id,
                baseColor = Hex(baseHex),
                shadow = Hex(shadowHex),
                line = Hex(lineHex),
                trim = Hex(trimHex),
                accent = Hex(accentHex)
            };

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        private static Color Tint(Color color, float amount) => new(
            Mathf.Clamp01(color.r + amount), Mathf.Clamp01(color.g + amount),
            Mathf.Clamp01(color.b + amount), color.a);

        private static float Fraction(float value)
        {
            var fraction = Mathf.Repeat(value, 1f);
            return fraction <= 0.001f || fraction >= 0.999f ? 0f : Mathf.Round(fraction * 1000f) / 1000f;
        }

        private static Bounds LocalBounds(Transform root, Bounds world)
        {
            var points = new[]
            {
                new Vector3(world.min.x, world.min.y), new Vector3(world.min.x, world.max.y),
                new Vector3(world.max.x, world.min.y), new Vector3(world.max.x, world.max.y)
            };
            var result = new Bounds(root.InverseTransformPoint(points[0]), Vector3.zero);
            for (var i = 1; i < points.Length; i++) result.Encapsulate(root.InverseTransformPoint(points[i]));
            return result;
        }

        private static GameObject FindExact(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.name == name)?.gameObject;

        private static Transform FindContains(Transform root, string name)
        {
            if (root.name.Contains(name, StringComparison.OrdinalIgnoreCase)) return root;
            foreach (Transform child in root)
            {
                var found = FindContains(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static string Path(GameObject gameObject) => PrometheusSceneQuery.Path(gameObject);

        private static PrometheusAiChange Change(string action, string path, string before, string after) => new()
        {
            action = action,
            hierarchyPath = path,
            before = before,
            after = after
        };
    }
}
