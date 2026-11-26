# Talyer customization

Garage ▸ **TALYER** customizes the jeepney on the turntable. Cosmetic only: stats and the
jeepney's stat class never change.

| Slot | Items |
|---|---|
| Pintura (paint) | Pula't Puti, Asul Dagat, Berde Bukid, Dilaw Araw, Itim at Chrome, Rosas Pista; *Gintong Hari* (earned: Hari ng Kalsada) |
| Windshield banner | GOD BLESS OUR TRIP, SAKAY NA!, BAHALA NA; *HARI NG KALSADA* (earned) |
| Rear sticker | BAYAD MUNA BAGO BABA, HOW'S MY DRIVING?, Santo Niño, Araw at Tatlong Bituin; *LISENSYADO!* (earned: Non-Pro) |
| Karatula (sign board) | QUIAPO–CUBAO, BACLARAN–DIVISORIA, MONUMENTO–LAWTON, COLON–IT PARK, BAGUIO–LA TRINIDAD, SPECIAL TRIP |
| Busina (horn) | Toot-Toot-Toot, Tunog-Pista (original musical tune), Air Horn (patok) |
| Mudflap | Red, chrome, MAHAL KITA print |
| Palamuti (hood ornament, classic jeepneys) | Five horses, eagle, carabao, none; *gold horses* (earned: Operator) |
| Ilaw (lights) | Fog lights, side marker lights, neon underglow (blue / pink) |

- Items are bought **once with coins** and work on **every jeepney**; each jeepney keeps its
  own setup. Tap an item to preview it on the turntable (horns play), tap again to buy / fit.
- Earned items unlock with a career license and can't be bought.
- Online, purchases go through `jr_buy_cosmetic` (catalog price, once per item) and the
  snapshot's `cosmetics` list restores them on another phone. Which item is fitted on which
  jeepney is stored on the phone.

Code: `Logic/Customization.cs` (catalog, owning, fitting), `JeepneyBuilder` (paint, banner,
sticker, sign, mudflap print, ornaments, lights), `SoundBank` (horns), `UIManager.Talyer.cs`.
Textures: `cos_*` in `scripts/unity-textures.mjs`. Server: `20261126104930_jr_cosmetics.sql`
and the catalog `20261126104932_jr_catalog.sql`. Tests: `CustomizationTests`, `dev-tests/cosmetics.sql`.
