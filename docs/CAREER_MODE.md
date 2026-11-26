# Career mode – BUHAY TSUPER

Main menu ▸ **CAREER**. You drive Mang Kanor's jeepney and work your way up from a student
permit to *Hari ng Kalsada*.

## Shifts
20 shifts in 5 chapters, one chapter per driver's license. Each shift is a *biyahe* on a set
route that ends at a terminal (the run ends there – no endless road):

| Star | How |
|---|---|
| ★ | Reach the terminal (crash before it and the shift doesn't count) |
| ★ | Also collect the **boundary** in **fares** (passenger money – road coins don't count) |
| ★ | Also meet the shift's extra goal (deliveries, near misses, no crashes, streak…) |

The HUD shows the metres left to the terminal and a fare counter (PAMASAHE, GAS, KITA).

## Money: fares, diesel, boundary, kita
- **Fares** (pamasahe) = everything passengers pay: fares, tips, fare dodgers caught with the horn.
- **Road coins** = coins picked up on the road. Always yours, for upgrades and jeepneys.
- **Diesel** bought at a gas station comes out of the earnings (the driver complains about it).
- In a career shift Mang Kanor takes the **boundary** out of the fares left after the diesel:
  `paid = min(boundary, fares − gas, run coins)` – never more, so the driver never goes into debt.
- **Kita** (take-home) = fares − gas − boundary. Results show the whole sum.
- Free ride has no boundary: kita = fares − gas.
A shift unlocks when the one before it (same chapter) has a star.

## Licenses
| License | Stars | Reward |
|---|---|---|
| Student Permit | 0 | – |
| Non-Pro License | 6 | 500 coins, 10 gems |
| Professional License | 15 | 1,000 coins, 20 gems |
| Operator – Prangkisa | 25 | 2,000 coins, 30 gems |
| Hari ng Kalsada | 36 | 4,000 coins, 50 gems |

Every star earned for the first time pays 40 coins (+30 per chapter).

## Where it lives
- Rules: `Scripts/Logic/Career.cs` (tests: `Tests/EditMode/CareerTests.cs`)
- Run flow: `GameplaySession` (`Shift`, `ArriveAtTerminal`), `GameManager.PlayShift`
- UI: `UIManager.Career.cs`
- Server: `supabase/migrations/20261126100000_jr_career.sql` + the generated catalog
  (`jr_catalog_career`, `jr_catalog_licenses`); `jr_submit_run` applies the same rules and the
  snapshot carries `career` / `career_rank`. Tests: `supabase/dev-tests/career.sql`.

## Tuning
Lengths, boundaries and goals are in `Career.Shifts`. After changing them run
`dotnet run --project unity/tools/ExportCatalog` and commit the new catalog migration.
The boundaries are first guesses (≈ 30 fares/km early, ≈ 60 fares/km at the end) and need
play-testing.
