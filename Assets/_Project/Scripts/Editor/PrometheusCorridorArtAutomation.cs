using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusCorridorArtAutomation
    {
        private const string ReviewScenePath = "Assets/Scenes/AIReview/TutorialScene_FPilot_Review.unity";
        private const string ArtRoot = "Assets/_Project/Art/AIConcepts/TutorialCorridorProps/Generated";
        private const string CratesPath = ArtRoot + "/TUTO_C_TransportCrates_v1.png";
        private const string ChestPath = ArtRoot + "/TUTO_C_MaintenanceChest_v1.png";
        private const string PipesPath = ArtRoot + "/TUTO_C_OverheadPipes_v1.png";
        private const string ColumnPath = ArtRoot + "/TUTO_C_StructuralColumn_v1.png";

        private sealed class Placement
        {
            public string objectName;
            public string spritePath;
            public Vector3 worldPosition;
            public float worldHeight;
            public Vector2 pivot;
            public int sortingOrder;
        }

        private static readonly Placement[] Placements =
        {
            new()
            {
                objectName = "창고상자_01", spritePath = CratesPath,
                worldPosition = new Vector3(123f, -5.05f, 0f), worldHeight = 3.35f,
                pivot = new Vector2(0.5f, 0f), sortingOrder = -8
            },
            new()
            {
                objectName = "창고상자_02", spritePath = ChestPath,
                worldPosition = new Vector3(127.2f, -5.05f, 0f), worldHeight = 1.55f,
                pivot = new Vector2(0.5f, 0f), sortingOrder = -7
            },
            new()
            {
                objectName = "천장배관", spritePath = PipesPath,
                worldPosition = new Vector3(134f, 5f, 0f), worldHeight = 2.05f,
                pivot = new Vector2(0.5f, 1f), sortingOrder = -18
            },
            new()
            {
                objectName = "중앙구조기둥", spritePath = ColumnPath,
                worldPosition = new Vector3(139f, -5.05f, 0f), worldHeight = 10.05f,
                pivot = new Vector2(0.5f, 0f), sortingOrder = -19
            }
        };

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (scene.path != ReviewScenePath)
                throw new InvalidOperationException("Corridor art is restricted to " + ReviewScenePath + "; active=" + scene.path);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying corridor art.");

            ValidateAssets();
            var corridor = RequireOne(scene, "복도");
            var targets = Placements.ToDictionary(
                placement => placement.objectName,
                placement => RequireDirectChild(corridor.transform, placement.objectName));
            var changes = DescribeChanges(targets);
            if (dryRun || changes.Count == 0) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply C corridor art");
            try
            {
                foreach (var placement in Placements)
                {
                    var sprite = ImportSprite(placement.spritePath, placement.pivot);
                    ApplyPlacement(targets[placement.objectName], sprite, placement);
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

        private static List<PrometheusAiChange> DescribeChanges(
            IReadOnlyDictionary<string, GameObject> targets)
        {
            var changes = new List<PrometheusAiChange>();
            foreach (var placement in Placements)
            {
                var target = targets[placement.objectName];
                var renderer = target.GetComponent<SpriteRenderer>();
                var current = renderer == null || renderer.sprite == null
                    ? "missing"
                    : AssetDatabase.GetAssetPath(renderer.sprite);
                var matches = current == placement.spritePath &&
                              Approximately(target.transform.position, placement.worldPosition) &&
                              Mathf.Abs(renderer.bounds.size.y - placement.worldHeight) <= 0.05f &&
                              renderer.sortingOrder == placement.sortingOrder;
                if (matches) continue;
                changes.Add(new PrometheusAiChange
                {
                    action = "replace-corridor-placeholder-art",
                    objectId = PrometheusSceneQuery.ObjectId(target),
                    hierarchyPath = PrometheusSceneQuery.Path(target),
                    before = current,
                    after = placement.spritePath
                });
            }
            return changes;
        }

        private static void ApplyPlacement(GameObject target, Sprite sprite, Placement placement)
        {
            var renderer = target.GetComponent<SpriteRenderer>() ??
                           throw new InvalidOperationException("Missing SpriteRenderer: " + target.name);
            Undo.RecordObject(renderer, "Replace C corridor placeholder sprite");
            Undo.RecordObject(target.transform, "Fit C corridor prop");
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.drawMode = SpriteDrawMode.Simple;
            renderer.sortingOrder = placement.sortingOrder;
            target.transform.position = placement.worldPosition;
            target.transform.rotation = Quaternion.identity;
            var scale = placement.worldHeight / Mathf.Max(0.01f, sprite.bounds.size.y);
            var parentScale = target.transform.parent.lossyScale;
            target.transform.localScale = new Vector3(
                scale / Mathf.Max(0.01f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)),
                1f / Mathf.Max(0.01f, Mathf.Abs(parentScale.z)));
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(target.transform);
        }

        private static Sprite ImportSprite(string path, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256f;
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
            if (sprite == null) throw new InvalidOperationException("Corridor sprite import failed: " + path);
            return sprite;
        }

        private static GameObject RequireDirectChild(Transform parent, string name)
        {
            var child = parent.Cast<Transform>().SingleOrDefault(item => item.name == name);
            return child == null
                ? throw new InvalidOperationException("Missing direct child " + name + " under " + parent.name)
                : child.gameObject;
        }

        private static GameObject RequireOne(Scene scene, string name)
        {
            var matches = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene == scene && item.name == name).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Expected one " + name + ", found " + matches.Length);
            return matches[0];
        }

        private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= 0.0025f;

        private static void ValidateAssets()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (var placement in Placements)
                if (!File.Exists(Path.Combine(projectRoot, placement.spritePath)))
                    throw new FileNotFoundException("Missing C corridor art asset", placement.spritePath);
        }
    }
}
