using System;
using System.Collections.Generic;
using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// Runs the Core game in the scene: creates the <see cref="ResortGame"/> from the config and ticks it every
    /// frame. Views and UI read <see cref="Game"/>; they never change rules themselves. In the Game scene it first
    /// picks up a game the player left for the tutorial (<see cref="ParkedGame"/>), if there is one.
    /// Pausing when the app goes to the background (WAVE_PAUSED) comes in a later sprint (docs/tasks/later.md).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        [Tooltip("Speeds the simulation up for testing. 1 = real time.")]
        [SerializeField, Range(0f, 8f)] private float simulationSpeed = 1f;

        public ResortGame Game { get; private set; }

        public float SimulationSpeed
        {
            get => simulationSpeed;
            set => simulationSpeed = Mathf.Clamp(value, 0f, 8f);
        }

        /// <summary>
        /// True when this scene picked up the game the player left for the tutorial (<see cref="ParkedGame"/>) instead of
        /// starting a new one. It may be in the middle of a wave.
        /// </summary>
        public bool IsResumed { get; private set; }

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

            // A broken config would crash or freeze the game later, far from the cause, so it stops here with every problem listed.
            List<string> errors = GameConfigValidator.Validate(config);
            if (errors.Count > 0)
            {
                Debug.LogError($"GameConfig {config.name} has problems, so the game doesn't start:\n- {string.Join("\n- ", errors)}", this);
                enabled = false;
                return;
            }

            Application.targetFrameRate = 60;

            // Back from the tutorial: the player carries on with the game they left, not a new one.
            ResortGame parked = gameObject.scene.name == GameScenes.Game ? ParkedGame.Take() : null;
            if (parked != null)
            {
                Game = parked;
                IsResumed = true;
                return;
            }

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
            IsResumed = false;
            GameCreated?.Invoke(Game);
        }

        /// <summary>
        /// Starts over on another config, e.g. the tutorial's practice wave. A config with problems is refused (logged),
        /// and the current game goes on.
        /// </summary>
        public bool NewGame(GameConfig newConfig)
        {
            List<string> errors = GameConfigValidator.Validate(newConfig);
            if (errors.Count > 0)
            {
                Debug.LogError($"GameConfig {(newConfig != null ? newConfig.name : "(none)")} has problems, so the game doesn't switch to it:\n- {string.Join("\n- ", errors)}", this);
                return false;
            }

            config = newConfig;
            NewGame();
            return true;
        }
    }
}
