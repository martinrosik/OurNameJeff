using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// Settings: sound on or off. The device remembers it (PlayerPrefs) and it applies to every scene through the
    /// global listener volume, so sounds added later follow it without extra code.
    /// </summary>
    public static class SoundSettings
    {
        internal const string MutedKey = "ResortEmpire.Muted";

        public static bool IsMuted => PlayerPrefs.GetInt(MutedKey, 0) == 1;

        public static void SetMuted(bool muted)
        {
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Puts the remembered setting on the listener; runs before the first scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Apply() => AudioListener.volume = IsMuted ? 0f : 1f;
    }
}
