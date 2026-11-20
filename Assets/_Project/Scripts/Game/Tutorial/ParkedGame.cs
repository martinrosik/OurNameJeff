using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// The game the player left for the tutorial (PLAY TUTORIAL AGAIN in the settings). It waits here untouched, frozen
    /// like WAVE_PAUSED in the Build doc (nothing progresses, nothing gets worse), and the Game scene's
    /// <see cref="GameController"/> picks it up again, so the player carries on exactly where they left off, also in
    /// the middle of a wave. Kept in memory only: saving to disk comes with save/load (roadmap).
    /// </summary>
    public static class ParkedGame
    {
        private static ResortGame _game;

        /// <summary>Whether a game waits for the player to come back from the tutorial.</summary>
        public static bool HasGame => _game != null;

        public static void Park(ResortGame game) => _game = game;

        /// <summary>The waiting game, or null. It's handed out once.</summary>
        public static ResortGame Take()
        {
            ResortGame game = _game;
            _game = null;
            return game;
        }

        internal static void Clear() => _game = null;

        // Play mode starts clean, also with domain reload turned off in the Editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => _game = null;
    }
}
