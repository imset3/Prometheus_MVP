using System;
using System.Collections.Generic;
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
    public static class PrometheusTilemapSceneIntegrator
    {
        private static readonly (string nodeName, string prefabPath, string integrationName)[] Mappings = new[]
        {
            ("회의장", "Assets/TileMap/회의장-타일맵.prefab", "A_Meeting_Integration"),
            ("숨겨진방", "Assets/TileMap/숨겨진방-타일맵.prefab", "B_HiddenRoom_Integration"),
            ("외부", "Assets/TileMap/외부-타일맵.prefab", "E_Exterior_Integration"),
            ("훈련장", "Assets/TileMap/훈련장-타일맵.prefab", "D_Training_Integration"),
            ("선착장", "Assets/TileMap/선착장-타일맵.prefab", "H_Helte_Integration"),
            ("F스테이지", "Assets/TileMap/F스테이지-타일맵.prefab", "F_Encounter01_Integration"),
            ("G스테이지", "Assets/TileMap/G스테이지-타일맵.prefab", "G_Encounter02_Integration")
        };

        private const string RebuiltPlatformRootName = "재구성_플랫폼타일맵";
        private static readonly Vector3Int[] CardinalDirections =
        {
            Vector3Int.up,
            Vector3Int.right,
            Vector3Int.down,
            Vector3Int.left
        };

        [MenuItem(PrometheusToolMenuPaths.Analysis + "Report Team Tilemap Platform Replacement")]
        public static void ReportTeamTilemapPlatformReplacement()
        {
            ReplaceBlockoutPlatformVisuals(dryRun: true);
        }

        [MenuItem(PrometheusToolMenuPaths.Root + "Tilemap/Apply Team Tilemaps as Platform Visuals")]
        public static void ApplyTeamTilemapsAsPlatformVisuals()
        {
            ReplaceBlockoutPlatformVisuals(dryRun: false);
        }

        private static void ReplaceBlockoutPlatformVisuals(bool dryRun)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            var hiddenBlockoutCount = 0;
            var promotedTilemapCount = 0;
            foreach (var (nodeName, _, _) in Mappings)
            {
                var sectionName = nodeName == "훈련장" ? "훈련장 수정버전" : nodeName;
                var section = FindSectionNode(scene, sectionName);
                var tilemapRoot = section != null
                    ? FindDescendant(section.transform, nodeName + "-타일맵") ??
                      FindDescendant(section.transform, "TileMap")
                    : null;
                if (section == null || tilemapRoot == null)
                {
                    Debug.LogWarning($"[sragon000][Tilemap Platform] {sectionName}: section or team tilemap missing.");
                    continue;
                }

                // The imported white blockout platforms are direct children of each
                // Korean section root. Decorative art and functional marker visuals
                // live below their own child roots and are intentionally preserved.
                var blockoutRenderers = section.transform.Cast<Transform>()
                    .Where(child => child != tilemapRoot)
                    .Select(child => child.GetComponent<SpriteRenderer>())
                    .Where(renderer => renderer != null)
                    .ToArray();
                var tilemapRenderers = tilemapRoot.GetComponentsInChildren<TilemapRenderer>(true);

                Debug.Log(
                    $"[sragon000][Tilemap Platform] {sectionName} | " +
                    $"hideBlockout={blockoutRenderers.Length}, promoteTilemap={tilemapRenderers.Length}, " +
                    $"dryRun={dryRun}");
                hiddenBlockoutCount += blockoutRenderers.Length;
                promotedTilemapCount += tilemapRenderers.Length;
                if (dryRun) continue;

                foreach (var renderer in blockoutRenderers)
                {
                    Undo.RecordObject(renderer, "Hide Replaced Blockout Platform Visual");
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }

                foreach (var renderer in tilemapRenderers)
                {
                    Undo.RecordObject(renderer, "Promote Team Tilemap Platform Visual");
                    renderer.enabled = true;
                    renderer.sortingOrder = 0;
                    EditorUtility.SetDirty(renderer);
                }
            }

            if (!dryRun)
                EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"[sragon000][Tilemap Platform] complete | hiddenBlockout={hiddenBlockoutCount}, " +
                $"promotedTilemap={promotedTilemapCount}, dryRun={dryRun}");
        }

        [MenuItem(PrometheusToolMenuPaths.Analysis + "Report Rebuilt Platform Tilemaps")]
        public static void ReportRebuiltPlatformTilemaps()
        {
            RebuildPlatformTilemaps(dryRun: true);
        }

        [MenuItem(PrometheusToolMenuPaths.Root + "Tilemap/Rebuild Platforms and Walls from Team Tiles")]
        public static void RebuildPlatformTilemapsMenu()
        {
            RebuildPlatformTilemaps(dryRun: false);
        }

        private static void RebuildPlatformTilemaps(bool dryRun)
        {
            var scene = EditorSceneManager.GetActiveScene();
            RebuildPlatformTilemaps(scene, dryRun, null);
        }

        internal static void RebuildPlatformTilemaps(
            Scene scene,
            bool dryRun,
            IReadOnlyCollection<string> onlyZones)
        {
            if (!scene.IsValid()) return;

            var rebuiltZoneCount = 0;
            var rebuiltCellCount = 0;
            foreach (var (nodeName, prefabPath, _) in Mappings)
            {
                if (onlyZones != null && onlyZones.Count > 0 && !onlyZones.Contains(nodeName)) continue;
                var sectionName = nodeName == "훈련장" ? "훈련장 수정버전" : nodeName;
                var section = FindSectionNode(scene, sectionName);
                if (section == null)
                {
                    Debug.LogWarning($"[sragon000][Tilemap Rebuild] {sectionName}: section missing.");
                    continue;
                }

                var sourceTilemapRoot = FindDescendant(section.transform, nodeName + "-타일맵") ??
                                        FindDescendant(section.transform, "TileMap");
                var blockoutRenderers = section.transform.Cast<Transform>()
                    .Where(child => child != sourceTilemapRoot && child.name != RebuiltPlatformRootName)
                    .Select(child => child.GetComponent<SpriteRenderer>())
                    .Where(IsStaticPlatformRenderer)
                    .ToArray();
                if (blockoutRenderers.Length == 0)
                {
                    Debug.LogWarning($"[sragon000][Tilemap Rebuild] {sectionName}: blockout platform renderers missing.");
                    continue;
                }

                var localBounds = blockoutRenderers
                    .Select(renderer => ToLocalBounds(section.transform, renderer.bounds))
                    .ToArray();
                var clearanceBounds = FindClearanceBounds(section.transform);
                var boundsGroups = localBounds
                    .GroupBy(ResolveBoundsGridOffset)
                    .OrderBy(group => group.Key.y)
                    .ThenBy(group => group.Key.x)
                    .Select(group => (offset: group.Key, bounds: group.ToArray()))
                    .ToArray();
                var occupiedCellCount = boundsGroups.Sum(group =>
                    BuildOccupiedCells(group.bounds, group.offset, clearanceBounds).Count);
                // F and G are consecutive exterior combat stages. G's source map
                // contains pink gameplay guide tiles mixed into its palette, so the
                // rebuilt structural floor/walls share F's clean exterior palette.
                var palettePrefabPath = nodeName == "G스테이지"
                    ? "Assets/TileMap/F스테이지-타일맵.prefab"
                    : prefabPath;
                var tileRules = BuildTileRules(palettePrefabPath);
                if (occupiedCellCount == 0 || tileRules.fallback == null)
                {
                    Debug.LogWarning(
                        $"[sragon000][Tilemap Rebuild] {sectionName}: no target cells or source team tiles.");
                    continue;
                }

                Debug.Log(
                    $"[sragon000][Tilemap Rebuild] {sectionName} | blockouts={blockoutRenderers.Length}, " +
                    $"cells={occupiedCellCount}, gridLayers={boundsGroups.Length}, " +
                    $"teamRules={tileRules.byMask.Count}, dryRun={dryRun}");
                rebuiltZoneCount++;
                rebuiltCellCount += occupiedCellCount;
                if (dryRun) continue;

                var existing = section.transform.Find(RebuiltPlatformRootName);
                if (existing != null)
                    Undo.DestroyObjectImmediate(existing.gameObject);

                var rebuiltRoot = new GameObject(RebuiltPlatformRootName);
                Undo.RegisterCreatedObjectUndo(rebuiltRoot, "Create Rebuilt Platform Tilemap");
                rebuiltRoot.transform.SetParent(section.transform, false);
                rebuiltRoot.transform.localPosition = Vector3.zero;

                for (var layerIndex = 0; layerIndex < boundsGroups.Length; layerIndex++)
                {
                    var group = boundsGroups[layerIndex];
                    var occupiedCells = BuildOccupiedCells(group.bounds, group.offset, clearanceBounds);
                    var gridObject = new GameObject($"격자레이어_{layerIndex + 1:00}", typeof(Grid));
                    gridObject.transform.SetParent(rebuiltRoot.transform, false);
                    gridObject.transform.localPosition = new Vector3(group.offset.x, group.offset.y, 0f);
                    var tilemapObject = new GameObject($"플랫폼과벽_{layerIndex + 1:00}");
                    tilemapObject.transform.SetParent(gridObject.transform, false);
                    // Parent first, then add Tilemap so its layoutGrid is bound to
                    // this layer's Grid rather than cached as null at creation.
                    var targetTilemap = tilemapObject.AddComponent<Tilemap>();
                    var targetRenderer = tilemapObject.AddComponent<TilemapRenderer>();
                    targetRenderer.sortingOrder = 0;
                    foreach (var cell in occupiedCells)
                    {
                        var mask = GetNeighbourMask(occupiedCells, cell);
                        var tile = tileRules.byMask.TryGetValue(mask, out var matched)
                            ? matched
                            : tileRules.fallback;
                        targetTilemap.SetTile(cell, tile);
                    }
                }

                if (sourceTilemapRoot != null)
                {
                    foreach (var renderer in sourceTilemapRoot.GetComponentsInChildren<TilemapRenderer>(true))
                    {
                        Undo.RecordObject(renderer, "Hide Source Team Tilemap Layout");
                        renderer.enabled = false;
                    }

                    // The source prefab is now palette/reference data only. Its
                    // collider would no longer match the rebuilt cells and must not
                    // compete with the preserved authored gameplay colliders.
                    foreach (var collider in sourceTilemapRoot.GetComponentsInChildren<TilemapCollider2D>(true))
                    {
                        Undo.RecordObject(collider, "Disable Replaced Source Tilemap Collider");
                        collider.enabled = false;
                    }
                }

                foreach (var renderer in blockoutRenderers)
                {
                    Undo.RecordObject(renderer, "Hide Rebuilt Blockout Visual");
                    renderer.enabled = false;
                }
            }

            if (!dryRun)
                EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"[sragon000][Tilemap Rebuild] complete | zones={rebuiltZoneCount}, " +
                $"cells={rebuiltCellCount}, dryRun={dryRun}");
        }

        internal static List<PrometheusAiRecord> AuditSolidColliders(
            Scene scene,
            Vector2 center,
            Vector2 size)
        {
            var queryBounds = new Bounds(center, new Vector3(size.x, size.y, 1f));
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Collider2D>(true))
                .Where(collider => collider != null &&
                                   collider.enabled &&
                                   !collider.isTrigger &&
                                   collider.gameObject.scene == scene &&
                                   collider.bounds.Intersects(queryBounds) &&
                                   collider.GetComponentInParent<PlayerInputHost>(true) == null)
                .OrderBy(collider => collider.bounds.center.x)
                .ThenBy(collider => collider.bounds.center.y)
                .Select(collider => new PrometheusAiRecord
                {
                    id = PrometheusSceneQuery.ObjectId(collider.gameObject),
                    kind = collider.GetType().Name,
                    hierarchyPath = PrometheusSceneQuery.Path(collider.gameObject),
                    value = $"boundsCenter={collider.bounds.center}; boundsSize={collider.bounds.size}; " +
                            $"renderer={collider.GetComponent<Renderer>()?.GetType().Name ?? "none"}",
                    position = collider.transform.position,
                    rotation = collider.transform.eulerAngles,
                    scale = collider.transform.lossyScale,
                    active = collider.gameObject.activeInHierarchy
                })
                .ToList();
        }

        internal static List<PrometheusAiChange> ApplyTilemapClearance(
            Scene scene,
            string markerId,
            string zoneName,
            Vector2 center,
            Vector2 size,
            bool dryRun)
        {
            var sectionName = zoneName == "훈련장" ? "훈련장 수정버전" : zoneName;
            var section = FindSectionNode(scene, sectionName);
            if (section == null) throw new InvalidOperationException($"Tilemap zone '{sectionName}' was not found.");
            if (string.IsNullOrWhiteSpace(markerId))
                throw new ArgumentException("A stable tilemap-clearance markerId is required.");

            size = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y));
            var bounds = new Bounds(center, new Vector3(size.x, size.y, 1f));
            var changes = new List<PrometheusAiChange>();
            var markerObject = PrometheusMarkerAuthoring.FindById(scene, markerId);
            changes.Add(new PrometheusAiChange
            {
                action = markerObject == null ? "create-tilemap-clearance" : "update-tilemap-clearance",
                objectId = markerObject == null ? null : PrometheusSceneQuery.ObjectId(markerObject),
                hierarchyPath = markerObject == null
                    ? PrometheusSceneQuery.Path(section) + "/" + markerId
                    : PrometheusSceneQuery.Path(markerObject),
                before = markerObject == null ? "missing" : DescribeMarker(markerObject),
                after = $"zone={zoneName}; center={center}; size={size}"
            });

            var mapping = Mappings.FirstOrDefault(item => item.nodeName == zoneName);
            var integrationRoot = string.IsNullOrWhiteSpace(mapping.integrationName)
                ? null
                : FindDescendant(scene.GetRootGameObjects()
                    .FirstOrDefault(root => root.name == "GameplayIntegrationRoot")?.transform,
                    mapping.integrationName);
            var solidColliders = integrationRoot == null
                ? Array.Empty<Collider2D>()
                : integrationRoot.GetComponentsInChildren<Collider2D>(true)
                    .Where(collider => collider != null && collider.enabled && !collider.isTrigger)
                    .Where(collider => bounds.Contains(GetWorldBounds(collider).center))
                    .ToArray();
            foreach (var collider in solidColliders)
                changes.Add(new PrometheusAiChange
                {
                    action = "disable-clearance-blocker",
                    objectId = PrometheusSceneQuery.ObjectId(collider.gameObject),
                    hierarchyPath = PrometheusSceneQuery.Path(collider.gameObject),
                    before = $"enabled=true; bounds={GetWorldBounds(collider)}",
                    after = "enabled=false; reserved for object/traversal clearance"
                });
            changes.Add(new PrometheusAiChange
            {
                action = "rebuild-zone-tilemap-with-clearance",
                hierarchyPath = PrometheusSceneQuery.Path(section),
                before = "rebuilt tile cells may overlap the reserved area",
                after = $"exclude cells intersecting marker '{markerId}'"
            });
            if (dryRun) return changes;

            if (markerObject == null)
            {
                markerObject = PrometheusMarkerAuthoring.Create(
                    scene,
                    section.transform,
                    TutorialFunctionMarkerKind.TilemapClearance,
                    markerId,
                    center,
                    size);
            }
            else
            {
                var marker = markerObject.GetComponent<TutorialFunctionMarkerHost>();
                if (marker == null || marker.Kind != TutorialFunctionMarkerKind.TilemapClearance)
                    throw new InvalidOperationException($"Marker '{markerId}' is not a tilemap-clearance marker.");
                Undo.RecordObject(markerObject.transform, "Move Tilemap Clearance Marker");
                markerObject.transform.position = center;
                if (markerObject.TryGetComponent<BoxCollider2D>(out var markerCollider))
                {
                    Undo.RecordObject(markerCollider, "Resize Tilemap Clearance Marker");
                    markerCollider.size = size;
                }
            }

            foreach (var collider in solidColliders)
            {
                Undo.RecordObject(collider, "Disable Tilemap Clearance Blocker");
                collider.enabled = false;
                EditorUtility.SetDirty(collider);
            }
            RebuildPlatformTilemaps(scene, false, new[] { zoneName });
            EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        private static bool IsStaticPlatformRenderer(SpriteRenderer renderer)
        {
            if (renderer == null || !renderer.gameObject.activeSelf) return false;
            // Thin vertical rectangles in G are encounter gates (39/43), retired
            // blockers (40/60), or other stateful doors. Baking them into a static
            // tilemap makes the visual remain after its gameplay gate opens.
            var size = renderer.bounds.size;
            return !(size.x <= 0.3f && size.y >= 1.5f);
        }

        private static Bounds ToLocalBounds(Transform sectionRoot, Bounds worldBounds)
        {
            var corners = new[]
            {
                new Vector3(worldBounds.min.x, worldBounds.min.y),
                new Vector3(worldBounds.min.x, worldBounds.max.y),
                new Vector3(worldBounds.max.x, worldBounds.min.y),
                new Vector3(worldBounds.max.x, worldBounds.max.y)
            };
            var first = sectionRoot.InverseTransformPoint(corners[0]);
            var result = new Bounds(first, Vector3.zero);
            for (var index = 1; index < corners.Length; index++)
                result.Encapsulate(sectionRoot.InverseTransformPoint(corners[index]));
            return result;
        }

        private static Vector2 ResolveBoundsGridOffset(Bounds bounds)
        {
            // Every authored blockout rectangle keeps its own fractional grid origin.
            // A single zone-wide offset cannot represent both integer and half-cell
            // platform edges and would move some rendered floors by half a tile.
            return new Vector2(
                QuantizeGridFraction(bounds.min.x),
                QuantizeGridFraction(bounds.min.y));
        }

        private static float QuantizeGridFraction(float value)
        {
            var fraction = Mathf.Repeat(value, 1f);
            if (fraction >= 0.999f || fraction <= 0.001f) return 0f;
            return Mathf.Round(fraction * 1000f) / 1000f;
        }

        private static HashSet<Vector3Int> BuildOccupiedCells(
            IReadOnlyList<Bounds> localBounds,
            Vector2 gridOffset,
            IReadOnlyList<Bounds> clearanceBounds = null)
        {
            var occupied = new HashSet<Vector3Int>();
            foreach (var bounds in localBounds)
            {
                const float edgeTolerance = 0.001f;
                var minX = Mathf.FloorToInt(bounds.min.x - gridOffset.x + edgeTolerance);
                var maxX = Mathf.CeilToInt(bounds.max.x - gridOffset.x - edgeTolerance);
                var minY = Mathf.FloorToInt(bounds.min.y - gridOffset.y + edgeTolerance);
                var maxY = Mathf.CeilToInt(bounds.max.y - gridOffset.y - edgeTolerance);
                if (maxX <= minX) maxX = minX + 1;
                if (maxY <= minY) maxY = minY + 1;
                for (var x = minX; x < maxX; x++)
                for (var y = minY; y < maxY; y++)
                    occupied.Add(new Vector3Int(x, y, 0));
            }
            if (clearanceBounds != null && clearanceBounds.Count > 0)
                occupied.RemoveWhere(cell => clearanceBounds.Any(clearance =>
                    clearance.Intersects(new Bounds(
                        new Vector3(cell.x + gridOffset.x + 0.5f, cell.y + gridOffset.y + 0.5f, 0f),
                        new Vector3(0.98f, 0.98f, 1f)))));
            return occupied;
        }

        private static List<Bounds> FindClearanceBounds(Transform section)
        {
            return section.GetComponentsInChildren<TutorialFunctionMarkerHost>(true)
                .Where(marker => marker.Kind == TutorialFunctionMarkerKind.TilemapClearance)
                .Select(marker => marker.GetComponent<BoxCollider2D>())
                .Where(collider => collider != null)
                .Select(collider => ToLocalBounds(section, GetWorldBounds(collider)))
                .ToList();
        }

        private static Bounds GetWorldBounds(Collider2D collider)
        {
            if (collider.gameObject.activeInHierarchy) return collider.bounds;
            if (collider is BoxCollider2D box)
            {
                var half = box.size * 0.5f;
                var corners = new[]
                {
                    box.offset + new Vector2(-half.x, -half.y),
                    box.offset + new Vector2(-half.x, half.y),
                    box.offset + new Vector2(half.x, -half.y),
                    box.offset + new Vector2(half.x, half.y)
                };
                var first = collider.transform.TransformPoint(corners[0]);
                var result = new Bounds(first, Vector3.zero);
                for (var index = 1; index < corners.Length; index++)
                    result.Encapsulate(collider.transform.TransformPoint(corners[index]));
                return result;
            }
            return new Bounds(collider.transform.position, Vector3.zero);
        }

        private static string DescribeMarker(GameObject marker)
        {
            var collider = marker.GetComponent<BoxCollider2D>();
            return $"center={marker.transform.position}; size={(collider == null ? Vector2.zero : collider.size)}";
        }

        private static (Dictionary<int, TileBase> byMask, TileBase fallback) BuildTileRules(string prefabPath)
        {
            var countsByMask = new Dictionary<int, Dictionary<TileBase, int>>();
            var fallbackCounts = new Dictionary<TileBase, int>();
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var sourceTilemap in prefabRoot.GetComponentsInChildren<Tilemap>(true))
                {
                    var occupied = new HashSet<Vector3Int>();
                    foreach (var position in sourceTilemap.cellBounds.allPositionsWithin)
                        if (sourceTilemap.HasTile(position))
                            occupied.Add(position);
                    foreach (var position in occupied)
                    {
                        var tile = sourceTilemap.GetTile(position);
                        if (tile == null) continue;
                        var mask = GetNeighbourMask(occupied, position);
                        if (!countsByMask.TryGetValue(mask, out var counts))
                        {
                            counts = new Dictionary<TileBase, int>();
                            countsByMask.Add(mask, counts);
                        }
                        counts[tile] = counts.TryGetValue(tile, out var count) ? count + 1 : 1;
                        fallbackCounts[tile] = fallbackCounts.TryGetValue(tile, out var fallbackCount)
                            ? fallbackCount + 1
                            : 1;
                    }
                }

                return (
                    countsByMask.ToDictionary(pair => pair.Key, pair => MostFrequent(pair.Value)),
                    fallbackCounts.Count > 0 ? MostFrequent(fallbackCounts) : null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static TileBase MostFrequent(IReadOnlyDictionary<TileBase, int> counts)
        {
            return counts.OrderByDescending(pair => pair.Value).First().Key;
        }

        private static int GetNeighbourMask(HashSet<Vector3Int> occupied, Vector3Int cell)
        {
            var mask = 0;
            for (var index = 0; index < CardinalDirections.Length; index++)
                if (occupied.Contains(cell + CardinalDirections[index]))
                    mask |= 1 << index;
            return mask;
        }

        [MenuItem(PrometheusToolMenuPaths.Analysis + "Report Team Tilemap Alignment")]
        public static void ReportTeamTilemapAlignment()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            foreach (var (nodeName, prefabPath, integrationName) in Mappings)
            {
                var section = FindSectionNode(scene, nodeName == "훈련장" ? "훈련장 수정버전" : nodeName);
                var integration = FindSectionNode(scene, integrationName);
                var tilemap = section != null
                    ? FindDescendant(section.transform, nodeName + "-타일맵") ??
                      FindDescendant(section.transform, "TileMap")
                    : null;

                if (section == null || integration == null || tilemap == null)
                {
                    Debug.LogWarning(
                        $"[sragon000][Tilemap Alignment] {nodeName}: section/integration/tilemap reference missing.");
                    continue;
                }

                if (!TryPrefabTilemapBounds(prefabPath, tilemap, out var artBounds) ||
                    !TrySolidColliderBounds(integration.transform, out var gameplayBounds))
                {
                    Debug.LogWarning(
                        $"[sragon000][Tilemap Alignment] {nodeName}: bounds unavailable.");
                    continue;
                }

                TryBlockoutRendererBounds(section.transform, tilemap, out var blockoutBounds);

                var scaleX = gameplayBounds.size.x / Mathf.Max(0.01f, artBounds.size.x);
                var scaleY = gameplayBounds.size.y / Mathf.Max(0.01f, artBounds.size.y);
                Debug.Log(
                    $"[sragon000][Tilemap Alignment] {nodeName} | " +
                    $"art center=({artBounds.center.x:F2},{artBounds.center.y:F2}) " +
                    $"size=({artBounds.size.x:F2},{artBounds.size.y:F2}) | " +
                    $"gameplay center=({gameplayBounds.center.x:F2},{gameplayBounds.center.y:F2}) " +
                    $"size=({gameplayBounds.size.x:F2},{gameplayBounds.size.y:F2}) | " +
                    $"scaleRatio=({scaleX:F3},{scaleY:F3}) " +
                    $"bottomDelta={gameplayBounds.min.y - artBounds.min.y:F2} " +
                    $"centerXDelta={gameplayBounds.center.x - artBounds.center.x:F2} | " +
                    $"blockout center=({blockoutBounds.center.x:F2},{blockoutBounds.center.y:F2}) " +
                    $"size=({blockoutBounds.size.x:F2},{blockoutBounds.size.y:F2}) " +
                    $"blockoutDelta=({blockoutBounds.center.x - artBounds.center.x:F2}," +
                    $"{blockoutBounds.center.y - artBounds.center.y:F2})");
            }
        }

        private static bool TryBlockoutRendererBounds(
            Transform sectionRoot,
            Transform tilemapRoot,
            out Bounds bounds)
        {
            var renderers = sectionRoot.Cast<Transform>()
                .Where(child => child != tilemapRoot)
                .Select(child => child.GetComponent<SpriteRenderer>())
                .Where(renderer => renderer != null)
                .ToArray();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return true;
        }

        private static bool TryPrefabTilemapBounds(string prefabPath, Transform instanceRoot, out Bounds bounds)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var tilemaps = prefabRoot.GetComponentsInChildren<Tilemap>(true)
                    .Where(tilemap => tilemap != null && tilemap.cellBounds.size != Vector3Int.zero)
                    .ToArray();
                if (tilemaps.Length == 0)
                {
                    bounds = default;
                    return false;
                }

                bounds = GetPrefabTilemapWorldBounds(prefabRoot.transform, instanceRoot, tilemaps[0]);
                for (var index = 1; index < tilemaps.Length; index++)
                    bounds.Encapsulate(GetPrefabTilemapWorldBounds(prefabRoot.transform, instanceRoot, tilemaps[index]));
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Bounds GetPrefabTilemapWorldBounds(
            Transform prefabRoot,
            Transform instanceRoot,
            Tilemap tilemap)
        {
            var cellBounds = tilemap.cellBounds;
            var prefabWorldMin = tilemap.transform.TransformPoint(tilemap.CellToLocal(cellBounds.min));
            var prefabWorldMax = tilemap.transform.TransformPoint(tilemap.CellToLocal(cellBounds.max));
            var instanceWorldMin = instanceRoot.TransformPoint(prefabRoot.InverseTransformPoint(prefabWorldMin));
            var instanceWorldMax = instanceRoot.TransformPoint(prefabRoot.InverseTransformPoint(prefabWorldMax));
            var min = Vector3.Min(instanceWorldMin, instanceWorldMax);
            var max = Vector3.Max(instanceWorldMin, instanceWorldMax);
            var result = new Bounds();
            result.SetMinMax(min, max);
            return result;
        }

        private static bool TryRendererBounds(Transform root, out Bounds bounds)
        {
            var tilemaps = root.GetComponentsInChildren<Tilemap>(true)
                .Where(tilemap => tilemap != null && tilemap.cellBounds.size != Vector3Int.zero)
                .ToArray();
            if (tilemaps.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = GetTilemapWorldBounds(tilemaps[0]);
            for (var index = 1; index < tilemaps.Length; index++)
                bounds.Encapsulate(GetTilemapWorldBounds(tilemaps[index]));
            return true;
        }

        private static Bounds GetTilemapWorldBounds(Tilemap tilemap)
        {
            var cellBounds = tilemap.cellBounds;
            var localMin = tilemap.CellToLocal(cellBounds.min);
            var localMax = tilemap.CellToLocal(cellBounds.max);
            var worldMin = tilemap.transform.TransformPoint(localMin);
            var worldMax = tilemap.transform.TransformPoint(localMax);
            var min = Vector3.Min(worldMin, worldMax);
            var max = Vector3.Max(worldMin, worldMax);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private static bool TrySolidColliderBounds(Transform root, out Bounds bounds)
        {
            var colliders = root.GetComponentsInChildren<Collider2D>(true)
                .Where(collider => collider != null && collider.enabled && !collider.isTrigger)
                .ToArray();
            if (colliders.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = colliders[0].bounds;
            for (var index = 1; index < colliders.Length; index++)
                bounds.Encapsulate(colliders[index].bounds);
            return true;
        }

        private static GameObject FindSectionNode(Scene scene, string name)
        {
            var transforms = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .ToArray();
            return transforms.FirstOrDefault(transform => transform.name == name)?.gameObject ??
                   transforms.FirstOrDefault(transform => transform.name.Contains(name))?.gameObject;
        }

        private static Transform FindDescendant(Transform root, string substring)
        {
            if (root.name.Contains(substring, StringComparison.OrdinalIgnoreCase)) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDescendant(root.GetChild(i), substring);
                if (found != null) return found;
            }
            return null;
        }
    }
}
