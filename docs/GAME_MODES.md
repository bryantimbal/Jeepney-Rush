# Game modes (main menu ▸ MODES)

| Mode | Rules | Rankings |
|---|---|---|
| Daily Challenge | Same road for everyone today | Daily board (as before) |
| **Rush Hour** | 3 minutes on the clock, traffic ×1.45, +2 people per stop. The run ends when time is up (or you crash). | Weekly RUSH board (+ per stat class) |
| **Night Shift** | Always night (8 PM – 4 AM), traffic ×0.7, one fewer person per stop, +30 % fares, night events (balut, drunks). Pause ▸ **UWI NA** ends the shift and keeps everything. | Weekly NIGHT board (+ per stat class) |
| **Ghost Race** | Race a rival's recorded run on the same road (same seed and route): their jeepney is see-through and can't be hit. Beat their score to win. | Counts like a free ride |

- Mode runs play on the route selected in ROUTES; career and the daily challenge always use normal rules.
- Rush Hour and Night Shift runs go on their own boards, not the normal ones. The server only
  accepts a Rush Hour score from a run that fits the clock.
- **Ghosts:** every free ride (and ghost race) records your path (a sample every 0.5 s). After the
  run is accepted the path is uploaded; the server checks it (no going backwards, no impossible
  speed, ends where the verified run ended) and keeps your best ghost per route and stat class.
  A ghost race picks another player's ghost in your class with the closest score above yours.
  Offline runs don't upload a ghost.

Code: `Logic/GameModes.cs`, `Logic/Ghost.cs`, `GameplaySession` (Mode, UpdateModeHud, EndShift,
ghost recording / SpawnGhost / LateUpdate), `UIManager.Modes.cs`, `UIManager.Ghost.cs`.
Server: `20261126110700_jr_game_modes.sql`, `20261126111100_jr_ghosts.sql`.
Tests: `GameModeTests`, `GhostTests`, `FairnessTests.RushHourIsBusyButFair`,
`dev-tests/game_modes.sql`, `dev-tests/ghosts.sql`.
