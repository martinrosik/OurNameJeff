using System.Collections.Generic;
using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// OUR-38 / OUR-39: a short message over the island when a building changes state during the wave:
    /// <list type="bullet">
    /// <item>BOTTLENECK: red "! Beach Bar queue growing" with a guest quote ("Drinks takes a bit too long.");</item>
    /// <item>RECOVERING: green "Beach Bar is catching up", in the same frame as the Worker move that caused it.</item>
    /// </list>
    /// It sits in the top bar, fades after a few seconds. Never a countdown or a satisfaction number (Build doc §4.4).
    /// </summary>
    internal sealed class StateToastView
    {
        private const float ShowSeconds = 3f;
        private const float FadeSeconds = 0.5f;
        private const float Width = 290f;

        // In the free part of the top bar, right of the status pill, so it never covers a building's name.
        private static readonly Vector2 Position = new Vector2(244f, 39f);

        private readonly CanvasGroup _group;
        private readonly Image _panel;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _quote;
        private readonly Dictionary<NeedId, NeedState> _lastStates = new Dictionary<NeedId, NeedState>();
        private ResortGame _game;
        private float _age = float.MaxValue;

        public StateToastView(RectTransform islandArea)
        {
            _panel = UIFactory.Panel("StateToast", islandArea, GreyboxPalette.Warning);
            _panel.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), Position, new Vector2(Width, 36f));
            _panel.raycastTarget = false;
            _group = _panel.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _title = UIFactory.Text("Title", _panel.transform, string.Empty, 12, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            _title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(Width - 10f, 16f));
            _quote = UIFactory.Text("Quote", _panel.transform, string.Empty, 10, Color.white, TextAlignmentOptions.Center, FontStyles.Italic);
            _quote.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -19f), new Vector2(Width - 10f, 14f));

            _panel.gameObject.SetActive(false);
        }

        public bool IsVisible => _panel.gameObject.activeSelf;
        public string Title => _title.text;
        public string Quote => _quote.text;

        public void Refresh(ResortGame game)
        {
            if (game != _game)
            {
                _game = game;
                _lastStates.Clear();
                Hide();
            }

            foreach (BuildingRuntime building in game.Buildings)
            {
                NeedState state = game.GetNeedState(building.Need);
                _lastStates.TryGetValue(building.Need, out NeedState last);
                _lastStates[building.Need] = state;
                if (state == last || game.Phase != GamePhase.WaveActive) continue;

                if (state == NeedState.Bottleneck)
                {
                    Show(GameText.QueueGrowing(game, building.Id), GameText.GuestComplaint(game, building.Need), GreyboxPalette.Warning);
                }
                else if (state == NeedState.Recovering && last == NeedState.Bottleneck)
                {
                    Show(GameText.CatchingUp(game, building.Id), string.Empty, GreyboxPalette.WorkerOn);
                }
            }

            if (!IsVisible) return;
            _age += Time.deltaTime;
            _group.alpha = Mathf.Clamp01((ShowSeconds + FadeSeconds - _age) / FadeSeconds);
            if (_age >= ShowSeconds + FadeSeconds) Hide();
        }

        private void Show(string title, string quote, Color color)
        {
            _title.text = title;
            _quote.text = quote;
            _quote.gameObject.SetActive(!string.IsNullOrEmpty(quote));
            _panel.color = color;
            _panel.rectTransform.sizeDelta = new Vector2(Width, string.IsNullOrEmpty(quote) ? 22f : 36f);
            _group.alpha = 1f;
            _age = 0f;
            _panel.gameObject.SetActive(true);
        }

        private void Hide()
        {
            _panel.gameObject.SetActive(false);
            _age = float.MaxValue;
        }
    }
}
