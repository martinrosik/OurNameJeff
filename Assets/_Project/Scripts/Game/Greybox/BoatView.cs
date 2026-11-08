using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// The guest boat (US3): sails in to the dock during WAVE_INCOMING, stays during the wave,
    /// and is gone between waves.
    /// </summary>
    public sealed class BoatView : MonoBehaviour
    {
        [SerializeField] private Vector3 seaPosition = new Vector3(-3.4f, -7.5f, 0f);
        [SerializeField] private Vector3 dockPosition = new Vector3(-3.4f, -4.4f, 0f);
        [SerializeField] private GameObject visual;

        public void Refresh(ResortGame game)
        {
            switch (game.Phase)
            {
                case GamePhase.WaveIncoming:
                    float duration = Mathf.Max(0.01f, game.Config.wave.incomingDuration);
                    float t = 1f - Mathf.Clamp01((float)game.IncomingRemaining / duration);
                    Show(Vector3.Lerp(seaPosition, dockPosition, Mathf.SmoothStep(0f, 1f, t)));
                    break;
                case GamePhase.WaveActive:
                case GamePhase.WavePaused:
                case GamePhase.WaveResolving:
                case GamePhase.WaveComplete:
                    Show(dockPosition);
                    break;
                default:
                    visual.SetActive(false);
                    break;
            }
        }

        private void Show(Vector3 position)
        {
            visual.SetActive(true);
            transform.localPosition = position;
        }
    }
}
