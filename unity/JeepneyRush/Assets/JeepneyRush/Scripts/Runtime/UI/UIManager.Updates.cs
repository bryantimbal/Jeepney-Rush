using System;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>"May bagong version!" / "Kailangan ng update" dialogs (see UpdateChecker).</summary>
    public sealed partial class UIManager
    {
        private VisualElement _update, _updateCard;

        /// <param name="update">what UPDATE does (default: open the Play Store page)</param>
        /// <param name="later">MAMAYA NA (optional updates only)</param>
        public void ShowUpdate(UpdateNeed need, AppVersionInfo v, Action later = null, Action update = null)
        {
            if (need == UpdateNeed.None) return;
            if (_update == null) _update = ModalShell("update", "UPDATE", out _updateCard);
            _updateCard.Clear();
            var title = UiKit.Text(AppUpdates.Title(need), 58, need == UpdateNeed.Required ? UiKit.Red : UiKit.Green, true, 2f);
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.marginBottom = 12;
            _updateCard.Add(title);
            var text = UiKit.Text(AppUpdates.Text(need, v), 32, UiKit.Ink, false);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.marginBottom = 12;
            _updateCard.Add(text);
            _updateCard.Add(UiKit.Button("UPDATE", () =>
            {
                if (update != null) { Show(_update, false); update(); }
                else UpdateChecker.OpenStore(); // a required update keeps the dialog up
                if (need == UpdateNeed.Optional && update == null) Show(_update, false);
            }, UiKit.Green, Color.white, 58, 150));
            if (need == UpdateNeed.Optional)
                _updateCard.Add(UiKit.Button("MAMAYA NA", () => { Show(_update, false); later?.Invoke(); }, Color.white, UiKit.Ink, 44, 110));
            // a required update blocks the game (it's not in the modal stack, so Back doesn't close it)
            _update.BringToFront();
            Show(_update, true);
            UiKit.PopIn(_updateCard);
        }

        /// <summary>A flexible update finished downloading: restart to install it.</summary>
        public void ShowUpdateReady(Action restart)
        {
            if (_update == null) _update = ModalShell("update", "UPDATE", out _updateCard);
            _updateCard.Clear();
            _updateCard.Add(UiKit.Text("HANDA NA ANG UPDATE!", 56, UiKit.Green, true, 2f));
            var t = UiKit.Text("The new version is downloaded. Restart the game to install it (takes a few seconds).", 32, UiKit.Ink, false);
            t.style.whiteSpace = WhiteSpace.Normal;
            _updateCard.Add(t);
            _updateCard.Add(UiKit.Button("I-RESTART", () => { Show(_update, false); restart(); }, UiKit.Green, Color.white, 58, 150));
            _updateCard.Add(UiKit.Button("MAMAYA NA", () => Show(_update, false), Color.white, UiKit.Ink, 44, 110));
            _update.BringToFront();
            Show(_update, true);
        }
    }
}
