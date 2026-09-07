using UnityEngine;

namespace CheatMode
{
    public class Render : MonoBehaviour
    {
        public static GUIStyle StringStyle { get; set; } = new GUIStyle(GUI.skin.label);
        public static Texture2D lineTex = new Texture2D(1, 1);

        public static void DrawString(Vector2 position, string label, Color color, bool centered = false)
        {
            Color colorBackup = GUI.color;
            GUI.color = color;

            GUIContent content = new GUIContent(label);
            Vector2 size = StringStyle.CalcSize(content);
            Vector2 upperLeft = centered ? position - (size / 2f) : position;
            GUI.Label(new Rect(upperLeft, size), content);

            GUI.color = colorBackup;
        }

        public static void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width)
        {
            Matrix4x4 matrixBackup = GUI.matrix;
            Color colorBackup = GUI.color;

            GUI.color = color;

            float num = Vector3.Angle(pointB - pointA, Vector2.right);
            if (pointA.y > pointB.y)
            {
                num = -num;
            }

            if ((pointB - pointA).magnitude == 0)
            {
                return;
            }

            GUIUtility.ScaleAroundPivot(new Vector2((pointB - pointA).magnitude, width), new Vector2(pointA.x, pointA.y + 0.5f));
            GUIUtility.RotateAroundPivot(num, pointA);
            GUI.DrawTexture(new Rect(pointA.x, pointA.y, 1f, 1f), lineTex);

            GUI.matrix = matrixBackup;
            GUI.color = colorBackup;
        }

        public static void DrawBox(float x, float y, float w, float h, Color color, float thickness, string label)
        {
            float xPlusW = x + w;
            float yPlusH = y + h;

            if (label != null)
            {
                DrawString(new Vector2(x + 5, yPlusH), label, color, false);
            }

            DrawLine(new Vector2(x, y), new Vector2(xPlusW, y), color, thickness);
            DrawLine(new Vector2(x, y), new Vector2(x, yPlusH), color, thickness);
            DrawLine(new Vector2(xPlusW, y), new Vector2(xPlusW, yPlusH), color, thickness);
            DrawLine(new Vector2(x, yPlusH), new Vector2(xPlusW, yPlusH), color, thickness);
        }
    }
}
