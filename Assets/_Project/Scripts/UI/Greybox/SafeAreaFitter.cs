using UnityEngine;

namespace ResortEmpire.UI
{
    /// <summary>Keeps UI inside the screen's safe area (notches and rounded corners on phones held sideways).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void Update()
        {
            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (safe == _applied && screen == _screen || screen.x <= 0 || screen.y <= 0)
            {
                return;
            }

            _applied = safe;
            _screen = screen;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rect.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
