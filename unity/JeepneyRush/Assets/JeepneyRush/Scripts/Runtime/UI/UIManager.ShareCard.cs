using System;
using System.Collections;
using JeepneyRush.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace JeepneyRush
{
    /// <summary>
    /// Results ▸ I-SHARE ANG SCORE: a score card over the frozen last moment of the run (logo on
    /// top, the road in a yellow frame, the score panel below / beside), captured as a PNG and
    /// handed to Android's share sheet (see ShareService).
    /// </summary>
    public sealed partial class UIManager
    {
        private VisualElement _shareCard, _scPanel, _scWindow;
        private Label _scHead, _scScore, _scDetail, _scJeep, _scRank, _scChallenge;
        private VisualElement _scStars;
        private RunResult _lastResult;
        private bool _sharing;

        private void BuildShareCard()
        {
            _shareCard = UiKit.Box("shareCard");
            _shareCard.style.position = Position.Absolute;
            _shareCard.style.left = _shareCard.style.right = _shareCard.style.top = _shareCard.style.bottom = 0;
            _shareCard.pickingMode = PickingMode.Ignore;
            var top = UiKit.Box();
            top.style.backgroundColor = UiKit.Ink;
            top.style.alignItems = Align.Center;
            UiKit.Pad(top.style, 18, 24);
            top.Add(UiKit.Icon("logo", 520));
            _shareCard.Add(top);
            var body = _scWindow = UiKit.Box();
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Column;
            _shareCard.Add(body);
            var window = UiKit.Box("window");
            window.style.flexGrow = 1;
            UiKit.Border(window.style, 12, UiKit.Yellow, 0);
            body.Add(window);
            var panel = _scPanel = UiKit.Box("panel");
            panel.style.backgroundColor = UiKit.Cream;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;
            UiKit.Pad(panel.style, 26, 30);
            panel.style.borderTopWidth = 10;
            panel.style.borderTopColor = UiKit.Ink;
            _scHead = UiKit.Text("", 60, UiKit.Red, true, 2.5f);
            _scScore = UiKit.Text("", 150, UiKit.Blue, true, 4f);
            var pts = UiKit.Text("PUNTOS", 30, UiKit.Grey, false);
            _scStars = UiKit.Box("stars");
            _scStars.style.flexDirection = FlexDirection.Row;
            _scStars.style.marginTop = 4;
            _scDetail = UiKit.Text("", 34, UiKit.Ink, false);
            _scJeep = UiKit.Text("", 30, UiKit.Ink, false);
            _scRank = UiKit.Text("", 32, UiKit.Blue, true);
            _scChallenge = UiKit.Text("", 72, UiKit.Red, true, 3f);
            _scChallenge.style.marginTop = 10;
            var foot = UiKit.Text("JEEPNEY RUSH · I-download sa Google Play", 26, UiKit.Grey, false);
            foot.style.marginTop = 6;
            foreach (var l in new[] { _scHead, _scScore, pts, _scDetail, _scJeep, _scRank, _scChallenge, foot })
            {
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                panel.Add(l);
                if (l == pts) panel.Add(_scStars);
            }
            body.Add(panel);
            _shareCard.style.display = DisplayStyle.None;
            _root.Add(_shareCard);
        }

        private ScoreCardInfo CardInfo(RunResult r)
        {
            var c = new ScoreCardInfo
            {
                Score = r.Score, Distance = r.Distance, Route = r.Environment, Jeepney = r.Jeepney, Mode = r.Mode,
                Ghost = r.GhostName, BeatGhost = r.BeatGhost, NewBest = r.NewBestScore,
            };
            var car = r.Rewards?.Career;
            if (car != null) { c.Shift = car.Shift.Title; c.Stars = car.Stars; c.ShiftDone = car.Finished; }
            string online = _goOnline != null ? _goOnline.text : null;
            if (!string.IsNullOrEmpty(online) && online.Contains("#")) c.Rank = online;
            return c;
        }

        /// <summary>Builds the card over the last frame of the run, captures it and opens the share sheet.</summary>
        public void ShareResult()
        {
            if (_sharing || _shareCard == null) return;
            Services.Audio?.Click();
            StartCoroutine(ShareRoutine());
        }

        private IEnumerator ShareRoutine()
        {
            _sharing = true;
            var c = CardInfo(_lastResult);
            var j = JeepneyCatalog.Get(c.Jeepney);
            _scHead.text = ScoreCard.Headline(c);
            _scScore.text = c.Score.ToString("N0");
            _scStars.Clear();
            if (c.Shift != null)
                for (int i = 0; i < 3; i++)
                {
                    var st = UiKit.Icon("ui_star", 64);
                    st.style.opacity = i < c.Stars ? 1f : 0.25f;
                    _scStars.Add(st);
                }
            _scStars.style.display = c.Shift != null ? DisplayStyle.Flex : DisplayStyle.None;
            _scDetail.text = $"{(c.Distance >= 1000 ? $"{c.Distance / 1000f:0.0} km" : $"{c.Distance:0} m")} · {ScoreCard.Detail(c)}";
            _scJeep.text = $"{j.DisplayName} · {JeepneyClasses.Name(JeepneyClasses.For(j))} CLASS";
            _scRank.text = c.Rank ?? "";
            _scRank.style.display = string.IsNullOrEmpty(c.Rank) ? DisplayStyle.None : DisplayStyle.Flex;
            _scChallenge.text = ScoreCard.Challenge(c);
            // landscape: the panel sits beside the road instead of under it
            _scWindow.style.flexDirection = _landscapeUi ? FlexDirection.Row : FlexDirection.Column;
            _scPanel.style.width = _landscapeUi ? Length.Percent(42f) : StyleKeyword.Auto;
            _scPanel.style.borderTopWidth = _landscapeUi ? 0 : 10;
            _scPanel.style.borderLeftWidth = _landscapeUi ? 10 : 0;
            _scPanel.style.borderLeftColor = UiKit.Ink;
            // only the card on screen
            bool go = _gameOver.style.display == DisplayStyle.Flex;
            Show(_gameOver, false);
            if (_fxLayer != null) _fxLayer.style.display = DisplayStyle.None;
            if (_floats != null) _floats.style.display = DisplayStyle.None;
            _shareCard.style.display = DisplayStyle.Flex;
            yield return null;
            yield return new WaitForEndOfFrame();
            byte[] png = null;
            Texture2D shot = null, rgb = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                rgb = new Texture2D(shot.width, shot.height, TextureFormat.RGB24, false);
                rgb.SetPixels(shot.GetPixels());
                rgb.Apply(false);
                png = rgb.EncodeToPNG();
            }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                if (shot != null) Destroy(shot);
                if (rgb != null) Destroy(rgb);
            }
            _shareCard.style.display = DisplayStyle.None;
            if (_fxLayer != null) _fxLayer.style.display = DisplayStyle.Flex;
            if (_floats != null) _floats.style.display = DisplayStyle.Flex;
            if (go) Show(_gameOver, true);
            if (png != null)
            {
                string msg = ShareService.ShareImage(png, $"JeepneyRush_{DateTime.Now:yyyyMMdd_HHmmss}.png", ScoreCard.ShareText(c, Application.identifier));
                if (!string.IsNullOrEmpty(msg)) Toast(msg);
            }
            else Toast("Hindi nagawa ang score card.");
            _sharing = false;
        }
    }
}
