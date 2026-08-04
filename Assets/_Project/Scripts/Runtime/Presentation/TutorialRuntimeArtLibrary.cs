using UnityEngine;

namespace Narthex.Presentation
{
    public static class TutorialRuntimeArtLibrary
    {
        public const string Theus = "TutorialArt/Theus";
        public const string FriendA = "TutorialArt/FriendA";
        public const string FriendC = "TutorialArt/FriendC";
        public const string Cryon = "TutorialArt/Cryon";
        public const string Prome = "TutorialArt/Prome";

        public static Sprite LoadSprite(string resourcePath)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null) return sprite;

            var sprites = Resources.LoadAll<Sprite>(resourcePath);
            return sprites != null && sprites.Length > 0 ? sprites[0] : null;
        }
    }
}
