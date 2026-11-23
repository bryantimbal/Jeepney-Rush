# Roads update: two-way roads, flyovers, bridges, fog, Fiesta Town

Unity can't run in the build environment, so this was checked with compile checks, the .NET
tests (210 pass, 1 long sweep skipped by default), the Supabase SQL tests and three.js
mock-ups. It isn't finished until it has been played on a phone.

## What's new

| Route | Change |
|---|---|
| Provincial Road, Beach Highway (coastal) | **Two-way traffic.** The left lane carries oncoming vehicles behind a solid double yellow line. PARA stops are always on the right, and nobody weaves into or out of the oncoming lane. Oncoming vehicles still count as near misses, but not as overtakes. |
| Beach Highway | River-mouth **arch bridge** slices (blue steel arches, parapets, lamps and bangkas) and a more winding road. |
| EDSA | **Flyover** slices: an elevated road crossing high over EDSA and the MRT, with piers off the lanes, a green direction sign and cars on top. |
| Baguio | **Steeper** streets (up to 18% grade), **tighter curves**, and **rolling fog banks** with a "MAKAPAL ANG HAMOG!" warning. Visibility never drops below 95 m. Mountain Province gets lighter fog banks. |
| **Fiesta Town** (new, unlocks at 2,200 m) | Bahay na bato with capiz windows, banderitas strung across the street, food stalls, lechon, balloons and crowds. Special slices have the church with a "MALIGAYANG PIYESTA!" arch, and the landmark slice has a perya (Ferris wheel and carousel). Parade floats are the main road event, and events come 25% more often. Traffic is mostly tricycles and motorbikes, there are barangay tanod checkpoints, the music is a marching-band variant, and there are 12 stops (Plazuela, Simbahan, Peryahan...). |

## Fairness

- Oncoming rows are only placed if the road stays passable **from every lane the player may
  be in** (mid lane change), with 25% slower steering.
- The fairness test bot now reports its real lane to the spawner, as the game does, and plans
  lane changes with the spawner's steering time.
- Long sweep (140 seeds × 4 km, all 8 routes): **1 bad seed** (a Cebu stop-zone intrusion of a
  kind that was there before), down from 12 before this update.

## Server

New migration **`20261123184227_jr_catalog.sql`** (generated). It adds route 7 "Fiesta Town",
and the "Island Hopper" achievement now needs all 8 routes. Apply it in Supabase like the
earlier migrations.

## Device checklist

1. Provincial and Beach: oncoming cars come down the left lane and face you. The double yellow
   line is visible. Stops are on the right. Passing close to oncoming traffic gives NEAR MISS.
2. Beach: the arch bridge shows up now and then, and the camera never clips the top bracing.
3. EDSA: a flyover passes overhead now and then without touching the MRT or the camera.
4. Baguio: the fog thickens and clears with a warning, and the road is still readable.
5. Fiesta Town: it unlocks at 2,200 m. Check the arch text reads correctly, the banderitas sit
   above the camera, the tanod checkpoint, frequent parade floats, and smooth frame rate.

# Steering: hold the sides or tilt (replaces swiping)

- **Analog steering** (`Logic/Steering.cs`): the jeepney now has a real sideways position and
  speed. Holding steer turns the "wheel" over 0.3 s, and letting go eases the turn out, so
  nothing snaps. At full lock one lane takes **0.6 s** (the old swipe hop took 0.42 s), and the
  spawner plans with exactly that time, so traffic stays fair. Wet roads steer slower, as before.
- **Settings ▸ Steering**:
  - **Hold sides** (default): keep a finger on the left or right half of the screen. Fingers
    that start on GAS / BRAKE / BUSINA / TURBO never steer, so you can hold GAS with one thumb
    and steer with the other.
  - **Tilt phone**: tilt like a steering wheel. The way the phone is held when the run starts
    counts as straight. There's a small dead zone, and **Steering sensitivity** sets how far
    you tilt for full lock. Without a tilt sensor the game falls back to Hold sides.
- **Lane assist** (on by default): hands off, the jeepney settles into the nearest lane so
  stops and gas stations line up. Turn it off for fully manual driving.
- **PC**: hold ← / → (or A / D).
- Swipe up (horn) and swipe down (brake) are gone. Use the BUSINA and BRAKE buttons.
- The first 3 runs show a short hint on how to steer.
- Projects with a saved balance asset are moved to the new 0.6 s lane time automatically
  (GameBalance v4).

Device checklist: try both modes, steer while holding GAS, check tilt calibration when holding
the phone at an angle, and check lane assist on and off.
