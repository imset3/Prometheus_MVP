using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Narthex.Presentation
{
    /// <summary>
    /// Dims only hidden-room environment art. Player, Theus and the beam live outside
    /// this renderer set, so they remain readable until the passkey restores the room.
    /// </summary>
    public sealed class TutorialHiddenRoomLightingHost : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField] private Tilemap[] tilemaps = Array.Empty<Tilemap>();
        [SerializeField] private Color darknessTint = new(0.08f, 0.11f, 0.14f, 1f);

        private Color[] spriteColors = Array.Empty<Color>();
        private Color[] tilemapColors = Array.Empty<Color>();

        public bool IsDark { get; private set; }
        public bool HasValidSetup => spriteRenderers != null && tilemaps != null &&
                                     spriteRenderers.Length + tilemaps.Length > 0;

        private void Awake()
        {
            CacheOriginalColors();
        }

        private void CacheOriginalColors()
        {
            spriteColors = new Color[spriteRenderers.Length];
            for (var index = 0; index < spriteRenderers.Length; index++)
                spriteColors[index] = spriteRenderers[index] != null ? spriteRenderers[index].color : Color.white;
            tilemapColors = new Color[tilemaps.Length];
            for (var index = 0; index < tilemaps.Length; index++)
                tilemapColors[index] = tilemaps[index] != null ? tilemaps[index].color : Color.white;
        }

        public void SetDark(bool dark)
        {
            // The chapter flow can initialize before this inactive room receives
            // Awake. Cache lazily so script execution order cannot break startup.
            if (spriteColors.Length != spriteRenderers.Length || tilemapColors.Length != tilemaps.Length)
                CacheOriginalColors();
            IsDark = dark;
            for (var index = 0; index < spriteRenderers.Length; index++)
            {
                var renderer = spriteRenderers[index];
                if (renderer == null) continue;
                renderer.color = dark ? Multiply(spriteColors[index], darknessTint) : spriteColors[index];
            }

            for (var index = 0; index < tilemaps.Length; index++)
            {
                var tilemap = tilemaps[index];
                if (tilemap == null) continue;
                tilemap.color = dark ? Multiply(tilemapColors[index], darknessTint) : tilemapColors[index];
            }
        }

        private static Color Multiply(Color source, Color tint) => new(
            source.r * tint.r,
            source.g * tint.g,
            source.b * tint.b,
            source.a);
    }
}
