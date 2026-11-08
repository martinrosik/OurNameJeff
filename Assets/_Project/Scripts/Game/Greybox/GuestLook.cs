using System;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// One guest character's walk cycle (baked from the Kenney Mini Characters by GuestSpriteBaker).
    /// <see cref="walkFront"/> walks down-right on screen, <see cref="walkBack"/> up-right; left is the same frames
    /// flipped.
    /// </summary>
    [Serializable]
    public sealed class GuestLook
    {
        public string name;
        public Sprite[] walkFront = Array.Empty<Sprite>();
        public Sprite[] walkBack = Array.Empty<Sprite>();

        public bool IsValid => walkFront != null && walkFront.Length > 0 && walkBack != null && walkBack.Length > 0;
    }
}
