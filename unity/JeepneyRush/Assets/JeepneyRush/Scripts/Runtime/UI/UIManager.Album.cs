using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Sticker album: a page per route with its four landmark postcards.</summary>
    public partial class UIManager
    {
        private VisualElement _album, _albumBody;

        private void BuildAlbum()
        {
            _album = ModalShell("album", "STICKER ALBUM", out var card);
            var scroll = UiKit.Scroll(1250);
            scroll.style.height = 1250;
            _albumBody = UiKit.Stack();
            scroll.Add(_albumBody);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenAlbum()
        {
            if (!_modals.Contains(_album)) OpenModal(_album);
            _albumBody.Clear();
            _albumBody.Add(Note("Loading…"));
            AlbumProgress.Status(DrawAlbum);
        }

        private void DrawAlbum(AlbumInfo a, string err)
        {
            _albumBody.Clear();
            if (a == null) { _albumBody.Add(Note(err ?? "Could not load the album.", 32, UiKit.Red)); return; }
            var have = new System.Collections.Generic.HashSet<int>(a.cards);
            var sets = new System.Collections.Generic.HashSet<int>(a.sets);
            _albumBody.Add(Note($"{have.Count} / {Album.Count} postcards. Half of your runs hide one on the road after {Album.MinDistance:0} m – a landmark of the route you're driving. Duplicates pay {Album.DuplicateCoins} coins.", 26));
            for (int route = 0; route < Album.Routes; route++)
            {
                _albumBody.Add(Heading(Environments.Get((EnvironmentKind)route).Name.ToUpperInvariant()));
                var grid = UiKit.Box();
                grid.style.flexDirection = FlexDirection.Row;
                grid.style.flexWrap = Wrap.Wrap;
                grid.style.justifyContent = Justify.SpaceBetween;
                int got = 0;
                for (int i = 0; i < Album.PerRoute; i++)
                {
                    int c = Album.Card((EnvironmentKind)route, i);
                    bool owned = have.Contains(c);
                    if (owned) got++;
                    var tile = UiKit.Text(owned ? Album.Name(c).ToUpperInvariant() : "?", owned ? 22 : 40, owned ? UiKit.Ink : UiKit.Grey);
                    tile.style.whiteSpace = WhiteSpace.Normal;
                    tile.style.width = Length.Percent(48.5f);
                    tile.style.height = 120;
                    tile.style.marginBottom = 10;
                    tile.style.backgroundColor = owned ? UiKit.Hex("#FFF8E1") : UiKit.Hex("#EDEDED");
                    UiKit.Border(tile.style, 4, owned ? UiKit.Hex("#1F3FBF") : UiKit.Hex("#CCCCCC"), 16);
                    grid.Add(tile);
                }
                _albumBody.Add(grid);
                int set = route;
                if (sets.Contains(route)) _albumBody.Add(Note("SET COMPLETE · reward claimed", 24, UiKit.Green));
                else if (got == Album.PerRoute)
                    _albumBody.Add(UiKit.Button($"CLAIM SET · {Album.SetCoins} COINS + {Album.SetGems} GEMS", () => AlbumProgress.ClaimSet(set, (ok, msg, _) =>
                        { Toast(ok ? "Set complete!" : msg); if (ok) Services.Audio.Play(Logic.Audio.Sfx.Purchase); RefreshMenuTop(); OpenAlbum(); }), UiKit.Yellow, UiKit.Ink, 28, 90));
            }
            if (!sets.Contains(Album.Routes) && have.Count >= Album.Count)
                _albumBody.Add(UiKit.Button($"WHOLE ALBUM! CLAIM {Album.AlbumGems} GEMS", () => AlbumProgress.ClaimSet(Album.Routes, (ok, msg, _) =>
                    { Toast(ok ? "Kumpleto ang album!" : msg); RefreshMenuTop(); OpenAlbum(); }), UiKit.Hex("#C8102E"), Color.white, 32, 100));
        }
    }
}
