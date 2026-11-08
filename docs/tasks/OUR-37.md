# OUR-37 Zobrazovanie potrieb hostí na bočnom paneli

**The Wave Needs Panel (S2): a side panel showing how the resort is coping, need by need.** Needs OUR-34. It shows
OUR-38/39 states once they exist.

## What the player sees (UI doc S2, "the heart of the game")

A **manager's dashboard** next to the island, not a quest list. It opens on its own when the wave starts and sits
on the **right side**, so the island stays visible. One row per need on the island:

- **"Food 2/6"**: served / total for this wave (the total grows if extra guests come later)
- a progress bar
- the state: OK / **Queue growing!** (red, OUR-38) / **Catching up** (green, OUR-39) / All served
- how many are waiting, and whether the building has a Worker ("No Worker (slow)" vs "Worker"). When seats are full
  and a Worker is present, say so ("Full: 3/3 seats"). This helps the player tell **"too small"** (upgrade later)
  apart from **"no Worker"** (move one).

Plus:
- a header "WAVE 1", and on the right **"Guests arriving..."**, then **"All guests arrived"** after 90 s (OUR-41)
- one line for the latest guest quote at the bottom (OUR-38)

## Rules (UI doc S2)

- **Never show Wave Satisfaction as a number during the wave** (decided 2026-09-29).
- Show only needs whose building is on the island. The layout must handle 3 → 4 → 5 needs **without moving the
  existing rows** (Relax and Ocean View come with later unlocks).
- Ocean View is a premium need: make it look different (e.g. a gold tint, "PREMIUM").
- Tapping a row will open Worker Allocation (OUR-45). Make the row a Button now.
- Visible in WAVE_INCOMING and WAVE_ACTIVE; hidden in AWAITING_WAVE, where the forecast (OUR-33) uses the same area.

## Where it goes

- New `Scripts/UI/Greybox/NeedsPanelView.cs` (a plain class like `HudView`), created in `GreyboxUI.Build()` inside
  **`GreyboxUI.SideArea`** (300 × ~345 reference px, right of the island) and refreshed in `GreyboxUI.LateUpdate()`.
- Build it with `UIFactory`: `Panel`, `Text`, `Button`, `Bar` + `SetFill`. Rows about 48 px tall work for 5 needs.
- Data: `ResortGame.GetNeedStatus(need)` from OUR-34 (Served, Total, Waiting, Seated, later State) and
  `GetBuilding(...).HasWorker`.
- Strings go in `GameText`. Use plain characters only ("...", not "…").

## Done when

- During wave 1 the panel shows Rooms, Drinks and Food, with counts that rise to 8/8, 8/8, 6/6.
- The Restaurant row says "No Worker (slow)".
- After 90 s the header says "All guests arrived".

## Tests to write (PlayMode)

- Start a wave, `Tick` past the boat: the panel is visible with 3 rows.
- After the wave ends, the panel is hidden again.
- Served counts match `GetNeedStatus`.
