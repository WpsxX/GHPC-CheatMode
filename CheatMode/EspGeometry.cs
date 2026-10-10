using UnityEngine;

namespace CheatMode
{
    // Pure projection math: camera matrices and scratch storage are supplied by the caller.
    internal static class EspGeometry
    {
        private static readonly int[] BoundsEdges =
        {
            0, 1, 2, 3, 4, 5, 6, 7,
            0, 2, 1, 3, 4, 6, 5, 7,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        internal static bool TryProjectBounds(Matrix4x4 view, Matrix4x4 viewProjection,
            float near, int screenWidth, int screenHeight, Bounds bounds, Vector3 root,
            Vector3[] corners, Vector3[] projectedCorners, out Rect rect)
        {
            rect = new Rect();
            if (screenWidth <= 0 || screenHeight <= 0 || !Finite(near) || near <= 0f
                || !Finite(root) || !Finite(bounds.center) || !Finite(bounds.size))
            {
                return false;
            }
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector2 screenMin = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 screenMax = new Vector2(float.MinValue, float.MinValue);
            int frontCount = 0;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                corners[i] = corner;
                float depth = -view.MultiplyPoint3x4(corner).z;
                if (!Finite(depth))
                {
                    return false;
                }
                projectedCorners[i] = new Vector3(0f, 0f, depth);
                if (depth >= near)
                {
                    Vector3 point;
                    if (!TryProjectPoint(viewProjection, corner, out point))
                    {
                        return false;
                    }
                    Accumulate(point, ref screenMin, ref screenMax);
                    frontCount++;
                }
            }
            if (frontCount == 0)
            {
                return false;
            }
            // Most units are wholly in front: skip all twelve edge tests on that path.
            if (frontCount != 8)
            {
                for (int i = 0; i < BoundsEdges.Length; i += 2)
                {
                    int a = BoundsEdges[i];
                    int b = BoundsEdges[i + 1];
                    float za = projectedCorners[a].z;
                    float zb = projectedCorners[b].z;
                    if ((za >= near) == (zb >= near))
                    {
                        continue;
                    }
                    float t = (near - za) / (zb - za);
                    Vector3 point;
                    if (!TryProjectPoint(viewProjection, Vector3.LerpUnclamped(corners[a], corners[b], t), out point))
                    {
                        return false;
                    }
                    Accumulate(point, ref screenMin, ref screenMax);
                }
            }
            if (screenMax.x <= 0f || screenMin.x >= 1f || screenMax.y <= 0f || screenMin.y >= 1f)
            {
                return false;
            }

            // Restore the original shape: root-aligned top/bottom, projected bounds width,
            // and a minimum width equal to height (square for narrow infantry).
            Vector3 top = new Vector3(root.x, max.y, root.z);
            Vector3 bottom = new Vector3(root.x, min.y, root.z);
            Vector3 topPoint;
            Vector3 bottomPoint;
            if (frontCount == 8 && -view.MultiplyPoint3x4(top).z >= near
                && -view.MultiplyPoint3x4(bottom).z >= near
                && TryProjectPoint(viewProjection, top, out topPoint)
                && TryProjectPoint(viewProjection, bottom, out bottomPoint))
            {
                float height = Mathf.Abs(topPoint.y - bottomPoint.y) * screenHeight;
                float width = Mathf.Max((screenMax.x - screenMin.x) * screenWidth, height);
                float x = bottomPoint.x * screenWidth - width * 0.5f;
                float y = (1f - Mathf.Max(topPoint.y, bottomPoint.y)) * screenHeight;
                if (Finite(width) && Finite(height) && Finite(x) && Finite(y)
                    && width <= screenWidth * 4f && height <= screenHeight * 4f)
                {
                    rect = new Rect(x, y, width, height);
                    return width > 1f && height > 1f && rect.xMax > 0f
                        && rect.xMin < screenWidth && rect.yMax > 0f && rect.yMin < screenHeight;
                }
            }

            // Near-camera crossings need clipped extents instead of the old inverted box.
            // Bound the fallback to the screen before drawing or placing its label.
            float xMin = Mathf.Clamp01(screenMin.x) * screenWidth;
            float xMax = Mathf.Clamp01(screenMax.x) * screenWidth;
            float yMin = (1f - Mathf.Clamp01(screenMax.y)) * screenHeight;
            float yMax = (1f - Mathf.Clamp01(screenMin.y)) * screenHeight;
            rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return rect.width > 1f && rect.height > 1f;
        }

        private static bool TryProjectPoint(Matrix4x4 viewProjection, Vector3 world, out Vector3 point)
        {
            Vector4 clip = viewProjection * new Vector4(world.x, world.y, world.z, 1f);
            point = new Vector3();
            if (!Finite(clip.w) || Mathf.Abs(clip.w) < 0.0000001f)
            {
                return false;
            }
            point.x = (clip.x / clip.w + 1f) * 0.5f;
            point.y = (clip.y / clip.w + 1f) * 0.5f;
            return Finite(point);
        }

        private static void Accumulate(Vector3 point, ref Vector2 min, ref Vector2 max)
        {
            min.x = Mathf.Min(min.x, point.x);
            min.y = Mathf.Min(min.y, point.y);
            max.x = Mathf.Max(max.x, point.x);
            max.y = Mathf.Max(max.y, point.y);
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Finite(Vector3 value)
        {
            return Finite(value.x) && Finite(value.y) && Finite(value.z);
        }
    }
}
