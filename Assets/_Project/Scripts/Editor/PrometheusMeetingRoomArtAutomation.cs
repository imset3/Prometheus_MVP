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
    public static class PrometheusMeetingRoomArtAutomation
    {
        private const string ReviewScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string ArtRoot = "Assets/_Project/Art/AIConcepts/TutorialMeetingRoomProps/Generated";
        private const string TablePath = ArtRoot + "/TUTO_A_CommandTable_v1.png";
        private const string MapPath = ArtRoot + "/TUTO_A_TacticalMap_v1.png";
        private const string VisualRootName = "AI_MeetingRoomArtRoot";
        private const string TableName = "CommandTable_ART";
        private const string MapName = "TacticalMap_ART";
        private static readonly Vector3 TablePosition = new(0f, -5.05f, 0f);
        private static readonly Vector3 MapPosition = new(-3.6f, 0.65f, 0f);
        private const float TableHeight = 4.2f;
        private const float MapHeight = 4.4f;

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (scene.path != ReviewScenePath)
                throw new InvalidOperationException("Meeting-room art is restricted to " + ReviewScenePath + "; active=" + scene.path);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying meeting-room art.");

            ValidateAssets();
            var room = RequireOne(scene, "회의장");
            var visualRoot = room.transform.Find(VisualRootName);
            var changes = new List<PrometheusAiChange>();
            DescribeProp(changes, room, visualRoot, TableName, TablePath, TablePosition, TableHeight);
            DescribeProp(changes, room, visualRoot, MapName, MapPath, MapPosition, MapHeight);
            if (dryRun || changes.Count == 0) return changes;

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply A meeting-room art");
            try
            {
                var table = ImportSprite(TablePath, new Vector2(0.5f, 0f));
                var map = ImportSprite(MapPath, new Vector2(0.5f, 0.5f));
                visualRoot = UpsertChild(room.transform, VisualRootName);
                UpsertProp(visualRoot, TableName, table, TablePosition, TableHeight, -10);
                UpsertProp(visualRoot, MapName, map, MapPosition, MapHeight, -20);
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

        private static void DescribeProp(List<PrometheusAiChange> changes, GameObject room,
            Transform visualRoot, string name, string spritePath, Vector3 worldPosition, float worldHeight)
        {
            var prop = visualRoot == null ? null : visualRoot.Find(name);
            var renderer = prop == null ? null : prop.GetComponent<SpriteRenderer>();
            var current = renderer == null || renderer.sprite == null
                ? "missing"
                : AssetDatabase.GetAssetPath(renderer.sprite);
            var matches = current == spritePath && Approximately(prop.position, worldPosition) &&
                          Mathf.Abs(renderer.bounds.size.y - worldHeight) <= 0.05f;
            if (matches) return;
            changes.Add(new PrometheusAiChange
            {
                action = "upsert-meeting-room-prop",
                objectId = PrometheusSceneQuery.ObjectId(prop == null ? room : prop.gameObject),
                hierarchyPath = PrometheusSceneQuery.Path(prop == null ? room : prop.gameObject),
                before = current,
                after = spritePath
            });
        }

        private static Transform UpsertChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create A meeting-room art object");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void UpsertProp(Transform parent, string name, Sprite sprite,
            Vector3 worldPosition, float worldHeight, int sortingOrder)
        {
            var prop = parent.Find(name) ?? UpsertChild(parent, name);
            var renderer = prop.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<SpriteRenderer>(prop.gameObject);
            Undo.RecordObject(renderer, "Configure A meeting-room prop");
            Undo.RecordObject(prop, "Place A meeting-room prop");
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
                scale / Mathf.Max(0.01f, Mathf.Abs(parentScale.y)),
                1f / Mathf.Max(0.01f, Mathf.Abs(parentScale.z)));
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(prop);
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
            if (sprite == null) throw new InvalidOperationException("Meeting-room sprite import failed: " + path);
            return sprite;
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
            foreach (var path in new[] { TablePath, MapPath })
                if (!File.Exists(Path.Combine(projectRoot, path)))
                    throw new FileNotFoundException("Missing A meeting-room art asset", path);
        }
    }
}
