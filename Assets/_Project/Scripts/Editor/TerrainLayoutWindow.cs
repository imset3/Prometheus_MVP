using Narthex.Presentation;
using UnityEditor;
using UnityEngine;

namespace Narthex.Tools
{
    public sealed class TerrainLayoutWindow : EditorWindow
    {
        private const float DefaultGridSize = 0.5f;

        private Vector2 nextSize = new Vector2(4f, 1f);
        private Vector2 nextPosition;
        private float gridSize = DefaultGridSize;

        [MenuItem("Prometheus/Level Design/Terrain Layout")]
        private static void Open() => GetWindow<TerrainLayoutWindow>("Terrain Layout");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create Terrain Block", EditorStyles.boldLabel);
            nextPosition = EditorGUILayout.Vector2Field("Position", nextPosition);
            nextSize = EditorGUILayout.Vector2Field("Size", nextSize);
            gridSize = Mathf.Max(0.25f, EditorGUILayout.FloatField("Grid Size", gridSize));

            if (GUILayout.Button("Create Block")) CreateBlock();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected Terrain Block", EditorStyles.boldLabel);
            var block = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<TerrainBlock>()
                : null;

            using (new EditorGUI.DisabledScope(block == null))
            {
                if (GUILayout.Button("Snap Selected To Grid")) SnapSelected(block);
                if (GUILayout.Button("Widen Selected")) ResizeSelected(block, new Vector2(gridSize, 0f));
                if (GUILayout.Button("Raise Selected")) ResizeSelected(block, new Vector2(0f, gridSize));
            }

            if (block == null)
                EditorGUILayout.HelpBox("Select a TerrainBlock in the Hierarchy to snap or resize it.", MessageType.Info);
        }

        private void CreateBlock()
        {
            var root = FindOrCreateRoot();
            var blockObject = new GameObject("TerrainBlock");
            Undo.RegisterCreatedObjectUndo(blockObject, "Create Terrain Block");
            blockObject.transform.SetParent(root);
            blockObject.transform.position = Snap(nextPosition);

            var collisionObject = new GameObject("Collision");
            collisionObject.transform.SetParent(blockObject.transform, false);
            var collision = collisionObject.AddComponent<BoxCollider2D>();

            var visualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualObject.name = "Visual";
            Undo.RegisterCreatedObjectUndo(visualObject, "Create Terrain Visual");
            visualObject.transform.SetParent(blockObject.transform, false);
            DestroyImmediate(visualObject.GetComponent<BoxCollider>());

            var block = blockObject.AddComponent<TerrainBlock>();
            var serialized = new SerializedObject(block);
            serialized.FindProperty("collisionShape").objectReferenceValue = collision;
            serialized.FindProperty("visualShape").objectReferenceValue = visualObject.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            block.SetSize(nextSize);
            Selection.activeGameObject = blockObject;
        }

        private Transform FindOrCreateRoot()
        {
            var existing = GameObject.Find("TerrainLayoutRoot");
            if (existing != null) return existing.transform;

            var root = new GameObject("TerrainLayoutRoot");
            Undo.RegisterCreatedObjectUndo(root, "Create Terrain Layout Root");
            return root.transform;
        }

        private void SnapSelected(TerrainBlock block)
        {
            Undo.RecordObject(block.transform, "Snap Terrain Block");
            block.transform.position = Snap(block.transform.position);
            EditorUtility.SetDirty(block.transform);
        }

        private void ResizeSelected(TerrainBlock block, Vector2 delta)
        {
            Undo.RecordObject(block, "Resize Terrain Block");
            block.SetSize(block.Size + delta);
            EditorUtility.SetDirty(block);
        }

        private Vector3 Snap(Vector2 value)
        {
            return new Vector3(
                Mathf.Round(value.x / gridSize) * gridSize,
                Mathf.Round(value.y / gridSize) * gridSize,
                0f);
        }

        private Vector3 Snap(Vector3 value)
        {
            return new Vector3(
                Mathf.Round(value.x / gridSize) * gridSize,
                Mathf.Round(value.y / gridSize) * gridSize,
                value.z);
        }
    }
}
