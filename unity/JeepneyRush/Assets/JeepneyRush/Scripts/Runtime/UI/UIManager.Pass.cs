using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>Pasada Pass: this month's 30 tiers with a free and a premium reward each.</summary>
    public partial class UIManager
    {
        private VisualElement _pass, _passBody;
        private static readonly Color PassBlue = new Color32(0x0B, 0x25, 0x45, 0xFF);

        private void BuildPass()
        {
            _pass = ModalShell("pass", "PASADA PASS", out var card);
            var scroll = UiKit.Scroll(1250);
            scroll.style.height = 1250;
            _passBody = UiKit.Stack();
            scroll.Add(_passBody);
            card.Add(scroll);
            card.Add(UiKit.Button("BACK", CloseModal, Color.white, UiKit.Ink, 46, 120));
        }

        private void OpenPass()
        {
            if (!_modals.Contains(_pass)) OpenModal(_pass);
            _passBody.Clear();
            _passBody.Add(Note("Loading…"));
            PassProgress.Status(DrawPass);
        }

        private void DrawPass(PassInfo p, string err)
        {
            _passBody.Clear();
            if (p == null) { _passBody.Add(Note(err ?? "Could not load the pass.", 32, UiKit.Red)); return; }
            var month = new System.DateTime(p.season / 100, Mathf.Clamp(p.season % 100, 1, 12), 1);
            _passBody.Add(UiKit.Text($"{month:MMMM yyyy}".ToUpperInvariant() + $" · TIER {p.tier} / {PasadaPass.Tiers}", 38, PassBlue));
            int into = p.xp % PasadaPass.XpPerTier;
            _passBody.Add(Note(p.tier >= PasadaPass.Tiers ? "Max tier! Claim everything below." : $"{into} / {PasadaPass.XpPerTier} XP to tier {p.tier + 1} · {p.days_left} days left. Every run gives XP (distance + deliveries).", 26));
            if (!p.premium)
                _passBody.Add(UiKit.Button($"UNLOCK PREMIUM · {PasadaPass.PremiumGems} GEMS", () => PassProgress.BuyPremium((ok, msg, _) =>
                    { Toast(ok ? "Premium unlocked! Claim the premium rewards." : msg); if (ok) Services.Audio.Play(Logic.Audio.Sfx.Purchase); RefreshMenuTop(); OpenPass(); }), UiKit.Hex("#FCD116"), UiKit.Ink, 34, 104));
            for (int t = 1; t <= PasadaPass.Tiers; t++)
            {
                bool reached = t <= p.tier;
                var row = UiKit.Box();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = reached ? UiKit.Hex("#EEF3FF") : Color.white;
                UiKit.Border(row.style, 3, t == p.tier + 1 ? UiKit.Yellow : UiKit.Hex("#E2DBC6"), 16);
                UiKit.Pad(row.style, 6, 10);
                row.style.marginBottom = 6;
                var num = UiKit.Text(t.ToString(), 34, reached ? PassBlue : UiKit.Grey);
                num.style.width = 70;
                row.Add(num);
                row.Add(PassCell(t, false, reached, (p.claimed_free & (1L << t)) != 0, true));
                row.Add(PassCell(t, true, reached, (p.claimed_premium & (1L << t)) != 0, p.premium));
                _passBody.Add(row);
            }
        }

        private VisualElement PassCell(int tier, bool premium, bool reached, bool claimed, bool open)
        {
            var cell = UiKit.Box();
            cell.style.flexGrow = 1;
            cell.style.flexBasis = 0;
            cell.style.marginLeft = 6;
            cell.style.alignItems = Align.Center;
            cell.style.backgroundColor = premium ? UiKit.Hex("#FFF4C7") : UiKit.Hex("#F7F7F7");
            UiKit.Border(cell.style, 2, premium ? UiKit.Hex("#E0B23A") : UiKit.Hex("#DDDDDD"), 12);
            UiKit.Pad(cell.style, 4, 6);
            var label = UiKit.Text((premium ? "PREMIUM · " : "FREE · ") + PasadaPass.RewardText(tier, premium), 20, UiKit.Ink, false);
            label.style.whiteSpace = WhiteSpace.Normal;
            cell.Add(label);
            if (claimed) cell.Add(UiKit.Text("NAKUHA NA", 18, UiKit.Green, false));
            else if (reached && open)
            {
                var b = UiKit.Button("KUNIN", () => PassProgress.Claim(tier, premium, (ok, msg, coins) =>
                    { Toast(ok ? "Claimed!" : msg); if (ok) Services.Audio.Play(Logic.Audio.Sfx.Purchase); RefreshMenuTop(); OpenPass(); }), UiKit.Yellow, UiKit.Ink, 22, 64);
                b.style.width = 160;
                cell.Add(b);
            }
            else if (premium && !open) cell.Add(UiKit.Text("LOCKED", 18, UiKit.Grey, false));
            return cell;
        }
    }
}
