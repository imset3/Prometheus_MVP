using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    public static class PrometheusComponentAutomation
    {
        public static List<PrometheusAiRecord> Inspect(
            Scene scene,
            string markerId,
            string hierarchyPath,
            string objectId,
            string componentType)
        {
            var target = RequireTarget(scene, markerId, hierarchyPath, objectId);
            var component = RequireComponent(target, componentType);
            var records = new List<PrometheusAiRecord>();
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            if (!property.NextVisible(true)) return records;
            do
            {
                records.Add(new PrometheusAiRecord
                {
                    id = property.propertyPath,
                    kind = property.propertyType.ToString(),
                    hierarchyPath = PrometheusSceneQuery.Path(target),
                    value = ReadValue(property)
                });
            } while (property.NextVisible(false));
            return records;
        }

        public static PrometheusAiChange Set(
            Scene scene,
            string markerId,
            string hierarchyPath,
            string objectId,
            string componentType,
            string propertyPath,
            string value,
            bool dryRun)
        {
            var target = RequireTarget(scene, markerId, hierarchyPath, objectId);
            var component = RequireComponent(target, componentType);
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(propertyPath);
            if (property == null)
                throw new InvalidOperationException(
                    $"Serialized property '{propertyPath}' was not found on {component.GetType().FullName}.");
            var before = ReadValue(property);
            ValidateAndWrite(property, value, true);
            var change = new PrometheusAiChange
            {
                action = "set-component-property",
                objectId = PrometheusSceneQuery.ObjectId(target),
                hierarchyPath = PrometheusSceneQuery.Path(target),
                before = $"{component.GetType().FullName}.{propertyPath}={before}",
                after = $"{component.GetType().FullName}.{propertyPath}={value}"
            };
            if (dryRun) return change;

            Undo.RecordObject(component, "AI set serialized property");
            ValidateAndWrite(property, value, false);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(scene);
            return change;
        }

        public static PrometheusAiChange SetActive(
            Scene scene,
            string markerId,
            string hierarchyPath,
            string objectId,
            bool active,
            bool dryRun)
        {
            var target = RequireTarget(scene, markerId, hierarchyPath, objectId);
            var change = new PrometheusAiChange
            {
                action = "set-active",
                objectId = PrometheusSceneQuery.ObjectId(target),
                hierarchyPath = PrometheusSceneQuery.Path(target),
                before = target.activeSelf.ToString(),
                after = active.ToString()
            };
            if (dryRun) return change;
            Undo.RecordObject(target, "AI set GameObject active");
            target.SetActive(active);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(scene);
            return change;
        }

        public static PrometheusAiChange SetTransform(
            Scene scene,
            string markerId,
            string hierarchyPath,
            string objectId,
            Vector3 position,
            float rotationZ,
            Vector3? scale,
            bool dryRun)
        {
            var target = RequireTarget(scene, markerId, hierarchyPath, objectId);
            var before =
                $"position={target.transform.position}; rotationZ={target.transform.eulerAngles.z}; scale={target.transform.localScale}";
            var resolvedScale = scale ?? target.transform.localScale;
            var after = $"position={position}; rotationZ={rotationZ}; scale={resolvedScale}";
            var change = new PrometheusAiChange
            {
                action = "set-transform",
                objectId = PrometheusSceneQuery.ObjectId(target),
                hierarchyPath = PrometheusSceneQuery.Path(target),
                before = before,
                after = after
            };
            if (dryRun) return change;
            Undo.RecordObject(target.transform, "AI set Transform");
            target.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotationZ));
            target.transform.localScale = resolvedScale;
            EditorUtility.SetDirty(target.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            return change;
        }

        public static List<PrometheusAiRecord> FindCodeUsage(Scene scene, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("typeName is required.");
            var records = new List<PrometheusAiRecord>();
            foreach (var gameObject in PrometheusSceneQuery.All(scene))
            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (component == null) continue;
                var type = component.GetType();
                if (!string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(type.FullName, typeName, StringComparison.OrdinalIgnoreCase))
                    continue;
                records.Add(new PrometheusAiRecord
                {
                    id = PrometheusSceneQuery.ObjectId(gameObject),
                    kind = type.FullName,
                    hierarchyPath = PrometheusSceneQuery.Path(gameObject),
                    value = component is MonoBehaviour behaviour
                        ? AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(behaviour))
                        : string.Empty
                });
            }
            return records;
        }

        private static GameObject RequireTarget(
            Scene scene,
            string markerId,
            string hierarchyPath,
            string objectId) =>
            PrometheusSceneQuery.Resolve(scene, markerId, hierarchyPath, objectId) ??
            throw new InvalidOperationException("Target object was not found or was ambiguous.");

        private static Component RequireComponent(GameObject target, string componentType)
        {
            if (string.IsNullOrWhiteSpace(componentType))
                throw new ArgumentException("componentType is required.");
            var matches = target.GetComponents<Component>()
                .Where(component => component != null)
                .Where(component =>
                    string.Equals(component.GetType().Name, componentType, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(component.GetType().FullName, componentType, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return matches.Length switch
            {
                1 => matches[0],
                0 => throw new InvalidOperationException(
                    $"Component '{componentType}' was not found on {PrometheusSceneQuery.Path(target)}."),
                _ => throw new InvalidOperationException(
                    $"Component '{componentType}' is ambiguous on {PrometheusSceneQuery.Path(target)}.")
            };
        }

        private static string ReadValue(SerializedProperty property) =>
            property.propertyType switch
            {
                SerializedPropertyType.Integer => property.longValue.ToString(CultureInfo.InvariantCulture),
                SerializedPropertyType.Boolean => property.boolValue.ToString(),
                SerializedPropertyType.Float => property.doubleValue.ToString(CultureInfo.InvariantCulture),
                SerializedPropertyType.String => property.stringValue,
                SerializedPropertyType.Enum => property.enumNames[property.enumValueIndex],
                SerializedPropertyType.Vector2 => property.vector2Value.ToString("R"),
                SerializedPropertyType.Vector3 => property.vector3Value.ToString("R"),
                SerializedPropertyType.ObjectReference => property.objectReferenceValue != null
                    ? GlobalObjectId.GetGlobalObjectIdSlow(property.objectReferenceValue).ToString()
                    : "null",
                _ => property.hasVisibleChildren ? "<complex>" : property.ToString()
            };

        private static void ValidateAndWrite(SerializedProperty property, string value, bool validateOnly)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                        throw new FormatException($"'{value}' is not an integer.");
                    if (!validateOnly) property.longValue = integer;
                    break;
                case SerializedPropertyType.Boolean:
                    if (!bool.TryParse(value, out var boolean))
                        throw new FormatException($"'{value}' is not a boolean.");
                    if (!validateOnly) property.boolValue = boolean;
                    break;
                case SerializedPropertyType.Float:
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                        throw new FormatException($"'{value}' is not a number.");
                    if (!validateOnly) property.doubleValue = number;
                    break;
                case SerializedPropertyType.String:
                    if (!validateOnly) property.stringValue = value ?? string.Empty;
                    break;
                case SerializedPropertyType.Enum:
                    var index = Array.FindIndex(property.enumNames,
                        item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
                    if (index < 0) throw new FormatException(
                        $"'{value}' is not one of: {string.Join(", ", property.enumNames)}");
                    if (!validateOnly) property.enumValueIndex = index;
                    break;
                case SerializedPropertyType.ObjectReference:
                    UnityEngine.Object reference = null;
                    if (!string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!GlobalObjectId.TryParse(value, out var id))
                            throw new FormatException("Object reference must be 'null' or a GlobalObjectId.");
                        reference = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                        if (reference == null) throw new InvalidOperationException("Object reference could not be resolved.");
                    }
                    if (!validateOnly) property.objectReferenceValue = reference;
                    break;
                case SerializedPropertyType.Vector2:
                    var vector2 = ParseVector(value, 2);
                    if (!validateOnly) property.vector2Value = new Vector2(vector2[0], vector2[1]);
                    break;
                case SerializedPropertyType.Vector3:
                    var vector3 = ParseVector(value, 3);
                    if (!validateOnly) property.vector3Value = new Vector3(vector3[0], vector3[1], vector3[2]);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Property type '{property.propertyType}' is read-only through AI commands.");
            }
        }

        private static float[] ParseVector(string value, int expectedCount)
        {
            var parts = (value ?? string.Empty)
                .Trim()
                .Trim('(', ')', '[', ']')
                .Split(',');
            if (parts.Length != expectedCount)
                throw new FormatException($"Vector requires {expectedCount} comma-separated values.");
            var result = new float[expectedCount];
            for (var index = 0; index < expectedCount; index++)
                if (!float.TryParse(parts[index].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out result[index]))
                    throw new FormatException($"'{parts[index]}' is not a valid vector value.");
            return result;
        }
    }
}
