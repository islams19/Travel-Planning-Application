using UnityEngine;

namespace TravelPlanning.UI.Reviews
{
    /// <summary>Draws a star directly, avoiding font symbols and extra image dependencies.</summary>
    public sealed class StarGraphic : UnityEngine.UI.MaskableGraphic
    {
        [Header("Screen references")]
        [SerializeField]
        private bool filled;
        public bool Filled => filled;

        public void SetFilled(bool value)
        {
            filled = value;
            color = value ? new Color32(49, 87, 255, 255) : new Color32(193, 199, 211, 255);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
        {
            helper.Clear();
            Rect bounds = GetPixelAdjustedRect();
            Vector2 center = bounds.center;
            float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
            helper.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < 10; i++)
            {
                float angle = (90 + i * 36) * Mathf.Deg2Rad;
                float length = i % 2 == 0 ? radius : radius * .43f;
                helper.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length, color, Vector2.zero);
            }

            for (int i = 0; i < 10; i++)
            {
                helper.AddTriangle(0, i + 1, (i + 1) % 10 + 1);
            }
        }
    }
}
