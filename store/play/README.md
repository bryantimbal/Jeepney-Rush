# Google Play graphics – Jeepney Rush (Unity version)

Upload in Play Console ▸ Grow users ▸ Store presence ▸ Main store listing.

| Play Console field | File | Size / format |
|---|---|---|
| App icon | `icon-512.png` | 512×512, 32-bit PNG (Play rounds the corners itself) |
| Feature graphic | `feature-graphic-1024x500.png` | 1024×500, 24-bit PNG, no transparency |
| Phone screenshots | `phone/01…06` | 1080×1920 (9:16) |
| 7-inch tablet screenshots | `tablet-7in/01…06` | 1152×2048 (9:16) |
| 10-inch tablet screenshots | `tablet-10in/01…06` | 1440×2560 (9:16) |

Upload the screenshots in number order – the first 3 are what most people see.

These are rendered from the game's own 3D models, textures and fonts with a HUD drawn to match
the in-game layout. Replace them with real captures from a phone once you have them; Play
expects screenshots to show the actual game.

Regenerate after art changes:
```
dotnet run --project unity/Tools.Preview -- unity/Tools.Preview/bin/pv
node scripts/store-assets/render.mjs        # optional filter, e.g.: node scripts/store-assets/render.mjs garage
python3 scripts/store-assets/compose.py     # captions are at the top of compose.py
```
(`store/icon-512.png` and `store/feature-graphic-1024x500.png` one level up are from the older web version.)
