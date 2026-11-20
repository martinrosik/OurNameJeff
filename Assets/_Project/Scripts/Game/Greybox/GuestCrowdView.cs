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
    /// and empties when it walks out. They follow the walking guests, not the Core's seats: the Core seats a unit
    /// the moment it arrives, while the guest still has to walk over, so its seat count would fill squares before
    /// anyone is there. The queue dots under a building show the Core's queue instead (<see cref="BuildingView"/>),
    /// so they always match the needs panel.
    /// </para>
    /// <para>
    /// <b>Mood bubbles:</b> when a guest walks out of a building, a bubble above it shows for a moment how that visit
    /// went: a smile, a flat face or a frown, from how long it waited for its seat (<see cref="WaveSatisfaction.GetGuestMood"/>,
    /// the same thresholds Wave Satisfaction uses). Only a face, never a number or a countdown: the Build doc (§4.4)
    /// keeps the score for Wave Complete. Approved by the designer (docs/design-conflicts.md, row 12).
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

        [Tooltip("How long a mood bubble stays above a guest who has just walked out of a building, in seconds.")]
        [SerializeField, Min(0.1f)] private float moodBubbleSeconds = 2f;

        [Tooltip("Width of a mood bubble on screen, in world units.")]
        [SerializeField, Min(0.05f)] private float moodBubbleSize = 0.6f;

        private const float FadeSeconds = 0.35f;
        private const float BubblePopSeconds = 0.15f;
        private const float PathSpread = 0.14f;
        private const float StepSeconds = 0.05f;

        private readonly List<Walker> _walkers = new List<Walker>();
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly Stack<SpriteRenderer> _bubblePool = new Stack<SpriteRenderer>();
        private SatisfactionSettings _satisfaction;
        private readonly Dictionary<NeedId, Building> _buildings = new Dictionary<NeedId, Building>();
        private readonly System.Random _random = new System.Random(34);
        private WaveSimulation _wave;
        private GuestPlan _plan;
        private int _nextGuest;
        private bool _replannedForRush;
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

        /// <summary>
        /// Guests the player has seen served at the building of <paramref name="need"/>: served by the Core <b>and</b>
        /// walked out of the building again (its seat or queue square emptied). The Wave Needs Panel counts these, so
        /// "Food 2/6" never runs ahead of what the island shows.
        /// </summary>
        public int GetShownServed(NeedId need)
        {
            if (!_buildings.TryGetValue(need, out Building b)) return 0;
            return Mathf.Min(b.Left, b.Status.Served);
        }

        /// <summary>
        /// A need's numbers as the island shows them, for the Wave Needs Panel: guests seated are the ones inside the
        /// building (<see cref="GetShownSeated"/>), waiting the ones there beyond its seats (<see cref="GetShownWaiting"/>),
        /// served the ones that walked out again (<see cref="GetShownServed"/>). Total and arrived stay the Core's.
        /// </summary>
        public NeedStatus GetShownStatus(NeedId need, NeedStatus core) =>
            new NeedStatus(core.Need, core.Total, core.Arrived, GetShownWaiting(need), GetShownSeated(need), GetShownServed(need));

        /// <summary>
        /// Queue squares shown: guests at the building who found every seat taken. A queue square only appears once
        /// the seats are full on the island, never ahead of it because the Core already seated someone still walking.
        /// </summary>
        public int GetShownWaiting(NeedId need)
        {
            if (!_buildings.TryGetValue(need, out Building b)) return 0;
            return Mathf.Max(0, b.Inside - b.Seats);
        }

        /// <summary>Seat squares shown as taken: guests inside the building, up to its seats.</summary>
        public int GetShownSeated(NeedId need)
        {
            if (!_buildings.TryGetValue(need, out Building b)) return 0;
            return Mathf.Min(b.Inside, b.Seats);
        }

        /// <summary>The moods of the bubbles above guests right now, one per bubble.</summary>
        internal List<GuestMood> GetShownMoods()
        {
            var moods = new List<GuestMood>();
            foreach (Walker walker in _walkers)
            {
                if (walker.Bubble != null) moods.Add(walker.Mood);
            }

            return moods;
        }

        /// <summary>Sends out guests whose first unit the Core has released. Called by <see cref="IslandView"/>.</summary>
        public void Refresh(ResortGame game, IslandView island)
        {
            _satisfaction = game.Config.satisfaction;

            if (game.CurrentWave != _wave)
            {
                Clear();
                _wave = game.CurrentWave;
                _plan = _wave != null ? GuestPlan.Build(_wave) : null;
            }

            if (_wave == null) return;

            // Every building of the game gets an entry, also one without a view: otherwise its guests would never step off and the wave could never end.
            foreach (BuildingRuntime runtime in game.Buildings)
            {
                if (!_buildings.TryGetValue(runtime.Need, out Building building))
                {
                    building = new Building(GetDoor(runtime.Id, island));
                    _buildings[runtime.Need] = building;
                }

                building.Status = game.GetNeedStatus(runtime.Need);
                building.ServiceSeconds = (float)game.GetServiceSeconds(runtime.Id);
                building.Seats = runtime.Seats;
            }

            // Sudden rush: its extra units need guests. Everyone not yet off the boat is shared out again together with
            // them, plus one extra guest per rush unit, so the rush shows as a group hurrying from the boat.
            if (_wave.SuddenRush != null && !_replannedForRush)
            {
                _plan = _plan.Replan(_wave, _nextGuest, _wave.SuddenRush.Units);
                _replannedForRush = true;
            }

            while (_nextGuest < _plan.Guests.Count && IsReleased(_plan.Guests[_nextGuest].Visits[0]))
            {
                Send(_plan.Guests[_nextGuest]);
                _nextGuest++;
            }
        }

        /// <summary>
        /// Joins a wave that is already under way (the player is back from the tutorial): sends out every guest the Core
        /// has released and walks them forward until they're where the frozen game says they are, inside a building
        /// waiting to be served, or back on the boat. The Core doesn't move meanwhile, so nothing in the wave changes.
        /// </summary>
        internal void CatchUp(ResortGame game, IslandView island)
        {
            Refresh(game, island);
            if (_nextGuest > 0) FastForward(CatchUpSeconds);
        }

        // Long enough for the last guest of a big wave to step off and walk through three buildings' service.
        private const float CatchUpSeconds = 180f;

        /// <summary>Runs the walking forward without waiting for frames (tests, and catching up after the tutorial).</summary>
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
                    HideBubble(_walkers[i]);
                    Recycle(_walkers[i].Renderer);
                    _walkers.RemoveAt(i);
                }
                else
                {
                    StepBubble(_walkers[i], dt);
                }
            }
        }

        /// <summary>
        /// The guest has just walked out of the building of <paramref name="visit"/>: a bubble above it shows how long
        /// it waited for its seat there.
        /// </summary>
        private void ShowMood(Walker walker, GuestVisit visit)
        {
            if (_wave == null || _satisfaction == null) return;
            if (!_wave.TryGetWaitTime(visit.Need, visit.Unit, out double wait)) return;

            HideBubble(walker);
            walker.Mood = WaveSatisfaction.GetGuestMood(wait, _satisfaction);
            walker.Bubble = TakeBubble();
            walker.Bubble.sprite = MoodBubbleArt.Get(walker.Mood);
            walker.Bubble.sortingOrder = sortingOrder + 1; // above every guest
            walker.BubbleTime = 0f;
            StepBubble(walker, 0f);
        }

        /// <summary>Keeps the bubble above the guest's head: pops in, stays, then fades out.</summary>
        private void StepBubble(Walker walker, float dt)
        {
            if (walker.Bubble == null) return;

            walker.BubbleTime += dt;
            if (walker.BubbleTime >= moodBubbleSeconds)
            {
                HideBubble(walker);
                return;
            }

            Bounds body = walker.Renderer.bounds;
            Vector3 head = transform.InverseTransformPoint(new Vector3(body.center.x, body.max.y, body.center.z));
            walker.Bubble.transform.localPosition = head + new Vector3(0f, 0.02f, 0f);

            float pop = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(walker.BubbleTime / BubblePopSeconds));
            float scale = moodBubbleSize * pop;
            walker.Bubble.transform.localScale = new Vector3(scale, scale, 1f);

            float fadeIn = Mathf.Clamp01(walker.BubbleTime / BubblePopSeconds);
            float fadeOut = Mathf.Clamp01((moodBubbleSeconds - walker.BubbleTime) / FadeSeconds);
            walker.Bubble.color = new Color(1f, 1f, 1f, Mathf.Min(fadeIn, fadeOut));
        }

        private void HideBubble(Walker walker)
        {
            if (walker.Bubble == null) return;
            walker.Bubble.gameObject.SetActive(false);
            _bubblePool.Push(walker.Bubble);
            walker.Bubble = null;
        }

        private SpriteRenderer TakeBubble()
        {
            if (_bubblePool.Count > 0)
            {
                SpriteRenderer pooled = _bubblePool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            var go = new GameObject("MoodBubble");
            go.transform.SetParent(transform, false);
            return go.AddComponent<SpriteRenderer>();
        }

        /// <summary>Where guests walk into a building: its door, or the plaza when the building has no view on the island.</summary>
        private Vector3 GetDoor(BuildingId building, IslandView island)
        {
            BuildingView view = island.GetView(building);
            if (view == null)
            {
                Debug.LogWarning($"{building} has no BuildingView on the island, so its guests walk to the plaza. Rebuild the scene with Resort Empire > Greybox > Rebuild Greybox Scene.", this);
                return plazaPosition;
            }

            Vector3 origin = transform.InverseTransformPoint(view.transform.position);
            DoorSetup setup = Array.Find(doorSetups, d => d != null && d.building == building);
            return origin + (setup != null ? setup.door : entranceOffset);
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
                    // Once the Core has served everyone, nobody sits out the rest of its service time (OUR-35: resolving is short).
                    bool stayedLongEnough = walker.InsideTime >= GetServiceSeconds(walker.CurrentVisit) || _wave.IsFinished;
                    if (!stayedLongEnough || !IsServed(walker.CurrentVisit)) return true;

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

        /// <summary>The guest walks out, served: its seat (or queue) square empties and it counts as served.</summary>
        private void LeaveBuilding(Walker walker)
        {
            ShowMood(walker, walker.CurrentVisit);
            if (!_buildings.TryGetValue(walker.CurrentVisit.Need, out Building building)) return;
            if (building.Inside > 0) building.Inside--;
            building.Left++; // served there: now the needs panel may count it
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
            foreach (Walker walker in _walkers)
            {
                HideBubble(walker);
                Recycle(walker.Renderer);
            }

            _walkers.Clear();
            _buildings.Clear();
            _plan = null;
            _nextGuest = 0;
            _nextSpawnDelay = 0f;
            _replannedForRush = false;
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

            /// <summary>Guests that were served here and have walked out again.</summary>
            public int Left;
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

            /// <summary>The mood bubble above the guest, or null when it shows none.</summary>
            public SpriteRenderer Bubble;
            public GuestMood Mood;
            public float BubbleTime;
        }
    }
}
