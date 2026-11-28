using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Main menu ▸ MODES: Daily Challenge, Rush Hour, Night Shift, Ghost Race.</summary>
    public sealed partial class UIManager
    {
        private VisualElement _modes;
        private Label _modesRoute;

        private void BuildModes()
        {
            _modes = ModalShell("modes", "MODES", out var card);
            _modesRoute = UiKit.Text("", 28, UiKit.Grey, false);
            _modesRoute.style.marginBottom = 8;
            card.Add(_modesRoute);
            card.Add(ModeRow("DAILY CHALLENGE", "Same road for everyone today – your best score is ranked.", UiKit.Hex("#FF8C42"), () => { CloseModal(); OpenDaily(); }));
            card.Add(ModeRow(GameModes.Name(GameMode.RushHour), GameModes.Blurb(GameMode.RushHour), UiKit.Hex("#E63946"), () => PlayModeFromMenu(GameMode.RushHour)));
            card.Add(ModeRow(GameModes.Name(GameMode.NightShift), GameModes.Blurb(GameMode.NightShift), UiKit.Hex("#2E3A87"), () => PlayModeFromMenu(GameMode.NightShift)));
            card.Add(ModeRow(GameModes.Name(GameMode.Ghost), GameModes.Blurb(GameMode.Ghost), UiKit.Hex("#7B61FF"), OpenGhost));
            card.Add(ModeRow("PASADA PASS", "Monthly pass: every run earns pass XP. 30 tiers of rewards; premium adds more.", UiKit.Hex("#0B2545"), () => { CloseModal(); OpenPass(); }));
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 44, 110));
        }

        private VisualElement ModeRow(string title, string blurb, Color col, System.Action go)
        {
            var row = UiKit.Box();
            row.pickingMode = PickingMode.Position;
            row.style.backgroundColor = Color.white;
            UiKit.Border(row.style, 5, col, 24);
            UiKit.Pad(row.style, 12, 18);
            row.style.marginBottom = 12;
            var t = UiKit.Text(title, 44, col, true, 1.5f);
            t.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(t);
            var b = UiKit.Text(blurb, 26, UiKit.Ink, false);
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.whiteSpace = WhiteSpace.Normal;
            row.Add(b);
            row.RegisterCallback<ClickEvent>(_ => { Services.Audio.Click(); go(); });
            return row;
        }

        private void OpenModes()
        {
            var env = (EnvironmentKind)Services.Save.Data.selectedEnvironment;
            _modesRoute.text = $"Route: {Environments.Get(env).Name} (change it in ROUTES) · Rankings: MODES tab";
            OpenModal(_modes);
        }

        private void PlayModeFromMenu(GameMode m)
        {
            _modals.Clear();
            HideAll();
            Services.Game.PlayMode(m);
        }
    }
}
