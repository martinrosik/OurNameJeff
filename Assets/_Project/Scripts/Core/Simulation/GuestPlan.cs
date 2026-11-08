using System;
using System.Collections.Generic;

namespace ResortEmpire.Core
{
    /// <summary>
    /// Which guest carries which need units in a wave (Build doc §4.4: demand is in need units, a guest carries
    /// about 2 of them, the guest count is visual).
    /// <para>
    /// Every unit of the wave belongs to exactly one guest, so the guests visit each building exactly as often as
    /// the forecast says (wave 1: Rooms 8, Drinks 8, Food 6 over 12 guests). A guest never carries the same need
    /// twice, and visits its buildings in the order the Core releases its units. Guests are filled in arrival
    /// order: the first released unit opens a guest, the next units join the oldest guest that still has room
    /// and lacks that need. Only the view uses this; it doesn't change the simulation.
    /// </para>
    /// </summary>
    public sealed class GuestPlan
    {
        private readonly List<PlannedGuest> _guests;

        private GuestPlan(List<PlannedGuest> guests) => _guests = guests;

        public IReadOnlyList<PlannedGuest> Guests => _guests;

        public static GuestPlan Build(WaveSimulation wave)
        {
            if (wave == null) throw new ArgumentNullException(nameof(wave));

            var units = new List<GuestVisit>();
            int needCount = 0;
            foreach (NeedId need in wave.Needs)
            {
                int total = wave.GetTotal(need);
                if (total > 0) needCount++;
                for (int k = 0; k < total; k++) units.Add(new GuestVisit(need, k, wave.GetArrivalTime(need, k)));
            }

            var guests = new List<PlannedGuest>();
            if (units.Count == 0) return new GuestPlan(guests);

            // Released order: by arrival time, ties in need order (Rooms before Drinks before Food …).
            units.Sort((a, b) =>
            {
                int byTime = a.Time.CompareTo(b.Time);
                return byTime != 0 ? byTime : ((int)a.Need).CompareTo((int)b.Need);
            });

            // At least enough guests that nobody needs the same thing twice, at most one per unit.
            int guestCount = Math.Max(wave.GuestCount, (units.Count + needCount - 1) / needCount);
            guestCount = Math.Max(1, Math.Min(guestCount, units.Count));
            // Even shares: the first guests carry one unit more than the rest, so every one of the guestCount guests
            // gets a unit (one shared cap let the first guests take everything, e.g. 11 guests instead of 12 in wave 1).
            int baseCapacity = units.Count / guestCount;
            int guestsWithOneMore = units.Count % guestCount;

            foreach (GuestVisit unit in units)
            {
                PlannedGuest target = null;
                for (int i = 0; i < guests.Count; i++)
                {
                    int capacity = i < guestsWithOneMore ? baseCapacity + 1 : baseCapacity;
                    if (guests[i].Count < capacity && !guests[i].Carries(unit.Need))
                    {
                        target = guests[i];
                        break;
                    }
                }

                if (target == null && guests.Count < guestCount)
                {
                    target = new PlannedGuest();
                    guests.Add(target);
                }

                if (target == null)
                {
                    // Every guest is full or already carries this need: give it to the least loaded guest without it.
                    foreach (PlannedGuest guest in guests)
                    {
                        if (guest.Carries(unit.Need)) continue;
                        if (target == null || guest.Count < target.Count) target = guest;
                    }
                }

                if (target == null)
                {
                    target = new PlannedGuest();
                    guests.Add(target);
                }

                target.Add(unit);
            }

            return new GuestPlan(guests);
        }

        /// <summary>Visits per need over all guests (equals the wave's demand).</summary>
        public int CountVisits(NeedId need)
        {
            int count = 0;
            foreach (PlannedGuest guest in _guests)
            {
                foreach (GuestVisit visit in guest.Visits)
                {
                    if (visit.Need == need) count++;
                }
            }

            return count;
        }
    }

    /// <summary>One guest: the buildings it visits, in order.</summary>
    public sealed class PlannedGuest
    {
        private readonly List<GuestVisit> _visits = new List<GuestVisit>();

        public IReadOnlyList<GuestVisit> Visits => _visits;
        public int Count => _visits.Count;

        public bool Carries(NeedId need)
        {
            foreach (GuestVisit visit in _visits)
            {
                if (visit.Need == need) return true;
            }

            return false;
        }

        internal void Add(GuestVisit visit) => _visits.Add(visit);
    }

    /// <summary>One need unit a guest brings to a building: unit <see cref="Unit"/> (0-based) of <see cref="Need"/>.</summary>
    public readonly struct GuestVisit
    {
        public GuestVisit(NeedId need, int unit, double time)
        {
            Need = need;
            Unit = unit;
            Time = time;
        }

        public NeedId Need { get; }
        public int Unit { get; }

        /// <summary>Wave time the Core releases this unit.</summary>
        public double Time { get; }
    }
}
