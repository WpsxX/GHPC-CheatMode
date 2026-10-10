using System;
using CheatMode;
using UnityEngine;

internal static class EspGeometryTests
{
    private static readonly Vector3[] Corners = new Vector3[8];
    private static readonly Vector3[] Projected = new Vector3[8];
    private static int _checks;

    private static void Check(bool condition, string name)
    {
        _checks++;
        if (!condition) throw new Exception(name);
    }

    // These matrices are constructed without invoking any native Unity camera API.
    private static Matrix4x4 View(Vector3 camera, float yaw, float pitch = 0f)
    {
        float c = (float)Math.Cos(yaw);
        float s = (float)Math.Sin(yaw);
        float cp = (float)Math.Cos(pitch);
        float sp = (float)Math.Sin(pitch);
        Matrix4x4 view = new Matrix4x4();
        view.m00 = c; view.m02 = -s; view.m03 = -c * camera.x + s * camera.z;
        view.m10 = -s * sp; view.m11 = cp; view.m12 = -c * sp;
        view.m13 = s * sp * camera.x - cp * camera.y + c * sp * camera.z;
        view.m20 = -s * cp; view.m21 = -sp; view.m22 = -c * cp;
        view.m23 = s * cp * camera.x + sp * camera.y + c * cp * camera.z;
        view.m33 = 1f;
        return view;
    }

    private static Matrix4x4 Perspective(float fov, float aspect)
    {
        float f = (float)(1.0 / Math.Tan(fov * Math.PI / 360.0));
        Matrix4x4 projection = new Matrix4x4();
        projection.m00 = f / aspect;
        projection.m11 = f;
        projection.m22 = -1f;
        projection.m23 = -0.2f;
        projection.m32 = -1f;
        return projection;
    }

    private static bool Project(Matrix4x4 view, Matrix4x4 projection, Bounds bounds,
        Vector3 root, int width, int height, out Rect rect)
    {
        return EspGeometry.TryProjectBounds(view, projection * view, 0.1f, width, height,
            bounds, root, Corners, Projected, out rect);
    }

    // Independent double-precision reference for the original box proportions and placement.
    private static Vector2 ReferencePoint(Vector3 point, Vector3 camera, double yaw, double pitch,
        double fov, double aspect, int width, int height)
    {
        double dx = point.x - camera.x;
        double dz = point.z - camera.z;
        double dy = point.y - camera.y;
        double x = Math.Cos(yaw) * dx - Math.Sin(yaw) * dz;
        double horizontalDepth = Math.Sin(yaw) * dx + Math.Cos(yaw) * dz;
        double depth = Math.Cos(pitch) * horizontalDepth + Math.Sin(pitch) * dy;
        double y = Math.Cos(pitch) * dy - Math.Sin(pitch) * horizontalDepth;
        double f = 1.0 / Math.Tan(fov * Math.PI / 360.0);
        return new Vector2((float)((x * f / aspect / depth + 1.0) * width * 0.5),
            (float)((1.0 - y * f / depth) * height * 0.5));
    }

    private static Rect ReferenceBox(Bounds bounds, Vector3 root, Vector3 camera, float yaw, float pitch,
        float fov, int width, int height)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        double minX = double.MaxValue;
        double maxX = double.MinValue;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            double x = ReferencePoint(corner, camera, yaw, pitch, fov, (double)width / height, width, height).x;
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
        }
        Vector2 top = ReferencePoint(new Vector3(root.x, max.y, root.z), camera,
            yaw, pitch, fov, (double)width / height, width, height);
        Vector2 bottom = ReferencePoint(new Vector3(root.x, min.y, root.z), camera,
            yaw, pitch, fov, (double)width / height, width, height);
        float h = Math.Abs(top.y - bottom.y);
        float w = (float)Math.Max(maxX - minX, h);
        return new Rect(bottom.x - w * 0.5f, Math.Min(top.y, bottom.y), w, h);
    }

    private static void Near(float actual, float expected, string name)
    {
        Check(Math.Abs(actual - expected) < 0.08f, name + ": " + actual + " vs " + expected);
    }

    private static void Main()
    {
        Matrix4x4 view = View(new Vector3(), 0f);
        Matrix4x4 projection = Perspective(60f, 1920f / 1080f);
        Rect rect;
        Vector3 root = new Vector3(0f, 0f, 50f);
        Check(Project(view, projection, new Bounds(root + new Vector3(0f, 0.9f, 0f),
            new Vector3(0.5f, 1.8f, 0.5f)), root, 1920, 1080, out rect), "infantry visible");
        Near(rect.width, rect.height, "legacy square infantry");
        Near(rect.center.x, 960f, "root alignment");

        // Vary camera translation, yaw/pitch, zoom, resolution, root offset and unit proportions.
        System.Random random = new System.Random(104729);
        for (int i = 0; i < 1000; i++)
        {
            int width = i % 3 == 0 ? 860 : i % 3 == 1 ? 1920 : 3440;
            int height = i % 3 == 0 ? 540 : i % 3 == 1 ? 1080 : 1440;
            Vector3 camera = new Vector3((float)random.NextDouble() * 100f,
                (float)random.NextDouble() * 8f, (float)random.NextDouble() * 100f);
            float yaw = (float)random.NextDouble() * 6f;
            float pitch = (float)random.NextDouble() * 2.2f - 1.1f;
            float distance = 100f + (float)random.NextDouble() * 2900f;
            root = camera + new Vector3((float)(Math.Sin(yaw) * Math.Cos(pitch)) * distance,
                (float)Math.Sin(pitch) * distance - camera.y,
                (float)(Math.Cos(yaw) * Math.Cos(pitch)) * distance);
            Bounds bounds = new Bounds(root + new Vector3(0.7f, 1f, -0.4f),
                new Vector3(0.8f + (float)random.NextDouble() * 8f, 2f, 3f));
            float fov = 10f + (float)random.NextDouble() * 70f;
            Rect expected = ReferenceBox(bounds, root, camera, yaw, pitch, fov, width, height);
            bool drawn = Project(View(camera, yaw, pitch), Perspective(fov, (float)width / height),
                bounds, root, width, height, out rect);
            Check(drawn == (expected.width > 1f && expected.height > 1f), "pixel-size visibility");
            Near(rect.x, expected.x, "x"); Near(rect.y, expected.y, "y");
            Near(rect.width, expected.width, "width"); Near(rect.height, expected.height, "height");
        }

        root = new Vector3(0f, 0f, -10f);
        Check(!Project(view, projection, new Bounds(root, Vector3.one), root, 1920, 1080, out rect),
            "behind camera rejected");
        root = new Vector3(100f, 0f, 10f);
        Check(!Project(view, projection, new Bounds(root, Vector3.one), root, 1920, 1080, out rect),
            "outside viewport rejected");
        root = new Vector3(0f, 0f, 0.2f);
        Check(Project(view, projection, new Bounds(root, Vector3.one), root, 1920, 1080, out rect),
            "near-plane crossing visible");
        Check(rect.xMin >= 0f && rect.yMin >= 0f && rect.xMax <= 1920f && rect.yMax <= 1080f,
            "near-plane fallback bounded");
        root = new Vector3(0f, 0f, 0.01f);
        Check(!Project(view, projection, new Bounds(root, Vector3.one * 0.01f),
            root, 1920, 1080, out rect), "entirely before near plane rejected");
        root = new Vector3(float.NaN, 0f, 10f);
        Check(!Project(view, projection, new Bounds(root, Vector3.one), root, 1920, 1080, out rect),
            "invalid bounds rejected");
        root = new Vector3(0f, 0f, 10f);
        Check(!Project(view, projection, new Bounds(root, Vector3.one), root, 0, 1080, out rect),
            "invalid screen size rejected");

        Matrix4x4 ortho = new Matrix4x4();
        ortho.m00 = 0.05f; ortho.m11 = 0.1f; ortho.m22 = -0.01f; ortho.m33 = 1f;
        Check(Project(view, ortho, new Bounds(root + new Vector3(0f, 1f, 0f),
            new Vector3(0.5f, 2f, 0.5f)), root, 1920, 1080, out rect), "orthographic visible");
        Near(rect.height, 108f, "orthographic height");
        Near(rect.width, 108f, "orthographic minimum width");
        Console.WriteLine("PASS: " + _checks + " projection checks (no Unity runtime required).");
    }
}
