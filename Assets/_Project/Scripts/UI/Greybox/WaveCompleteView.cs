using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// OUR-40 / UI doc S4 Wave Complete: a card in the middle of the dimmed screen once the wave is over.
    /// One main button, Continue, returns to the island with the next wave's forecast.
    /// Stars, rewards and "What went well" join the card in a later sprint.
    /// </summary>
    internal sealed class WaveCompleteView
    {
        private const float CardWidth = 340f;
        private const float CardHeight = 176f;
        private const float ContentWidth = 316f;

        private readonly GameObject _root;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _message;
        private readonly TextMeshProUGUI _guestsServed;
        private readonly Button _continue;

        public WaveCompleteView(GreyboxUI owner, Transform canvas)
        {
            Image backdrop = UIFactory.Backdrop("WaveComplete", canvas);
            _root = backdrop.gameObject;

            var center = new Vector2(0.5f, 0.5f);
            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);

            Image card = UIFactory.Panel("Card", backdrop.transform, UIFactory.PanelBackground);
            card.rectTransform.At(center, center, Vector2.zero, new Vector2(CardWidth, CardHeight));

            _title = UIFactory.Text("Title", card.transform, string.Empty, 17, UIFactory.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            _title.rectTransform.At(top, top, new Vector2(0f, -12f), new Vector2(ContentWidth, 24f));

            _message = UIFactory.Text("Message", card.transform, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.Center);
            _message.rectTransform.At(top, top, new Vector2(0f, -38f), new Vector2(ContentWidth, 20f));

            Image row = UIFactory.Panel("GuestsServedRow", card.transform, UIFactory.RowBackground);
            row.rectTransform.At(top, top, new Vector2(0f, -66f), new Vector2(ContentWidth, 26f));
            TextMeshProUGUI label = UIFactory.Text("Label", row.transform, GameText.GuestsServedLabel, 13, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            label.rectTransform.Fill(8, 80, 0, 0);
            _guestsServed = UIFactory.Text("Value", row.transform, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.MidlineRight, FontStyles.Bold);
            _guestsServed.rectTransform.Fill(80, 8, 0, 0);

            _continue = UIFactory.Button("ContinueButton", card.transform, GameText.ContinueLabel, GreyboxPalette.Primary, Color.white, 17, owner.ContinueAfterWave);
            ((RectTransform)_continue.transform).At(bottom, bottom, new Vector2(0f, 12f), new Vector2(240f, 46f));

            _root.SetActive(false);
        }

        public bool IsVisible => _root.activeSelf;
        public string TitleText => _title.text;
        public string MessageText => _message.text;
        public string GuestsServedText => _guestsServed.text;
        public Button ContinueButton => _continue;

        public void Refresh(ResortGame game)
        {
            bool isVisible = game.Phase == GamePhase.WaveComplete;
            _root.SetActive(isVisible);
            if (!isVisible) return;

            WaveResult result = game.LastWaveResult;
            _title.text = GameText.WaveCompleteTitle(result.WaveNumber);
            _message.text = GameText.WaveCompleteMessage(result);
            _guestsServed.text = GameText.GuestsServed(result);
        }
    }
}
