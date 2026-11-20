using System.Collections.Generic;
using NUnit.Framework;
using ResortEmpire.Game;
using ResortEmpire.UI;
using UnityEngine;

namespace ResortEmpire.Tests
{
    /// <summary>Where the tutorial's bubble and arrow go (<see cref="TutorialLayout"/>), and when the first launch opens the tutorial.</summary>
    public class TutorialLayoutTests
    {
        // The safe area of an 852 × 393 screen (the Figma frame), centred on the origin like the tutorial canvas.
        private static readonly Rect Screen = new Rect(-418f, -188f, 836f, 376f);
        private static readonly Vector2 Bubble = new Vector2(290f, 110f);

        [Test]
        public void TheBubbleGoesBesideItsTargetWithoutCoveringIt()
        {
            var target = new Rect(-300f, 20f, 80f, 80f);

            Vector2 center = TutorialLayout.PlaceBubble(Screen, Bubble, target, new List<Rect>(), Vector2.zero);
            Rect bubble = TutorialLayout.RectAt(center, Bubble);

            Assert.IsFalse(bubble.Overlaps(target), "The bubble covers its target.");
            Assert.AreEqual(target.xMax + TutorialLayout.ArrowGap + Bubble.x * 0.5f, center.x, 0.01f, "Right of the target, with room for the arrow.");
            Assert.AreEqual(target.center.y, center.y, 0.01f);
        }

        [Test]
        public void TheBubbleStaysOnScreenAndOffWhatItMustNotCover()
        {
            var target = new Rect(250f, -40f, 120f, 80f); // near the right edge: no room on the right
            var avoid = new List<Rect> { new Rect(-20f, -150f, 120f, 150f) };

            Assert.IsTrue(TutorialLayout.TryPlaceBubble(Screen, Bubble, target, avoid, out Vector2 center));
            Rect bubble = TutorialLayout.RectAt(center, Bubble);

            bool onScreen = bubble.xMin >= Screen.xMin && bubble.xMax <= Screen.xMax && bubble.yMin >= Screen.yMin && bubble.yMax <= Screen.yMax;
            Assert.IsTrue(onScreen, $"Off screen: {bubble}");
            Assert.IsFalse(bubble.Overlaps(target));
            Assert.IsFalse(bubble.Overlaps(avoid[0]), "The bubble covers a rect it had to avoid.");
        }

        [Test]
        public void NoSpotFitsWhenEverythingIsTaken()
        {
            var target = new Rect(-50f, -50f, 100f, 100f);
            var avoid = new List<Rect> { Screen };

            Assert.IsFalse(TutorialLayout.TryPlaceBubble(Screen, Bubble, target, avoid, out _));
            Assert.AreEqual(new Vector2(10f, 20f), TutorialLayout.PlaceBubble(Screen, Bubble, target, avoid, new Vector2(10f, 20f)),
                "PlaceBubble falls back to the given spot.");
        }

        [Test]
        public void TheArrowRunsBetweenTheEdgesAndNeverGetsTooLong()
        {
            Rect bubble = TutorialLayout.RectAt(new Vector2(-200f, 0f), Bubble);
            var target = new Rect(100f, -20f, 40f, 40f);

            Assert.IsTrue(TutorialLayout.TryGetArrow(bubble, target, out Vector2 from, out Vector2 to));
            Assert.Less(to.x, target.xMin, "The arrow stops before the target.");
            Assert.Greater(from.x, bubble.xMax, "The arrow starts outside the bubble.");
            Assert.LessOrEqual(Vector2.Distance(from, to), TutorialLayout.MaxArrowLength + 0.01f);
        }

        [Test]
        public void ThereIsNoArrowWhenTheBubbleTouchesItsTarget()
        {
            Rect bubble = TutorialLayout.RectAt(Vector2.zero, Bubble);
            var target = new Rect(bubble.xMax + 2f, -10f, 20f, 20f);

            Assert.IsFalse(TutorialLayout.TryGetArrow(bubble, target, out _, out _));
        }

        [Test]
        public void TheFirstLaunchOfABuildOpensTheTutorialUntilItIsDone()
        {
            Assert.IsTrue(FirstLaunchTutorial.ShouldOpenTutorial(isEditor: false, isDone: false, GameScenes.Game));
            Assert.IsFalse(FirstLaunchTutorial.ShouldOpenTutorial(isEditor: false, isDone: true, GameScenes.Game), "Done: the game opens.");
            Assert.IsFalse(FirstLaunchTutorial.ShouldOpenTutorial(isEditor: true, isDone: false, GameScenes.Game), "Play in the Editor starts the open scene.");
            Assert.IsFalse(FirstLaunchTutorial.ShouldOpenTutorial(isEditor: false, isDone: false, GameScenes.Tutorial));
        }
    }
}
