using System;
using System.Collections.Generic;
using ResortEmpire.Core;
using UnityEngine;

namespace ResortEmpire.Game
{
    /// <summary>
    /// Guests walking on the island, so the wave is visible (OUR-34 arrivals).
    /// <para>
    /// The wave's need units are shared out among its guests by <see cref="GuestPlan"/> (wave 1: 12 guests carry
    /// Rooms 8, Drinks 8, Food 6), so one guest may go to the Beach Bar and then to the Reception. A guest steps
    /// off the boat when the Core releases its first unit, walks to that building and goes in. It walks on to its
    /// next building only once it has been <b>served</b> there and the Core has released its next unit. Served at
    /// its last building, it walks back to the boat and boards. When every guest has boarded, the wave ends
    /// (<see cref="AllBoarded"/>, OUR-35).
    /// </para>
    /// <para>
    /// <b>Service (OUR-46 / OUR-47):</b> a guest counts as served when the Core's <see cref="NeedStatus.Served"/>
    /// for that need has passed its unit (units are served first come, first served). A guest also stays inside at
    /// least the building's service time (<see cref="ResortGame.GetServiceSeconds"/>: 12 s with a Worker, about 36 s
    /// without), since the walk can make it arrive after the Core has already served its unit.
    /// </para>
    /// <para>
    /// A building's seat squares show the guests who are inside it right now: a square fills when a guest walks in
    /// and empties when it walks out. Guests beyond the building's seats show as small queue squares. They follow
    /// the walking guests, not the Core's seats: the Core seats a unit the moment it arrives, while the guest still
    /// has to walk over, so its seat count would fill squares before anyone is there.
    /// </para>
    /// </summary>
    public sealed class GuestCrowdView : MonoBehaviour
    {
        [Tooltip("Where guests step off the boat (local to the island).")]
        [SerializeField] private Vector3 dockPosition;

        [Tooltip("Open space in the middle of the island that every path passes through.")]
        [SerializeField] private Vector3 plazaPosition;

        [Tooltip("Where a guest walks into a building, relative to it, for buildings without a door setup.")]
        [SerializeField] private Vector3 entranceOffset = new Vector3(0f, -1.1f, 0f);

        [Tooltip("Per building: where its door is, so guests never cover its seats or walk onto the dock.")]
        [SerializeField] private DoorSetup[] doorSetups = Array.Empty<DoorSetup>();

        [Tooltip("Guest characters to pick from.")]
        [SerializeField] private GuestLook[] looks = Array.Empty<GuestLook>();

        [Tooltip("Walking speed in world units per second.")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 1.3f;

        [Tooltip("Walk cycle frames per second.")]
        [SerializeField, Min(1f)] private float frameRate = 12f;

        [Tooltip("Height of a guest on screen, in world units.")]
        [SerializeField, Min(0.1f)] private float guestHeight = 0.45f;

        [Tooltip("Seconds between guests leaving the boat when several arrive at once.")]
        [SerializeField, Min(0f)] private float spawnSpacing = 0.35f;

        [SerializeField] private int sortingOrder = 7;

        private const float FadeSeconds = 0.35f;
        private const float PathSpread = 0.14f;
        private const float StepSeconds = 0.05f;

        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly Dictionary<NeedId, Building> _buildings = new Dictionary<NeedId, Building>();
        private readonly System.Random _random = new System.Random(34);
        private WaveSimulation _wave;
        private GuestPlan _plan;
        private int _nextGuest;
        private float _nextSpawnDelay;

        /// <summary>The current wave's guests and the buildings each one visits.</summary>
        public GuestPlan Plan => _plan;

        /// <summary>Guests who have stepped off the boat this wave.</summary>
        public int SpawnedCount => _nextGuest;

        /// <summary>
        /// Every guest of the wave has stepped off, made its visits and boarded the boat again. With
        /// WAVE_RESOLVING this ends the wave (<see cref="IslandView"/> calls <see cref="ResortGame.FinishResolving"/>).
        /// </summary>
        public bool AllBoarded => _plan != null && _nextGuest >= _plan.Guests.Count && _walkers.Count == 0;

        /// <summary>Guests inside a building, waiting to be served before they walk on.</summary>
        public int InsideCount => _walkers.Count - WalkingCount;

        /// <summary>Guests visible on the island (walking, or fading at a door).</summary>
        public int WalkingCount
        {
            get
            {
                int count = 0;
                foreach (Walker walker in _walkers)
                {
                    if (walker.State != WalkerState.Inside) count++;
                }

                return count;
            }
        }

        /// <summary>Guests (of all that visited it) who have walked into the building of <paramref name="need"/>.</summary>
        public int GetDeliveredCount(NeedId need) => _buildings.TryGetValue(need, out Building b) ? b.Delivered : 0;

        /// <summary>Seat squares shown as taken: guests inside the building, up to its seats.</summary>
        public int GetShownSeated(NeedId need)
        {
            if (!_buildings.TryGetValue(need, out Building b)) return 0;
            return Mathf.Min(b.Inside, b.Seats);
        }

        /// <summary>Queue squares shown: guests inside beyond the building's seats.</summary>
        public int GetShownWaiting(NeedId need)
        {
            if (!_buildings.TryGetValue(need, out Building b)) return 0;
            return Mathf.Max(0, b.Inside - b.Seats);
        }

        /// <summary>Sends out guests whose first unit the Core has released. Called by <see cref="IslandView"/>.</summary>
        public void Refresh(ResortGame game, IslandView island)
        {
            if (game.CurrentWave != _wave)
            {
                Clear();
                _wave = game.CurrentWave;
                _plan = _wave != null ? GuestPlan.Build(_wave) : null;
            }

            if (_wave == null) return;

            foreach (BuildingRuntime runtime in game.Buildings)
            {
                BuildingView view = island.GetView(runtime.Id);
                if (view == null) continue;

                if (!_buildings.TryGetValue(runtime.Need, out Building building))
                {
                    Vector3 origin = transform.InverseTransformPoint(view.transform.position);
                    DoorSetup setup = Array.Find(doorSetups, d => d != null && d.building == runtime.Id);
                    building = new Building(origin + (setup != null ? setup.door : entranceOffset));
                    _buildings[runtime.Need] = building;
                }

                building.Status = game.GetNeedStatus(runtime.Need);
                building.ServiceSeconds = (float)game.GetServiceSeconds(runtime.Id);
                building.Seats = runtime.Seats;
            }

            while (_nextGuest < _plan.Guests.Count && IsReleased(_plan.Guests[_nextGuest].Visits[0]))
            {
                Send(_plan.Guests[_nextGuest]);
                _nextGuest++;
            }
        }

        /// <summary>Runs the walking forward without waiting for frames (tests).</summary>
        internal void FastForward(float seconds)
        {
            while (seconds > 0f)
            {
                float dt = Mathf.Min(StepSeconds, seconds);
                Step(dt);
                seconds -= dt;
            }
        }

        private void Update() => Step(Time.deltaTime);

        private void Step(float dt)
        {
            _nextSpawnDelay = Mathf.Max(0f, _nextSpawnDelay - dt);
            for (int i = _walkers.Count - 1; i >= 0; i--)
            {
                if (!StepWalker(_walkers[i], dt))
                {
                    Recycle(_walkers[i].Renderer);
                    _walkers.RemoveAt(i);
                }
            }
        }

        private bool IsReleased(GuestVisit visit) =>
            _buildings.TryGetValue(visit.Need, out Building b) && b.Status.Arrived > visit.Unit;

        /// <summary>
        /// The guest's unit has been served: the Core serves a need's units in arrival order, so unit k is done
        /// once <see cref="NeedStatus.Served"/> is above k (OUR-46 / OUR-47).
        /// </summary>
        private bool IsServed(GuestVisit visit) =>
            _buildings.TryGetValue(visit.Need, out Building b) && b.Status.Served > visit.Unit;

        /// <returns>False once the guest has made its last visit and faded out.</returns>
        private bool StepWalker(Walker walker, float dt)
        {
            switch (walker.State)
            {
                case WalkerState.Waiting:
                    walker.Delay -= dt;
                    if (walker.Delay <= 0f) walker.State = WalkerState.Walking;
                    return true;

                case WalkerState.Walking:
                    FadeIn(walker, dt);
                    if (Walk(walker, dt))
                    {
                        if (walker.GoingHome)
                        {
                            walker.State = WalkerState.Boarding;
                            walker.Fade = 0f;
                            return true;
                        }

                        // At the door: the guest goes in, and the building's seat (or queue square) fills now.
                        if (_buildings.TryGetValue(walker.CurrentVisit.Need, out Building building))
                        {
                            building.Delivered++;
                            building.Inside++;
                        }
                        walker.State = WalkerState.GoingIn;
                        walker.Fade = 0f;
                    }

                    return true;

                case WalkerState.GoingIn:
                    walker.Fade += dt;
                    SetAlpha(walker, 1f - walker.Fade / FadeSeconds);
                    if (walker.Fade < FadeSeconds) return true;
                    walker.State = WalkerState.Inside;
                    walker.InsideTime = 0f;
                    return true;

                case WalkerState.Boarding:
                    walker.Fade += dt;
                    SetAlpha(walker, 1f - walker.Fade / FadeSeconds);
                    return walker.Fade < FadeSeconds; // on the boat: gone

                default: // Inside: waits to be served here, then walks on to its next building or back to the boat
                    walker.InsideTime += dt;
                    if (walker.InsideTime < GetServiceSeconds(walker.CurrentVisit) || !IsServed(walker.CurrentVisit)) return true;

                    if (walker.NextVisit >= walker.Guest.Count)
                    {
                        LeaveBuilding(walker);
                        GoHome(walker);
                    }
                    else if (IsReleased(walker.Guest.Visits[walker.NextVisit]))
                    {
                        LeaveBuilding(walker);
                        StartVisit(walker, via: plazaPosition);
                    }

                    return true;
            }
        }

        /// <summary>The guest walks out: its seat (or queue) square empties.</summary>
        private void LeaveBuilding(Walker walker)
        {
            if (_buildings.TryGetValue(walker.CurrentVisit.Need, out Building building) && building.Inside > 0) building.Inside--;
        }

        private float GetServiceSeconds(GuestVisit visit) =>
            _buildings.TryGetValue(visit.Need, out Building b) ? b.ServiceSeconds : 0f;

        /// <summary>Points the walker at its next building, through the plaza.</summary>
        private void StartVisit(Walker walker, Vector3 via)
        {
            GuestVisit visit = walker.Guest.Visits[walker.NextVisit++];
            walker.CurrentVisit = visit;
            Vector3 door = _buildings.TryGetValue(visit.Need, out Building building) ? building.Door : plazaPosition;
            walker.Target = via + walker.Spread;
            walker.Waypoints = new[] { door + new Vector3(walker.Spread.x * 0.5f, 0f, 0f) };
            walker.NextWaypoint = 0;
            walker.State = WalkerState.Walking;
        }

        /// <summary>Every need met: back across the plaza to the dock, and onto the boat.</summary>
        private void GoHome(Walker walker)
        {
            walker.GoingHome = true;
            walker.Target = plazaPosition + walker.Spread;
            walker.Waypoints = new[] { dockPosition + walker.Spread * 0.5f };
            walker.NextWaypoint = 0;
            walker.State = WalkerState.Walking;
        }

        /// <summary>Moves the walker towards its next point. Returns true when it has reached the last one.</summary>
        private bool Walk(Walker walker, float dt)
        {
            Transform t = walker.Renderer.transform;
            Vector3 position = t.localPosition;
            position.z = 0f;
            Vector3 direction = walker.Target - position;
            float step = walkSpeed * dt;
            bool arrived = false;
            while (step > 0f)
            {
                Vector3 toTarget = walker.Target - position;
                float distance = toTarget.magnitude;
                if (distance > 1e-4f) direction = toTarget;
                if (step < distance)
                {
                    position += toTarget / distance * step;
                    break;
                }

                position = walker.Target;
                step -= distance;
                if (walker.Waypoints != null && walker.NextWaypoint < walker.Waypoints.Length)
                {
                    walker.Target = walker.Waypoints[walker.NextWaypoint++];
                    continue;
                }

                arrived = true;
                break;
            }

            // Lower on screen = nearer the viewer, so guests in front draw over guests behind.
            position.z = position.y * 0.01f;
            t.localPosition = position;
            walker.AnimTime += dt;
            Show(walker, direction);
            return arrived;
        }

        private void Show(Walker walker, Vector3 direction)
        {
            Sprite[] frames = direction.y > 0f ? walker.Look.walkBack : walker.Look.walkFront;
            walker.Renderer.sprite = frames[Mathf.FloorToInt(walker.AnimTime * frameRate) % frames.Length];
            walker.Renderer.flipX = direction.x < 0f;
        }

        private void Send(PlannedGuest guest)
        {
            GuestLook look = NextLook();
            if (look == null || guest.Count == 0) return;

            // Each guest takes a slightly different line, so a group doesn't walk as one sprite.
            var spread = new Vector3(Spread(), Spread() * 0.5f, 0f);

            SpriteRenderer renderer = Take();
            renderer.transform.localPosition = dockPosition + spread * 0.5f;
            float scale = guestHeight / Mathf.Max(0.01f, look.walkFront[0].bounds.size.y);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.color = new Color(1f, 1f, 1f, 0f);
            renderer.sortingOrder = sortingOrder;

            var walker = new Walker(renderer, look, guest, spread)
            {
                Delay = _nextSpawnDelay,
                AnimTime = (float)_random.NextDouble(),
            };
            StartVisit(walker, via: plazaPosition);
            walker.State = WalkerState.Waiting;
            Show(walker, walker.Target - renderer.transform.localPosition);
            _walkers.Add(walker);
            _nextSpawnDelay += spawnSpacing;
        }

        private float Spread() => ((float)_random.NextDouble() * 2f - 1f) * PathSpread;

        private GuestLook NextLook()
        {
            if (looks.Length == 0) return null;
            int start = _random.Next(looks.Length);
            for (int i = 0; i < looks.Length; i++)
            {
                GuestLook look = looks[(start + i) % looks.Length];
                if (look != null && look.IsValid) return look;
            }

            return null;
        }

        private static void FadeIn(Walker walker, float dt)
        {
            if (walker.Renderer.color.a < 1f) SetAlpha(walker, walker.Renderer.color.a + dt / FadeSeconds);
        }

        private static void SetAlpha(Walker walker, float alpha)
        {
            Color color = walker.Renderer.color;
            color.a = Mathf.Clamp01(alpha);
            walker.Renderer.color = color;
        }

        private SpriteRenderer Take()
        {
            if (_pool.Count > 0)
            {
                SpriteRenderer pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var go = new GameObject("Guest");
            go.transform.SetParent(transform, false);
            return go.AddComponent<SpriteRenderer>();
        }

        private void Recycle(SpriteRenderer renderer)
        {
            renderer.gameObject.SetActive(false);
            _pool.Push(renderer);
        }

        private void Clear()
        {
            foreach (Walker walker in _walkers) Recycle(walker.Renderer);
            _walkers.Clear();
            _buildings.Clear();
            _plan = null;
            _nextGuest = 0;
            _nextSpawnDelay = 0f;
        }

        /// <summary>Where one building's door is (relative to the building).</summary>
        [Serializable]
        public sealed class DoorSetup
        {
            public BuildingId building;

            [Tooltip("Door position relative to the building.")]
            public Vector3 door;
        }

        private enum WalkerState
        {
            Waiting, // on the boat, waiting for its turn to step off
            Walking,  // on the way to its next building, or back to the boat
            GoingIn,  // fading out at the door; the seat or queue square fills
            Inside,   // inside, until it is served here (and the Core has released its next unit)
            Boarding, // back at the dock, fading out onto the boat
        }

        private sealed class Building
        {
            public Building(Vector3 door) => Door = door;

            public Vector3 Door { get; }
            public NeedStatus Status;
            public int Delivered;
            public float ServiceSeconds;

            /// <summary>Guests inside the building right now (fills its seat squares).</summary>
            public int Inside;
            public int Seats;
        }

        private sealed class Walker
        {
            public Walker(SpriteRenderer renderer, GuestLook look, PlannedGuest guest, Vector3 spread)
            {
                Renderer = renderer;
                Look = look;
                Guest = guest;
                Spread = spread;
            }

            public SpriteRenderer Renderer { get; }
            public GuestLook Look { get; }
            public PlannedGuest Guest { get; }
            public Vector3 Spread { get; }
            public WalkerState State;
            public GuestVisit CurrentVisit;
            public int NextVisit;
            public float Delay;
            public float AnimTime;
            public float Fade;
            public float InsideTime;
            public Vector3 Target;
            public Vector3[] Waypoints;
            public int NextWaypoint;
            public bool GoingHome;
        }
    }
}
