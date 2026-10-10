using UnityEngine;

namespace CheatMode
{
    public class Render : MonoBehaviour
    {
        private static GUIStyle _stringStyle;
        private static readonly GUIContent LabelContent = new GUIContent();

        public static GUIStyle StringStyle
        {
            // GUI.skin must only be accessed inside OnGUI, not during type initialization.
            get { return _stringStyle ?? (_stringStyle = new GUIStyle(GUI.skin.label)); }
            set { _stringStyle = value; }
        }

        public static void DrawString(Vector2 position, string label, Color color, bool centered = false)
        {
            Vector2 size = MeasureString(label);
            DrawString(position, label, color, centered, size);
        }

        public static Vector2 MeasureString(string label)
        {
            LabelContent.text = label;
            return StringStyle.CalcSize(LabelContent);
        }

        private static void DrawString(Vector2 position, string label, Color color, bool centered, Vector2 size)
        {
            Color colorBackup = GUI.color;
            try
            {
                GUI.color = color;
                LabelContent.text = label;
                GUIStyle style = StringStyle;
                Vector2 upperLeft = centered ? position - (size / 2f) : position;
                GUI.Label(new Rect(upperLeft, size), LabelContent, style);
            }
            finally
            {
                GUI.color = colorBackup;
            }
        }

        public static void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width)
        {
            Vector2 delta = pointB - pointA;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Matrix4x4 matrixBackup = GUI.matrix;
            Color colorBackup = GUI.color;

            try
            {
                GUI.color = color;
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(angle, pointA);
                GUI.DrawTexture(new Rect(pointA.x, pointA.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
            }
            finally
            {
                GUI.matrix = matrixBackup;
                GUI.color = colorBackup;
            }
        }

        public static void DrawBox(float x, float y, float w, float h, Color color, float thickness, string label)
        {
            DrawBox(x, y, w, h, color, thickness, label, label != null ? MeasureString(label) : Vector2.zero);
        }

        public static void DrawBox(float x, float y, float w, float h, Color color, float thickness,
            string label, Vector2 labelSize)
        {
            float yPlusH = y + h;

            if (label != null)
            {
                // Legacy boxes placed the label at the upper-left (their height was negative).
                DrawString(new Vector2(x + 5, Mathf.Max(0f, y)), label, color, false, labelSize);
            }
            Color colorBackup = GUI.color;
            try
            {
                GUI.color = color;
                Texture2D texture = Texture2D.whiteTexture;
                float halfThickness = thickness * 0.5f;
                // Axis-aligned borders need no matrix scaling/rotation.
                GUI.DrawTexture(new Rect(x, y - halfThickness, w, thickness), texture);
                GUI.DrawTexture(new Rect(x, yPlusH - halfThickness, w, thickness), texture);
                GUI.DrawTexture(new Rect(x - halfThickness, y, thickness, h), texture);
                GUI.DrawTexture(new Rect(x + w - halfThickness, y, thickness, h), texture);
            }
            finally
            {
                GUI.color = colorBackup;
            }
        }
    }
}
