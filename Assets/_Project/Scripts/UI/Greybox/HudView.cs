using System.Collections.Generic;
using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// S1 Home / Island View (UI doc §A):
    /// <list type="bullet">
    /// <item>resort status in the top bar ("READY FOR GUESTS", "BOAT ARRIVING", "WAVE 1 IN PROGRESS");</item>
    /// <item><b>OUR-33</b>: the next-wave forecast on the side panel before the wave, with which buildings have a Worker;</item>
    /// <item><b>OUR-27</b>: Welcome Guests starts the wave manually.</item>
    /// </list>
    /// Currencies (Coins, Energy, Resort Value) join the top bar in a later sprint; leave room on the right.
    /// </summary>
    internal sealed class HudView
    {
        private const int ForecastRows = 5;

        private readonly GreyboxUI _owner;
        private readonly TextMeshProUGUI _status;
        private readonly GameObject _forecastPanel;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _subtitle;
        private readonly List<(GameObject row, TextMeshProUGUI need, TextMeshProUGUI building)> _rows =
            new List<(GameObject, TextMeshProUGUI, TextMeshProUGUI)>();
        private readonly TextMeshProUGUI _note;
        private readonly Button _startWave;
        private readonly TextMeshProUGUI _hint;

        public HudView(GreyboxUI owner, RectTransform root, RectTransform sideArea, RectTransform islandArea)
        {
            _owner = owner;

            RectTransform topBar = UIFactory.Rect("TopBar", root);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.offsetMin = new Vector2(6f, -36f);
            topBar.offsetMax = new Vector2(-6f, -4f);

            Image pill = UIFactory.Panel("Status", topBar, UIFactory.Paper);
            pill.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(230f, 28f));
            Image accent = UIFactory.Panel("Accent", pill.transform, GreyboxPalette.Primary);
            accent.rectTransform.At(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(8f, 16f));
            _status = UIFactory.Text("Label", pill.transform, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _status.rectTransform.Fill(20, 8, 0, 0);

            Image panel = UIFactory.SidePanel("ForecastPanel", sideArea);
            _forecastPanel = panel.gameObject;

            _title = UIFactory.Text("Title", panel.transform, string.Empty, 17, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            _title.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(276f, 24f));
            _subtitle = UIFactory.Text("Subtitle", panel.transform, string.Empty, 13, UIFactory.Ink);
            _subtitle.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -32f), new Vector2(276f, 20f));

            for (int i = 0; i < ForecastRows; i++)
            {
                Image row = UIFactory.Panel($"ForecastRow{i}", panel.transform, UIFactory.RowBackground);
                row.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -60f - i * 30f), new Vector2(280f, 26f));
                TextMeshProUGUI need = UIFactory.Text("Need", row.transform, string.Empty, 13, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                need.rectTransform.Fill(8, 120, 0, 0);
                TextMeshProUGUI building = UIFactory.Text("Building", row.transform, string.Empty, 11, UIFactory.Ink, TextAlignmentOptions.MidlineRight);
                building.rectTransform.Fill(110, 8, 0, 0);
                _rows.Add((row.gameObject, need, building));
            }

            _note = UIFactory.Text("Note", panel.transform, string.Empty, 11, UIFactory.Ink.WithAlpha(0.75f), TextAlignmentOptions.TopLeft);
            _note.textWrappingMode = TextWrappingModes.Normal;
            _note.rectTransform.At(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 12f), new Vector2(276f, 44f));

            _startWave = UIFactory.Button("StartWaveButton", islandArea, "WELCOME GUESTS", GreyboxPalette.Primary, Color.white, 17, OnStartWave);
            ((RectTransform)_startWave.transform).At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(240f, 46f));

            _hint = UIFactory.Text("Hint", islandArea, "Check what the guests will need. Tap a building to move a Worker.", 11, Color.white, TextAlignmentOptions.Center);
            _hint.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(420f, 16f));
        }

        public Button StartWaveButton => _startWave;
        public bool ForecastVisible => _forecastPanel.activeSelf;
        public string StatusText => _status.text;

        public int VisibleForecastRows
        {
            get
            {
                int count = 0;
                foreach ((GameObject row, TextMeshProUGUI _, TextMeshProUGUI _) in _rows)
                {
                    if (row.activeSelf) count++;
                }

                return count;
            }
        }

        public void Refresh(ResortGame game)
        {
            _status.text = GameText.Status(game);

            bool awaiting = game.Phase == GamePhase.AwaitingWave;
            _forecastPanel.SetActive(awaiting);
            _startWave.gameObject.SetActive(awaiting && game.HasNextWave);
            _hint.gameObject.SetActive(awaiting && game.HasNextWave);

            if (awaiting)
            {
                ShowForecast(game);
            }
        }

        /// <summary>OUR-33: what the next wave's guests will need, and which buildings have a Worker.</summary>
        private void ShowForecast(ResortGame game)
        {
            WaveForecast forecast = game.GetNextWaveForecast();
            if (forecast == null)
            {
                _title.text = "ALL WAVES PLAYED";
                _subtitle.text = string.Empty;
                HideRowsFrom(0);
                _note.text = string.Empty;
                return;
            }

            _startWave.SetLabel($"WELCOME GUESTS\n<size=65%>Wave {forecast.WaveNumber}</size>");
            _title.text = $"NEXT: WAVE {forecast.WaveNumber}";
            _subtitle.text = $"{forecast.Guests} guests are coming";

            int index = 0;
            foreach (NeedDemand demand in forecast.Demand)
            {
                if (index >= _rows.Count) break;

                BuildingDefinition definition = game.Config.GetBuildingForNeed(demand.Need);
                BuildingRuntime building = definition != null ? game.GetBuilding(definition.building) : null;
                bool staffed = building != null && building.HasWorker;

                (GameObject row, TextMeshProUGUI need, TextMeshProUGUI place) = _rows[index];
                row.SetActive(true);
                need.text = $"{GameText.Need(game, demand.Need)}  {demand.Units}";
                place.text = staffed
                    ? $"{definition.displayName}: Worker"
                    : $"<color=#{ColorUtility.ToHtmlStringRGB(GreyboxPalette.Warning)}>{definition?.displayName}: No Worker</color>";
                index++;
            }

            HideRowsFrom(index);
            _note.text = "Each number is how many guests will need that service. One building always stays without a Worker.";
        }

        private void HideRowsFrom(int index)
        {
            for (int i = index; i < _rows.Count; i++)
            {
                _rows[i].row.SetActive(false);
            }
        }

        private void OnStartWave() => _owner.StartWave();
    }
}
