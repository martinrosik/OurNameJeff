using System;

namespace ResortEmpire.Core
{
    /// <summary>The tutorial's steps, in the order the player meets them (see <see cref="TutorialScript"/>).</summary>
    public enum TutorialStep
    {
        /// <summary>Guests come in waves; Workers go where they're needed.</summary>
        Welcome = 0,

        /// <summary>Each building serves one need; the squares are seats.</summary>
        Buildings = 1,

        /// <summary>The Worker badge: full speed with a Worker, about 3× slower without.</summary>
        Workers = 2,

        /// <summary>The forecast of the next wave.</summary>
        Forecast = 3,

        /// <summary>The player moves a Worker before the wave. Only the building without a Worker opens.</summary>
        MoveWorker = 4,

        /// <summary>The player taps Welcome Guests. No building opens meanwhile.</summary>
        WelcomeGuests = 5,

        /// <summary>The boat sails in (WAVE_INCOMING).</summary>
        BoatArriving = 6,

        /// <summary>The Wave Needs Panel. The wave waits while the player reads.</summary>
        NeedsPanel = 7,

        /// <summary>The wave runs until the building without a Worker falls behind (BOTTLENECK).</summary>
        WatchQueues = 8,

        /// <summary>The wave waits until the player moves a Worker to the building that fell behind. Only that building opens.</summary>
        Overload = 9,

        /// <summary>The building is catching up (RECOVERING). The wave waits while the player reads.</summary>
        CatchingUp = 10,

        /// <summary>The wave runs to its end.</summary>
        FinishWave = 11,

        /// <summary>Wave Complete: stars, then the button that starts the real game.</summary>
        Result = 12,

        /// <summary>The player left the tutorial for the real game.</summary>
        Finished = 13,
    }

    /// <summary>How a step waits for the player.</summary>
    public enum TutorialStepKind
    {
        /// <summary>Text with a Next button; nothing else on the screen reacts until the player taps it.</summary>
        Info = 0,

        /// <summary>Text that points at something to tap; the step ends when the player has done it.</summary>
        Action = 1,

        /// <summary>A short note while the game runs on; the step ends by itself.</summary>
        Hint = 2,

        /// <summary>The last step; its button leaves the tutorial.</summary>
        Final = 3,

        /// <summary>The tutorial is over.</summary>
        None = 4,
    }

    /// <summary>
    /// The tutorial's script in plain C#: which step is showing, what each step waits for, and when the wave waits
    /// for the player. It reads the game and reacts to the player's moves; it never changes the rules. The practice
    /// wave comes from <see cref="TutorialConfig"/>, and the UI (TutorialController) shows the steps as bubbles.
    /// <para>
    /// Steps follow the game, not a clock: starting the wave early skips the steps before it, and a wave that ends
    /// before any building falls behind goes straight to the result.
    /// </para>
    /// </summary>
    public sealed class TutorialScript : IDisposable
    {
        private readonly ResortGame _game;
        private bool _workerMovedThisStep;

        public TutorialScript(ResortGame game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _game.WorkerMoved += OnWorkerMoved;
            Step = TutorialStep.Welcome;
        }

        public TutorialStep Step { get; private set; }

        public TutorialStepKind Kind => GetKind(Step);

        public bool IsFinished => Step == TutorialStep.Finished;

        /// <summary>The building without a Worker that fell behind (from <see cref="TutorialStep.Overload"/> on).</summary>
        public BuildingId? OverloadedBuilding { get; private set; }

        /// <summary>
        /// While true the wave waits: the player reads, or fixes the overloaded building, without the queues
        /// growing meanwhile (calm decisions, no reflexes). Only steps during the wave pause it.
        /// </summary>
        public bool PausesGame =>
            Step == TutorialStep.NeedsPanel || Step == TutorialStep.Overload || Step == TutorialStep.CatchingUp;

        /// <summary>
        /// The building the step is about: the first one without a Worker while the player moves a Worker
        /// (<see cref="TutorialStep.MoveWorker"/>), the one that fell behind while the player fixes it and reads about it
        /// (<see cref="TutorialStep.Overload"/>, <see cref="TutorialStep.CatchingUp"/>). Null on other steps.
        /// </summary>
        public BuildingId? FocusBuilding
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.MoveWorker:
                        return FirstBuildingWithoutWorker();
                    case TutorialStep.Overload:
                    case TutorialStep.CatchingUp:
                        return OverloadedBuilding;
                    default:
                        return null;
                }
            }
        }

        /// <summary>
        /// Whether a tap may open the Worker sheet of <paramref name="building"/>. While a step asks for a Worker move,
        /// only the building it points at (<see cref="FocusBuilding"/>) opens, so the player learns the plain way: tap the
        /// building without a Worker and take one over. While it asks for Welcome Guests, no building opens. Other steps
        /// don't limit it.
        /// </summary>
        public bool AllowsWorkerSheet(BuildingId building)
        {
            switch (Step)
            {
                case TutorialStep.MoveWorker:
                case TutorialStep.Overload:
                    BuildingId? focus = FocusBuilding;
                    return focus == null || focus.Value == building;
                case TutorialStep.WelcomeGuests:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Whether Welcome Guests may start the wave: not while the tutorial waits for the Worker move before it.</summary>
        public bool AllowsWaveStart => Step != TutorialStep.MoveWorker;

        /// <summary>Raised with the new step whenever the step changes.</summary>
        public event Action<TutorialStep> StepChanged;

        public static TutorialStepKind GetKind(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Welcome:
                case TutorialStep.Buildings:
                case TutorialStep.Workers:
                case TutorialStep.Forecast:
                case TutorialStep.NeedsPanel:
                case TutorialStep.CatchingUp:
                    return TutorialStepKind.Info;
                case TutorialStep.MoveWorker:
                case TutorialStep.WelcomeGuests:
                case TutorialStep.Overload:
                    return TutorialStepKind.Action;
                case TutorialStep.BoatArriving:
                case TutorialStep.WatchQueues:
                case TutorialStep.FinishWave:
                    return TutorialStepKind.Hint;
                case TutorialStep.Result:
                    return TutorialStepKind.Final;
                default:
                    return TutorialStepKind.None;
            }
        }

        /// <summary>The player tapped Next on an <see cref="TutorialStepKind.Info"/> step. False on any other step.</summary>
        public bool Next()
        {
            if (Kind != TutorialStepKind.Info) return false;

            GoTo(Step + 1);
            Update();
            return true;
        }

        /// <summary>The player tapped the last step's button. False before the last step.</summary>
        public bool Finish()
        {
            if (Step != TutorialStep.Result) return false;

            GoTo(TutorialStep.Finished);
            return true;
        }

        /// <summary>Checks the game and moves on through every step whose condition already holds. Call it every frame.</summary>
        public void Update()
        {
            while (TryAdvance())
            {
            }
        }

        public void Dispose() => _game.WorkerMoved -= OnWorkerMoved;

        private bool IsWaveOver => _game.Phase == GamePhase.WaveResolving || _game.Phase == GamePhase.WaveComplete;

        private bool TryAdvance()
        {
            switch (Step)
            {
                case TutorialStep.Welcome:
                case TutorialStep.Buildings:
                case TutorialStep.Workers:
                case TutorialStep.Forecast:
                    // Info steps before the wave wait for Next, unless the wave was started anyway.
                    return _game.Phase != GamePhase.AwaitingWave && GoTo(TutorialStep.WelcomeGuests);

                case TutorialStep.MoveWorker:
                    return (_workerMovedThisStep || _game.Phase != GamePhase.AwaitingWave) && GoTo(TutorialStep.WelcomeGuests);

                case TutorialStep.WelcomeGuests:
                    return _game.Phase != GamePhase.AwaitingWave && GoTo(TutorialStep.BoatArriving);

                case TutorialStep.BoatArriving:
                    return _game.Phase != GamePhase.WaveIncoming && GoTo(TutorialStep.NeedsPanel);

                case TutorialStep.NeedsPanel:
                case TutorialStep.CatchingUp:
                    return IsWaveOver && GoTo(TutorialStep.FinishWave);

                case TutorialStep.WatchQueues:
                {
                    BuildingId? behind = FindBuildingFallingBehind();
                    if (behind != null)
                    {
                        OverloadedBuilding = behind;
                        return GoTo(TutorialStep.Overload);
                    }

                    return IsWaveOver && GoTo(TutorialStep.FinishWave);
                }

                case TutorialStep.Overload:
                {
                    BuildingRuntime building = OverloadedBuilding != null ? _game.GetBuilding(OverloadedBuilding.Value) : null;
                    if (building == null || building.HasWorker) return GoTo(TutorialStep.CatchingUp);
                    return IsWaveOver && GoTo(TutorialStep.FinishWave);
                }

                case TutorialStep.FinishWave:
                    return _game.Phase == GamePhase.WaveComplete && GoTo(TutorialStep.Result);

                default:
                    return false;
            }
        }

        /// <summary>
        /// A building without a Worker whose queue keeps growing (BOTTLENECK). A building that has its Worker and still
        /// falls behind is too small; moving a Worker can't fix that, so the tutorial doesn't point at it.
        /// </summary>
        private BuildingId? FindBuildingFallingBehind()
        {
            foreach (BuildingRuntime building in _game.Buildings)
            {
                BuildingId id = building.Definition.building;
                if (!building.HasWorker && _game.GetBuildingState(id) == NeedState.Bottleneck) return id;
            }

            return null;
        }

        private BuildingId? FirstBuildingWithoutWorker()
        {
            foreach (BuildingRuntime building in _game.Buildings)
            {
                if (!building.HasWorker) return building.Definition.building;
            }

            return null;
        }

        private bool GoTo(TutorialStep step)
        {
            if (Step == step) return false;

            Step = step;
            _workerMovedThisStep = false;
            StepChanged?.Invoke(step);
            return true;
        }

        private void OnWorkerMoved(BuildingId from, BuildingId to)
        {
            if (Step == TutorialStep.MoveWorker) _workerMovedThisStep = true;
        }
    }
}
