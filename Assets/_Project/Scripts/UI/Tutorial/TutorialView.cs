using System;
using System.Collections.Generic;
using ResortEmpire.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ResortEmpire.UI
{
    /// <summary>What one tutorial bubble shows and where it points, in the tutorial canvas's safe-area space.</summary>
    internal struct TutorialBubble
    {
        public string Title;
        public string Body;

        /// <summary>Shows NEXT, and a light dim that stops taps on the game until it's pressed.</summary>
        public bool WaitsForNext;

        /// <summary>A small note without the host or a button, while the game runs on.</summary>
        public bool IsHint;

        /// <summary>What the arrow points at and the highlight frame goes around (null: nothing).</summary>
        public Rect? Target;

        /// <summary>Areas the bubble must not cover, besides the target.</summary>
        public List<Rect> Avoid;

        /// <summary>Where the bubble goes without a target.</summary>
        public Vector2 Fallback;
    }

    /// <summary>
    /// The tutorial's overlay, built from code on a canvas above the greybox UI: a sand-coloured speech bubble with the
    /// resort host, a curved arrow to the target, a pulsing frame around it, and SKIP TUTORIAL. Placeholder look,
    /// replaced with the Figma pass like the rest of the greybox UI. Animations run on unscaled time, so they keep
    /// moving while the tutorial pauses the wave (timeScale 0).
    /// </summary>
    internal sealed class TutorialView
    {
        private static readonly float[] BubbleWidths = { 290f, 250f, 215f, 185f };
        private static readonly float[] HintWidths = { 250f };
        private const float MinWidthWithHost = 240f;
        private const float Padding = 12f;
        private const float HostSize = 50f;
        private const float TitleHeight = 22f;
        private const float NextHeight = 30f;
        private const float NextWidth = 96f;
        private const float FramePadding = 6f;
        private const float AppearSeconds = 0.18f;
        private const float NudgeSeconds = 0.4f;
        private const float NudgeDistance = 7f;

        private static readonly Color Border = GreyboxPalette.Primary;
        private static readonly Color BubbleColor = GreyboxPalette.Paper;
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.22f);
        private static readonly Vector2 ShadowOffset = new Vector2(1.5f, -2.5f);

        private readonly RectTransform _safeArea;
        private readonly Image _blocker;
        private readonly Image _frame;
        private readonly RectTransform _arrowRoot;
        private readonly Image _arrow;
        private readonly Image _arrowShadow;
        private readonly RectTransform _bubble;
        private readonly Image _bubbleFill;
        private readonly RectTransform _host;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _body;
        private readonly Button _next;
        private readonly Button _skip;

        private Vector2 _arrowFrom;
        private Vector2 _arrowTo;
        private Vector2 _bubbleCenter;
        private string _shownBody;
        private float _appearedAt = float.MinValue;
        private float _nudgedAt = float.MinValue;

        public TutorialView(Transform parent, Sprite arrowSprite, Sprite frameSprite, Sprite hostSprite, Action onNext, Action onSkip)
        {
            var canvasObject = new GameObject("TutorialCanvas", typeof(RectTransform));
            canvasObject.layer = 5;
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // above GreyboxUI's canvas, Wave Complete included

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(852f, 393f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

            _blocker = UIFactory.Panel("Blocker", canvasObject.transform, Color.black.WithAlpha(0.18f), rounded: false);
            _blocker.rectTransform.Fill();

            _safeArea = UIFactory.Rect("SafeArea", canvasObject.transform).Fill();
            _safeArea.gameObject.AddComponent<SafeAreaFitter>();

            _frame = UIFactory.Icon("Frame", _safeArea, frameSprite, Border);
            _frame.preserveAspect = false;
            _frame.type = Image.Type.Sliced;
            _frame.pixelsPerUnitMultiplier = 2f;

            _arrowRoot = UIFactory.Rect("Arrow", _safeArea);
            _arrowShadow = UIFactory.Icon("Shadow", _arrowRoot, arrowSprite, ShadowColor);
            _arrow = UIFactory.Icon("Fill", _arrowRoot, arrowSprite, Border);
            foreach (Image image in new[] { _arrowShadow, _arrow })
            {
                image.preserveAspect = false;
                Vector2 tip = TutorialArt.ArrowTip;
                Vector2 start = TutorialArt.ArrowStart;
                var size = new Vector2(TutorialArt.ArrowWidth, TutorialArt.ArrowHeight);
                image.rectTransform.pivot = new Vector2((start.x + tip.x) * 0.5f / size.x, (start.y + tip.y) * 0.5f / size.y);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }

            _bubble = UIFactory.Rect("Bubble", _safeArea);
            _bubble.anchorMin = _bubble.anchorMax = new Vector2(0.5f, 0.5f);
            Image shadow = UIFactory.Panel("Shadow", _bubble, ShadowColor);
            shadow.rectTransform.Fill(-2f, -2f, 2f, -6f); // a soft drop below the bubble
            Image border = UIFactory.Panel("Border", _bubble, Border);
            border.rectTransform.Fill(-2f, -2f, -2f, -2f);
            _bubbleFill = UIFactory.Panel("Fill", _bubble, BubbleColor);
            _bubbleFill.rectTransform.Fill();
            _bubbleFill.raycastTarget = true; // taps on the bubble don't reach the island

            Image hostBackground = UIFactory.Panel("Host", _bubble, GreyboxPalette.PanelBlue);
            _host = hostBackground.rectTransform;
            _host.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Padding, -Padding), new Vector2(HostSize, HostSize));
            Image host = UIFactory.Icon("Portrait", _host, hostSprite, Color.white);
            host.rectTransform.Fill(4f, 4f, 2f, 0f);
            host.enabled = hostSprite != null;

            _title = UIFactory.Text("Title", _bubble, string.Empty, 15, UIFactory.Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            _title.textWrappingMode = TextWrappingModes.Normal;
            _title.overflowMode = TextOverflowModes.Overflow;
            _body = UIFactory.Text("Body", _bubble, string.Empty, 12.5f, UIFactory.Ink, TextAlignmentOptions.TopLeft);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Overflow;

            _next = UIFactory.Button("NextButton", _bubble, GameText.TutorialNextLabel, GreyboxPalette.Primary, Color.white, 13, onNext);

            _skip = UIFactory.Button("SkipButton", _safeArea, GameText.TutorialSkipLabel, UIFactory.Ink.WithAlpha(0.6f), Color.white, 11, onSkip);
            ((RectTransform)_skip.transform).At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), SkipSize);

            Hide();
        }

        public static readonly Vector2 SkipSize = new Vector2(112f, 26f);

        public RectTransform SafeArea => _safeArea;
        public Button NextButton => _next;
        public Button SkipButton => _skip;
        public bool IsBubbleVisible => _bubble.gameObject.activeSelf;
        public bool IsArrowVisible => _arrowRoot.gameObject.activeSelf;
        public bool IsFrameVisible => _frame.gameObject.activeSelf;
        public bool BlocksGame => _blocker.gameObject.activeSelf;
        public string TitleText => _title.text;
        public string BodyText => _body.text;

        /// <summary>How many times the bubble was shaken for a tap on the wrong thing.</summary>
        public int Nudges { get; private set; }

        /// <summary>The bubble's rect in safe-area space (valid while it's visible).</summary>
        public Rect BubbleRect => TutorialLayout.RectAt(_bubble.anchoredPosition, _bubble.sizeDelta);

        /// <summary>The safe area's rect in its own space, minus a small margin: where bubbles may go.</summary>
        public Rect Bounds
        {
            get
            {
                Rect rect = _safeArea.rect;
                return TutorialLayout.Expand(rect, -TutorialLayout.Margin);
            }
        }

        /// <summary>The SKIP button's rect in safe-area space, so bubbles stay clear of it.</summary>
        public Rect SkipRect
        {
            get
            {
                Rect rect = _safeArea.rect;
                return new Rect(rect.xMax - 8f - SkipSize.x, rect.yMax - 8f - SkipSize.y, SkipSize.x, SkipSize.y);
            }
        }

        public void SetSkipVisible(bool visible) => _skip.gameObject.SetActive(visible);

        public void Show(TutorialBubble bubble)
        {
            var avoid = new List<Rect> { SkipRect };
            if (bubble.Avoid != null) avoid.AddRange(bubble.Avoid);

            // The widest bubble that fits beside its target and clear of the rest; on a narrow screen a slimmer one,
            // which drops the host to keep room for the text. If none fits, the widest at the fallback spot.
            float[] widths = bubble.IsHint ? HintWidths : BubbleWidths;
            Vector2 size = Vector2.zero;
            Vector2 center = Vector2.zero;
            bool placed = false;
            foreach (float width in bubble.Target != null ? widths : new[] { widths[0] })
            {
                size = LayOut(bubble, width);
                placed = TutorialLayout.TryPlaceBubble(Bounds, size, bubble.Target, avoid, out center);
                if (placed) break;
            }

            if (!placed)
            {
                size = LayOut(bubble, widths[0]);
                center = TutorialLayout.PlaceBubble(Bounds, size, bubble.Target, avoid, bubble.Fallback);
            }

            _bubble.sizeDelta = size;
            _bubble.anchoredPosition = center;
            _bubbleCenter = center;
            _bubble.gameObject.SetActive(true);
            _blocker.gameObject.SetActive(bubble.WaitsForNext);

            if (_shownBody != bubble.Body)
            {
                _shownBody = bubble.Body;
                _appearedAt = Time.unscaledTime;
            }

            ShowTarget(bubble.Target, TutorialLayout.RectAt(center, size));
        }

        /// <summary>Lays the bubble's contents out at <paramref name="width"/> and returns the bubble's size.</summary>
        private Vector2 LayOut(TutorialBubble bubble, float width)
        {
            bool hasTitle = !string.IsNullOrEmpty(bubble.Title) && !bubble.IsHint;
            bool hasHost = !bubble.IsHint && width >= MinWidthWithHost;
            float textLeft = Padding + (hasHost ? HostSize + 10f : 0f);
            float textWidth = width - textLeft - Padding;

            _host.gameObject.SetActive(hasHost);
            _title.gameObject.SetActive(hasTitle);
            _title.text = hasTitle ? bubble.Title : string.Empty;
            _body.text = bubble.Body;

            float y = Padding;
            if (hasTitle)
            {
                float titleHeight = Mathf.Max(TitleHeight, Mathf.Ceil(_title.GetPreferredValues(bubble.Title, textWidth, 0f).y) + 4f);
                _title.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textLeft, -y), new Vector2(textWidth, titleHeight));
                y += titleHeight;
            }

            float bodyHeight = Mathf.Ceil(_body.GetPreferredValues(bubble.Body, textWidth, 0f).y) + 2f;
            _body.rectTransform.At(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(textLeft, -y), new Vector2(textWidth, bodyHeight));
            y += bodyHeight;

            _next.gameObject.SetActive(bubble.WaitsForNext);
            if (bubble.WaitsForNext)
            {
                y += 8f;
                ((RectTransform)_next.transform).At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Padding, -y), new Vector2(NextWidth, NextHeight));
                y += NextHeight;
            }

            y += Padding;
            float height = hasHost ? Mathf.Max(y, HostSize + Padding * 2f) : y;
            return new Vector2(width, height);
        }

        public void Hide()
        {
            _bubble.gameObject.SetActive(false);
            _arrowRoot.gameObject.SetActive(false);
            _frame.gameObject.SetActive(false);
            _blocker.gameObject.SetActive(false);
            _shownBody = null;
        }

        /// <summary>A short shake of the bubble: that tap isn't the one it asks for.</summary>
        public void Nudge()
        {
            _nudgedAt = Time.unscaledTime;
            Nudges++;
        }

        /// <summary>The bubble pops in (and shakes after a nudge), the frame breathes and the arrow nudges toward its target.</summary>
        public void Animate(float time)
        {
            float appear = Mathf.Clamp01((time - _appearedAt) / AppearSeconds);
            float pop = 0.92f + 0.08f * (1f - (1f - appear) * (1f - appear));
            _bubble.localScale = new Vector3(pop, pop, 1f);

            float sinceNudge = time - _nudgedAt;
            float shake = sinceNudge >= 0f && sinceNudge < NudgeSeconds
                ? Mathf.Sin(sinceNudge * 45f) * NudgeDistance * (1f - sinceNudge / NudgeSeconds)
                : 0f;
            _bubble.anchoredPosition = _bubbleCenter + new Vector2(shake, 0f);

            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 4f);
            _frame.color = Border.WithAlpha(0.65f + 0.35f * pulse);
            _frame.rectTransform.localScale = Vector3.one * (1f + 0.025f * pulse);

            if (_arrowRoot.gameObject.activeSelf)
            {
                Vector2 direction = (_arrowTo - _arrowFrom).normalized;
                _arrowRoot.anchoredPosition = (_arrowFrom + _arrowTo) * 0.5f - direction * (3f * (1f - pulse));
            }
        }

        private void ShowTarget(Rect? target, Rect bubble)
        {
            bool hasTarget = target != null;
            _frame.gameObject.SetActive(hasTarget);
            if (!hasTarget)
            {
                _arrowRoot.gameObject.SetActive(false);
                return;
            }

            Rect frame = TutorialLayout.Expand(target.Value, FramePadding);
            _frame.rectTransform.anchorMin = _frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _frame.rectTransform.anchoredPosition = frame.center;
            _frame.rectTransform.sizeDelta = frame.size;

            bool hasArrow = TutorialLayout.TryGetArrow(bubble, frame, out _arrowFrom, out _arrowTo);
            _arrowRoot.gameObject.SetActive(hasArrow);
            if (!hasArrow) return;

            Vector2 delta = _arrowTo - _arrowFrom;
            Vector2 artChord = TutorialArt.ArrowTip - TutorialArt.ArrowStart;
            float scale = delta.magnitude / artChord.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - Mathf.Atan2(artChord.y, artChord.x) * Mathf.Rad2Deg;

            // Pointing left, mirror the arrow so its curve still bows upward.
            bool mirror = delta.x < 0f;
            _arrowRoot.anchorMin = _arrowRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _arrowRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
            _arrowRoot.localScale = new Vector3(1f, mirror ? -1f : 1f, 1f);
            _arrowRoot.anchoredPosition = (_arrowFrom + _arrowTo) * 0.5f;

            var size = new Vector2(TutorialArt.ArrowWidth, TutorialArt.ArrowHeight) * scale;
            _arrow.rectTransform.sizeDelta = size;
            _arrow.rectTransform.anchoredPosition = Vector2.zero;
            _arrowShadow.rectTransform.sizeDelta = size;

            // The shadow falls down and right on screen, whatever way the arrow is turned.
            Vector2 shadow = Quaternion.Euler(0f, 0f, -angle) * ShadowOffset;
            if (mirror) shadow.y = -shadow.y;
            _arrowShadow.rectTransform.anchoredPosition = shadow;
        }
    }
}
