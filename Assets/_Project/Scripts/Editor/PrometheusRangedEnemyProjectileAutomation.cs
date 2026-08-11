#if UNITY_EDITOR
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
    public static class PrometheusRangedEnemyProjectileAutomation
    {
        private const string RequiredScene = "Assets/Scenes/TutorialScene.unity";
        private const string ProjectileSpritePath =
            "Assets/_Project/Art/AIConcepts/TutorialPlayerVFX/ReviewBatch_v1/Generated/TUTO_VFX_RangedProjectile_v1.png";
        private const string UnlitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const int ProjectileSortingOrder = 180;

        public static List<PrometheusAiChange> Apply(Scene scene, bool dryRun)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("A loaded scene is required.");
            if (!string.Equals(scene.path, RequiredScene, StringComparison.Ordinal))
                throw new InvalidOperationException($"Ranged projectile art requires '{RequiredScene}', not '{scene.path}'.");

            var sprite = AssetDatabase.LoadAllAssetsAtPath(ProjectileSpritePath)
                .OfType<Sprite>()
                .FirstOrDefault();
            if (sprite == null)
                throw new InvalidOperationException("Ranged projectile sprite sub-asset is missing: " + ProjectileSpritePath);
            var unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (unlitMaterial == null)
                throw new InvalidOperationException("URP unlit sprite material is missing: " + UnlitMaterialPath);

            var changes = new List<PrometheusAiChange>();
            var projectiles = Resources.FindObjectsOfTypeAll<TutorialEnemyProjectileHost>()
                .Where(item => item != null && item.gameObject.scene == scene)
                .OrderBy(item => PrometheusSceneQuery.Path(item.gameObject), StringComparer.Ordinal)
                .ToArray();
            if (projectiles.Length == 0)
                throw new InvalidOperationException("No tutorial ranged-enemy projectiles were found in the active scene.");

            foreach (var projectile in projectiles)
            {
                var renderer = projectile.GetComponentInChildren<SpriteRenderer>(true);
                if (renderer == null)
                    throw new InvalidOperationException("Projectile SpriteRenderer is missing: " +
                                                        PrometheusSceneQuery.Path(projectile.gameObject));

                var alreadyVisible = renderer.sprite == sprite && renderer.enabled &&
                                     renderer.color.a > 0.99f &&
                                     renderer.sortingOrder == ProjectileSortingOrder &&
                                     renderer.sharedMaterial == unlitMaterial;
                if (alreadyVisible) continue;

                changes.Add(new PrometheusAiChange
                {
                    action = "apply-ranged-enemy-projectile-art",
                    objectId = PrometheusSceneQuery.ObjectId(projectile.gameObject),
                    hierarchyPath = PrometheusSceneQuery.Path(projectile.gameObject),
                    before = $"sprite={(renderer.sprite != null ? renderer.sprite.name : "null")}; " +
                             $"enabled={renderer.enabled}; alpha={renderer.color.a:0.##}; " +
                             $"sortingOrder={renderer.sortingOrder}; material=" +
                             (renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "null"),
                    after = $"sprite={sprite.name}; enabled=true; alpha=1; " +
                            $"sortingOrder={ProjectileSortingOrder}; material={unlitMaterial.name}"
                });
                if (dryRun) continue;

                Undo.RecordObject(renderer, "Apply Ranged Enemy Projectile Art");
                renderer.sprite = sprite;
                renderer.sharedMaterial = unlitMaterial;
                renderer.color = Color.white;
                renderer.enabled = true;
                renderer.sortingLayerName = "Default";
                renderer.sortingOrder = ProjectileSortingOrder;
                EditorUtility.SetDirty(renderer);
            }

            if (!dryRun && changes.Count > 0)
                EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }
    }
}
#endif
