using System;
using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// Runs the Core game in the scene: creates the <see cref="ResortGame"/> from the config and ticks it every
    /// frame. Views and UI read <see cref="Game"/>; they never change rules themselves.
    /// Pausing when the app goes to the background (WAVE_PAUSED) comes in a later sprint (docs/tasks/later.md).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        [Tooltip("Speeds the simulation up for testing. 1 = real time.")]
        [SerializeField, Range(0f, 8f)] private float simulationSpeed = 1f;

        public GameConfig Config => config;
        public ResortGame Game { get; private set; }

        public float SimulationSpeed
        {
            get => simulationSpeed;
            set => simulationSpeed = Mathf.Clamp(value, 0f, 8f);
        }

        /// <summary>Raised when a new game replaces the current one.</summary>
        public event Action<ResortGame> GameCreated;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("GameController has no GameConfig. Assign Assets/_Project/Data/Config_Prototype.asset.", this);
                enabled = false;
                return;
            }

            Application.targetFrameRate = 60;
            NewGame();
        }

        private void Update()
        {
            Game?.Tick(Time.deltaTime * simulationSpeed);
        }

        /// <summary>Starts over from the config's starting state.</summary>
        public void NewGame()
        {
            Game = new ResortGame(config);
            GameCreated?.Invoke(Game);
        }
    }
}
