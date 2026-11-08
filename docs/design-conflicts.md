# Design conflicts: Figma vs. docs

Where the Figma screens and the design docs (GDD, Build doc, UI screens doc, version IV) disagree.
**The designer decides each row.** Until a row is Decided, don't build that part of the screen. Ask instead.

How to use:
- Found a new conflict? Add a row with status **Open** and tell the designer.
- Once decided, set the status to **Decided: docs** / **Decided: Figma** / **Decided: other**, write the decision
  in one line, and update whichever source is wrong (doc or Figma) so the conflict doesn't come back.

Figma frames: [S1](https://www.figma.com/design/whlebVoyEKoW4emX7kdR2B/Resort-Empire?node-id=1-2) ·
[S2](https://www.figma.com/design/whlebVoyEKoW4emX7kdR2B/Resort-Empire?node-id=5-106) ·
[S3](https://www.figma.com/design/whlebVoyEKoW4emX7kdR2B/Resort-Empire?node-id=6-249) ·
[S4](https://www.figma.com/design/whlebVoyEKoW4emX7kdR2B/Resort-Empire?node-id=6-323) ·
[S5](https://www.figma.com/design/whlebVoyEKoW4emX7kdR2B/Resort-Empire?node-id=6-450) ·
[S9](https://www.figma.com/design/whlebVoyEKoW4emX7kdR2B/Resort-Empire?node-id=6-535)

| # | Screen | Figma shows | Docs say | Status | Decision |
|---|---|---|---|---|---|
| 1 | S1 Home | Purple **"VIP: 3 waves"** pill under the resort status | VIP isn't in the prototype at all. In MVP the Home VIP panel only appears while a VIP is waiting (UI doc S1, §F.4) | Open | |
| 2 | S1 Home | Energy shown as **45/50**, which reads as a cap | Prototype Energy is uncapped. The cap is an MVP tuning question (Build doc §4.6) | Open | |
| 3 | S1 Home | No **next-wave forecast** visible | AWAITING_WAVE shows a forecast (`Next wave: Rooms 16 · Drinks 18 · Food 14`) so the player can place Workers before Start Wave (UI doc S1) | Open | |
| 4 | S2 Needs Panel | Live **Satisfaction bar** with 3★ 50% / 4★ 70% / 5★ 90% marks during the wave | Satisfaction is never shown during a wave, only on Wave Complete. Wave thresholds are 1★ 40 · 2★ 55 · 3★ 70 · 4★ 80 · 5★ 90 · Perfect 100 (UI doc S2, Build doc §4.4) | Open | |
| 5 | S2 Needs Panel | The frame doesn't show which buildings have a Worker, an "All guests arrived" state, or the Energy **Speed Up ⚡3** button | The panel always shows which buildings are staffed, marks the end of arrivals, and offers Speed Up on an overloaded building (UI doc S2). These may just be missing states, not disagreements | Open | |
| 6 | S3 | Titled **"Assign Staff – Restaurant"** with **named chefs** (Marco, Lina, Leo), star ratings, Available/Busy tags | Prototype S3 is **Worker Allocation**: anonymous, interchangeable Workers, no names/levels/ratings. Moving one = "pick which building to leave empty". It must show what the move costs elsewhere (UI doc S3, §F.6). Named staff with cards are the MVP Manager layer | Open | |
| 7 | S5 Upgrade | **Food Capacity 6 → 9** and **Food Quality Basic → Improved** | Prototype upgrades change seats only, **3 → 5 → 7**. Quality (`base_quality`) is MVP Lv4+ (Build doc §4.8, UI doc S5) | Open | |
| 8 | S9 Area Unlock | No **"Includes: 1 Worker"** line; CTA "Unlock & Expand"; title "The Luxury Pool" | The screen must say the unlock includes a Worker, or the player waits for a second purchase. Example copy: `New Area Available: Pool · Adds: Relax demand · Includes: 1 Worker · Cost: 1,200 Coins · [Unlock]` (UI doc S9) | Open | |
| 9 | S9 Area Unlock | One design | S9 fires twice (Pool, Bungalows), and the second time must not feel recycled (UI doc S9) | Open | |
| 10 | Jira OUR-46 / OUR-47 | Titles say a staffed building runs at "0.33 s" and an unstaffed one at "1 s" | Build doc §4.4/§4.8: `service_time` 12 s per guest per seat, `utilization` 1.0 with a Worker and 0.33 without (about 36 s per guest). Possibly the 0.33 utilization was read as seconds. Until decided, implement both as config values with the doc numbers | Open | |
| 11 | Jira OUR-35 | "Ukončenie vlny do 2 minút" (wave ends within 2 minutes) | Build doc §4.4, WR3/WR4: no hard cutoff. The 90 s timer closes arrivals, and the wave ends when everyone is served, which is about 2 min when handled well and longer when not. A hard cap would send waiting guests home unserved | Open | |

S4 Wave Complete hasn't been compared yet (its Figma frame didn't load during review). Check it against UI doc S4.
