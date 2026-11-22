using ResortEmpire.Core;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>
    /// Settings: a gear button in the top-right corner opens a small menu under it with
    /// <list type="bullet">
    /// <item>Sound ON / OFF, which the device remembers (<see cref="SoundSettings"/>);</item>
    /// <item>PLAY TUTORIAL AGAIN, which opens the Tutorial scene at any time, also during a wave. The game waits
    /// frozen meanwhile (<see cref="ParkedGame"/>) and the player comes back to it exactly as they left it.</item>
    /// </list>
    /// The wave waits while the menu is open (timeScale 0), so nothing happens behind it. A tap outside the menu, on
    /// CLOSE, or on the gear again closes it. The Tutorial scene hides the gear (<see cref="GreyboxUI.SettingsAvailable"/>).
    /// </summary>
    internal sealed class SettingsView
    {
        private const float MenuWidth = 250f;
        private const float RowHeight = 32f;
        private const float ButtonHeight = 34f;
        private static readonly Vector2 GearButtonSize = new Vector2(32f, 28f);

        private readonly GreyboxUI _owner;
        private readonly Button _gear;
        private readonly GameObject _root;
        private readonly Button _sound;
        private readonly Image _soundBackground;
        private readonly TextMeshProUGUI _soundLabel;
        private readonly Button _tutorial;
        private readonly TextMeshProUGUI _tutorialNote;
        private float _timeScaleBeforeOpen = 1f;
        private bool _isOpen;

        public SettingsView(GreyboxUI owner, RectTransform root, Sprite gearIcon)
        {
            _owner = owner;

            // The gear sits in the top bar's right corner, level with the status pill.
            _gear = UIFactory.Button("SettingsButton", root, string.Empty, UIFactory.Paper, GreyboxPalette.Primary, 12, Toggle);
            ((RectTransform)_gear.transform).At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), GearButtonSize);
            Image gear = UIFactory.Icon("Gear", _gear.transform, gearIcon, GreyboxPalette.Primary);
            gear.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));

            Image backdrop = UIFactory.Backdrop("Settings", root);
            backdrop.color = Color.black.WithAlpha(0.2f);
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(Close);
            _root = backdrop.gameObject;

            Image menu = UIFactory.Panel("Menu", backdrop.transform, UIFactory.Paper);
            menu.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // taps on the menu don't close it
            RectTransform menuRect = menu.rectTransform;

            TextMeshProUGUI title = UIFactory.Text("Title", menuRect, GameText.SettingsTitle, 15, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            title.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(MenuWidth - 100f, 24f));

            Button close = UIFactory.Button("CloseButton", menuRect, GameText.CloseLabel, Color.white, UIFactory.Ink, 11, Close);
            ((RectTransform)close.transform).At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(70f, 24f));

            float y = 40f;
            Image soundRow = UIFactory.Panel("SoundRow", menuRect, UIFactory.RowBackground);
            soundRow.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(MenuWidth - 20f, RowHeight));
            TextMeshProUGUI soundName = UIFactory.Text("Label", soundRow.transform, GameText.SoundLabel, 13, UIFactory.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            soundName.rectTransform.Fill(10f, 80f, 0f, 0f);
            _sound = UIFactory.Button("SoundButton", soundRow.transform, GameText.SoundOnLabel, GreyboxPalette.Primary, Color.white, 12, ToggleSound);
            ((RectTransform)_sound.transform).At(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(64f, 24f));
            _soundBackground = (Image)_sound.targetGraphic;
            _soundLabel = _sound.GetComponentInChildren<TextMeshProUGUI>();
            y += RowHeight + 8f;

            _tutorial = UIFactory.Button("TutorialButton", menuRect, GameText.TutorialReplayLabel, GreyboxPalette.PanelBlue, UIFactory.Ink, 13, PlayTutorial);
            ((RectTransform)_tutorial.transform).At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -y), new Vector2(MenuWidth - 20f, ButtonHeight));
            y += ButtonHeight + 4f;

            _tutorialNote = UIFactory.Text("TutorialNote", menuRect, GameText.TutorialReplayNote, 11, UIFactory.Ink.WithAlpha(0.75f), TextAlignmentOptions.TopLeft);
            _tutorialNote.textWrappingMode = TextWrappingModes.Normal;
            _tutorialNote.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -y), new Vector2(MenuWidth - 24f, 28f));
            y += 28f + 6f;

            // Under the gear, right-aligned with it.
            menuRect.At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f - GearButtonSize.y - 4f), new Vector2(MenuWidth, y));

            _root.SetActive(false);
        }

        public bool IsOpen => _isOpen;
        public Button GearButton => _gear;
        public Button SoundButton => _sound;
        public Button TutorialButton => _tutorial;
        public string SoundText => _soundLabel.text;
        public string TutorialNoteText => _tutorialNote.text;

        public void Open()
        {
            if (_isOpen || !_owner.SettingsAvailable) return;

            _isOpen = true;
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = 0f; // the wave waits while the menu is open
            Refresh(_owner.Game);
        }

        /// <summary>Closes the menu and lets the wave run again. Safe while the scene is being torn down.</summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            if (_root != null) _root.SetActive(false);
            Time.timeScale = _timeScaleBeforeOpen;
        }

        /// <summary>Every frame: the gear shows where settings are available; the open menu shows the current state.</summary>
        public void Refresh(ResortGame game)
        {
            _gear.gameObject.SetActive(_owner.SettingsAvailable);
            if (!IsOpen) return;
            if (!_owner.SettingsAvailable)
            {
                Close();
                return;
            }

            bool muted = SoundSettings.IsMuted;
            _soundLabel.text = muted ? GameText.SoundOffLabel : GameText.SoundOnLabel;
            _soundBackground.color = muted ? UIFactory.Ink.WithAlpha(0.35f) : GreyboxPalette.Primary;
        }

        private void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void ToggleSound()
        {
            SoundSettings.SetMuted(!SoundSettings.IsMuted);
            Refresh(_owner.Game);
        }

        private void PlayTutorial()
        {
            Close();
            _owner.OpenTutorial();
        }
    }
}
