using UnityEngine;

namespace TravelPlanning.UI.Desktop
{
    /// <summary>Keeps the entire reference layout available when a desktop window becomes short or narrow.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.CanvasScaler))]
    public sealed class DesktopCanvasFit : MonoBehaviour
    {
        private void OnEnable() => Apply();
        public void Apply()
        {
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        }
    }
}
