using System;
using System.Collections.Generic;
using System.Linq;
using Narthex.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Narthex.Tools
{
    /// <summary>Applies the reusable grounded-physics contract to the seven authored F/G enemies.</summary>
    public static class PrometheusEnemyPhysicsAutomation
    {
        private static readonly string[] EnemyNames =
        {
            "ExteriorA_Enemy_01_ART_SLOT",
            "ExteriorA_Enemy_02_ART_SLOT",
            "ExteriorA_Enemy_03_ART_SLOT",
            "ExteriorB_Enemy_01_ART_SLOT",
            "ExteriorB_Enemy_02_ART_SLOT",
            "ExteriorB_Enemy_03_ART_SLOT",
            "ExteriorB_Enemy_04_ART_SLOT"
        };

        private static readonly HashSet<string> RangedEnemyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ExteriorA_Enemy_03_ART_SLOT",
            "ExteriorB_Enemy_02_ART_SLOT",
            "ExteriorB_Enemy_04_ART_SLOT"
        };

        public static IReadOnlyList<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            var changes = new List<PrometheusAiChange>();
            foreach (var enemyName in EnemyNames)
            {
                var matches = PrometheusSceneQuery.All(scene)
                    .Where(item => string.Equals(item.name, enemyName, StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length != 1)
                    throw new InvalidOperationException($"Expected one '{enemyName}', found {matches.Length}.");

                var enemy = matches[0];
                changes.Add(new PrometheusAiChange
                {
                    action = "apply-grounded-enemy-physics",
                    objectId = PrometheusSceneQuery.ObjectId(enemy),
                    hierarchyPath = PrometheusSceneQuery.Path(enemy),
                    before = Describe(enemy),
                    after = RangedEnemyNames.Contains(enemy.name)
                        ? "Dynamic Rigidbody2D; gravity=3; FreezeRotation; Interpolate; Continuous; solid body offsetY=0.8; grounded motor"
                        : "Dynamic Rigidbody2D; gravity=3; FreezeRotation; Interpolate; Continuous; solid body; grounded motor"
                });
                if (!dryRun) ConfigureEnemy(enemy);
            }

            var marchRoot = PrometheusSceneQuery.All(scene)
                .FirstOrDefault(item => string.Equals(item.name, "외부_적진격연출", StringComparison.Ordinal));
            if (marchRoot != null)
            {
                changes.Add(new PrometheusAiChange
                {
                    action = "ensure-march-support-floor",
                    objectId = PrometheusSceneQuery.ObjectId(marchRoot),
                    hierarchyPath = PrometheusSceneQuery.Path(marchRoot) + "/행군연출_카메라밖_발판",
                    before = "missing or unverified",
                    after = "hidden support collider below the presentation soldiers"
                });
                if (!dryRun) ConfigureMarchFloor(marchRoot.transform);
            }

            if (!dryRun) EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        public static IReadOnlyList<string> Validate(Scene scene)
        {
            var issues = new List<string>();
            foreach (var enemyName in EnemyNames)
            {
                var enemy = PrometheusSceneQuery.All(scene)
                    .FirstOrDefault(item => string.Equals(item.name, enemyName, StringComparison.Ordinal));
                if (enemy == null)
                {
                    issues.Add($"Missing enemy: {enemyName}");
                    continue;
                }

                var body = enemy.GetComponent<Rigidbody2D>();
                var collider = enemy.GetComponent<Collider2D>();
                var motor = enemy.GetComponent<TutorialGroundedEnemyMotorHost>();
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic || body.gravityScale <= 0f ||
                    (body.constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
                    issues.Add($"{enemyName}: invalid Rigidbody2D grounded configuration.");
                if (collider == null || collider.isTrigger)
                    issues.Add($"{enemyName}: body collider must be solid.");
                if (RangedEnemyNames.Contains(enemyName) && collider is BoxCollider2D rangedCollider &&
                    !Mathf.Approximately(rangedCollider.offset.y, 0.8f))
                    issues.Add($"{enemyName}: ranged body collider offset must keep the sprite feet on the ground.");
                if (motor == null || !motor.HasValidSetup)
                    issues.Add($"{enemyName}: grounded motor is missing or invalid.");
            }
            return issues;
        }

        private static void ConfigureEnemy(GameObject enemy)
        {
            var collider = enemy.GetComponent<BoxCollider2D>();
            if (collider == null) collider = Undo.AddComponent<BoxCollider2D>(enemy);
            Undo.RecordObject(collider, "Configure solid enemy body");
            collider.enabled = true;
            collider.isTrigger = false;
            collider.offset = new Vector2(collider.offset.x, RangedEnemyNames.Contains(enemy.name) ? 0.8f : 0f);

            var body = enemy.GetComponent<Rigidbody2D>();
            if (body == null) body = Undo.AddComponent<Rigidbody2D>(enemy);
            Undo.RecordObject(body, "Configure grounded enemy body");
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 3f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.linearVelocity = Vector2.zero;

            var motor = enemy.GetComponent<TutorialGroundedEnemyMotorHost>();
            if (motor == null) motor = Undo.AddComponent<TutorialGroundedEnemyMotorHost>(enemy);
            Undo.RecordObject(motor, "Configure grounded enemy motor");
            motor.Configure(body, collider);
            EditorUtility.SetDirty(motor);

            BindReference(enemy.GetComponent<TutorialEnemyPursuitHost>(), "groundMotor", motor);
            var ranged = enemy.GetComponent<TutorialRangedEnemyHost>();
            BindReference(ranged, "groundMotor", motor);
            BindReference(ranged, "bodyCollider", collider);
            EditorUtility.SetDirty(enemy);
        }

        private static void ConfigureMarchFloor(Transform parent)
        {
            var child = parent.Find("행군연출_카메라밖_발판");
            if (child == null)
            {
                var created = new GameObject("행군연출_카메라밖_발판");
                Undo.RegisterCreatedObjectUndo(created, "Create march support floor");
                child = created.transform;
                child.SetParent(parent, false);
            }
            Undo.RecordObject(child, "Position march support floor");
            child.localPosition = new Vector3(0f, -0.72f, 0f);
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            child.gameObject.layer = 2;
            var collider = child.GetComponent<BoxCollider2D>();
            if (collider == null) collider = Undo.AddComponent<BoxCollider2D>(child.gameObject);
            Undo.RecordObject(collider, "Configure march support floor");
            collider.isTrigger = false;
            collider.size = new Vector2(38f, 0.45f);
            collider.offset = Vector2.zero;
        }

        private static void BindReference(Component component, string propertyName, UnityEngine.Object value)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException(
                $"{component.GetType().Name}.{propertyName} was not found.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }

        private static string Describe(GameObject enemy)
        {
            var body = enemy.GetComponent<Rigidbody2D>();
            var collider = enemy.GetComponent<Collider2D>();
            return $"body={(body == null ? "missing" : body.bodyType.ToString())}; " +
                   $"gravity={(body == null ? 0f : body.gravityScale):0.##}; " +
                   $"collider={(collider == null ? "missing" : collider.isTrigger ? "trigger" : "solid")}; " +
                   $"motor={(enemy.GetComponent<TutorialGroundedEnemyMotorHost>() == null ? "missing" : "present")}";
        }
    }
}
