using Narthex.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace Narthex.Tests
{
    public sealed class TutorialRangedEnemyTests
    {
        [Test]
        public void Configure_RequiresCompleteEditableSceneWiring()
        {
            var enemy = new GameObject("RangedEnemyTest");
            var target = new GameObject("Target");
            var warning = new GameObject("Warning");
            var projectileObject = new GameObject("Projectile");
            try
            {
                warning.transform.SetParent(enemy.transform);
                projectileObject.transform.SetParent(enemy.transform);
                var actor = enemy.AddComponent<CombatActorHost>();
                var body = enemy.AddComponent<BoxCollider2D>();
                var muzzle = new GameObject("RangedMuzzle_EDITABLE").transform;
                muzzle.SetParent(enemy.transform);
                var warningRenderer = warning.AddComponent<SpriteRenderer>();

                var projectileCollider = projectileObject.AddComponent<CircleCollider2D>();
                projectileCollider.isTrigger = true;
                var projectileBody = projectileObject.AddComponent<Rigidbody2D>();
                projectileBody.bodyType = RigidbodyType2D.Kinematic;
                var projectileRenderer = projectileObject.AddComponent<SpriteRenderer>();
                var projectile = projectileObject.AddComponent<TutorialEnemyProjectileHost>();
                projectile.Configure(projectileCollider, projectileBody, projectileRenderer);

                var ranged = enemy.AddComponent<TutorialRangedEnemyHost>();
                Assert.That(ranged.HasValidSetup, Is.False);
                ranged.Configure(
                    actor,
                    target.transform,
                    body,
                    muzzle,
                    warning,
                    warningRenderer,
                    new[] { projectile });

                Assert.That(projectile.HasValidSetup, Is.True);
                Assert.That(projectile.HasVisibleSetup, Is.False,
                    "A projectile with no sprite must not be considered visually ready.");
                var texture = new Texture2D(2, 2);
                var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2f);
                projectileRenderer.sprite = sprite;
                Assert.That(projectile.HasVisibleSetup, Is.True);
                Assert.That(ranged.HasValidSetup, Is.True);

                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(target);
            }
        }
    }
}
