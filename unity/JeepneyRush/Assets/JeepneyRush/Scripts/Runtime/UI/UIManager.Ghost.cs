using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// MODES ▸ GHOST RACE: fetches a rival's ghost on your route in your jeepney's stat class (the
    /// closest score above yours) and races it on the same road. Every free ride records your own
    /// ghost for others to race.
    /// </summary>
    public sealed partial class UIManager
    {
        private VisualElement _ghost, _ghostCard;

        private void OpenGhost()
        {
            var o = Services.Online;
            if (o == null || !o.Configured) { Float("Ghost Race needs the online backend.", FloatKind.Bad); return; }
            if (!o.SignedIn) { Float("Sign in (or play as guest) under ACCOUNT to race ghosts.", FloatKind.Info); return; }
            var d = Services.Save.Data;
            var env = (EnvironmentKind)d.selectedEnvironment;
            var cls = JeepneyClasses.For(d.selectedJeepney);
            if (_ghost == null) _ghost = ModalShell("ghost", "GHOST RACE", out _ghostCard);
            _ghostCard.Clear();
            var title = UiKit.Text("GHOST RACE", 84, UiKit.Hex("#7B61FF"), true, 2.5f);
            title.style.marginBottom = 12;
            _ghostCard.Add(title);
            var info = UiKit.Text($"{Environments.Get(env).Name} · {JeepneyClasses.Name(cls)} class\nHinahanap ang kalaban…", 32, UiKit.Ink, false);
            info.style.whiteSpace = WhiteSpace.Normal;
            _ghostCard.Add(info);
            _ghostCard.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 44, 110));
            OpenModal(_ghost);
            o.GhostPick(env, cls, (g, err) =>
            {
                if (g == null) { info.text = err ?? "Could not reach the server."; return; }
                if (!g.found)
                {
                    info.text = $"Wala pang ghost sa {Environments.Get(env).Name} for {JeepneyClasses.Name(cls)} jeepneys.\nPlay free rides: your runs become ghosts for other drivers to race!";
                    return;
                }
                info.text = $"KALABAN: {g.name}\n{g.score:N0} points · {g.distance:N0} m on the same road\nBeat their score to win. Their jeepney is the see-through one.";
                var go = UiKit.Button("KARERA!", () => { _modals.Clear(); HideAll(); Services.Game.PlayGhost(g); }, UiKit.Hex("#7B61FF"), Color.white, 60, 150);
                _ghostCard.Insert(_ghostCard.childCount - 1, go);
            });
        }
    }
}
