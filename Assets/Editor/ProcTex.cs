using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Procedural texture generation (tileable value noise) saved as PNG assets.
public static class ProcTex
{
    public const string Dir = "Assets/Generated/Textures";

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xffffff) / (float)0xffffff;
        }
    }

    static int Mod(int a, int p) => ((a % p) + p) % p;

    /// Periodic value noise: tiles exactly with period (px, py) in lattice units.
    public static float VNoise(float x, float y, int px, int py, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int xa = Mod(x0, px), xb = Mod(x0 + 1, px), ya = Mod(y0, py), yb = Mod(y0 + 1, py);
        float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// Tileable fBm over u,v in [0,1).
    public static float Fbm(float u, float v, int fx, int fy, int octaves, int seed)
    {
        float s = 0, a = 0.5f, n = 0;
        for (int i = 0; i < octaves; i++)
        {
            s += a * VNoise(u * fx, v * fy, fx, fy, seed + i * 31);
            n += a;
            a *= 0.5f;
            fx *= 2;
            fy *= 2;
        }
        return s / n;
    }

    public static float H(int x, int y, int seed) => Hash(x, y, seed);

    public static Texture2D Save(string name, int w, int h, Func<float, float, Color> f, bool alpha = false, bool repeat = true)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // u,v with v = 0 at the TOP of the image (more natural for drawing).
                float u = (x + 0.5f) / w, v = 1f - (y + 0.5f) / h;
                px[y * w + x] = f(u, v);
            }
        tex.SetPixels(px);
        tex.Apply();

        Directory.CreateDirectory(Dir);
        string path = $"{Dir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.alphaIsTransparency = alpha;
        imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

    // ---------------- Surfaces ----------------

    public static Texture2D Plaster(string name, Color baseCol, int seed) => Save(name, 512, 512, (u, v) =>
    {
        float n = Fbm(u, v, 8, 8, 5, seed);
        float stain = Fbm(u, v, 3, 3, 4, seed + 100);
        float streak = Fbm(u, v, 24, 2, 3, seed + 200) * Mathf.Clamp01(1.2f - v * 1.3f);
        float k = 0.8f + n * 0.35f;
        var c = Mul(baseCol, k);
        if (stain > 0.56f) c = Color.Lerp(c, Mul(baseCol, 0.55f) * new Color(1f, 0.9f, 0.75f), (stain - 0.56f) * 2.2f);
        c = Color.Lerp(c, Mul(c, 0.78f), Mathf.Clamp01((streak - 0.5f) * 3f));
        c.a = 1;
        return c;
    });

    public static Texture2D Wallpaper(string name, Color a, Color b, int stripes, int seed, bool damask) => Save(name, 512, 512, (u, v) =>
    {
        float s = (u * stripes) % 1f;
        Color c = s < 0.5f ? a : b;
        if (damask)
        {
            float d = Mathf.Abs(Mathf.Sin(u * Mathf.PI * stripes) * Mathf.Sin(v * Mathf.PI * stripes * 2));
            if (d > 0.72f) c = Color.Lerp(c, Mul(b, 1.25f), 0.6f);
        }
        else if (Mathf.Abs(s - 0.25f) < 0.015f || Mathf.Abs(s - 0.75f) < 0.015f) c = Mul(c, 1.25f);

        float age = Fbm(u, v, 4, 4, 4, seed);
        float n = Fbm(u, v, 32, 32, 2, seed + 7);
        c = Mul(c, 0.8f + 0.3f * n);
        if (age > 0.58f) c = Color.Lerp(c, new Color(0.3f, 0.25f, 0.18f), (age - 0.58f) * 2.5f);
        float peel = Fbm(u, v, 6, 6, 3, seed + 50);
        if (peel > 0.68f) c = Color.Lerp(c, new Color(0.62f, 0.58f, 0.5f), Mathf.Clamp01((peel - 0.68f) * 8f));
        c.a = 1;
        return c;
    });

    public static Texture2D WoodPlanks(string name, int seed) => Save(name, 512, 512, (u, v) =>
    {
        const int planks = 5;
        int pi = Mathf.FloorToInt(u * planks);
        float lu = u * planks - pi;
        float offset = H(pi, 0, seed);
        float lv = (v * 2f + offset) % 1f;
        float grainWarp = Fbm(u, v, 4, 2, 3, seed + pi);
        float grain = 0.5f + 0.5f * Mathf.Sin((lu * 18f + grainWarp * 6f) * Mathf.PI * 2f);
        var c = new Color(0.26f, 0.17f, 0.11f) * (0.75f + 0.5f * H(pi, 1, seed));
        c = Mul(c, 0.85f + 0.25f * grain);
        c = Mul(c, 0.85f + 0.3f * Fbm(u, v, 16, 16, 3, seed + 9));
        if (lu < 0.02f || lu > 0.985f || lv < 0.008f) c = Mul(c, 0.35f);
        float wear = Fbm(u, v, 3, 3, 3, seed + 40);
        if (wear > 0.6f) c = Color.Lerp(c, new Color(0.35f, 0.3f, 0.24f), (wear - 0.6f) * 1.5f);
        c.a = 1;
        return c;
    });

    public static Texture2D Wood(string name, Color baseCol, int seed) => Save(name, 256, 256, (u, v) =>
    {
        float warp = Fbm(u, v, 2, 4, 3, seed);
        float grain = 0.5f + 0.5f * Mathf.Sin((u * 26f + warp * 5f) * Mathf.PI * 2f);
        var c = Mul(baseCol, 0.8f + 0.25f * grain);
        c = Mul(c, 0.85f + 0.3f * Fbm(u, v, 12, 12, 3, seed + 3));
        c.a = 1;
        return c;
    });

    public static Texture2D Concrete(string name, int seed) => Save(name, 512, 512, (u, v) =>
    {
        float n = Fbm(u, v, 6, 6, 5, seed);
        float spots = Fbm(u, v, 40, 40, 2, seed + 1);
        float g = 0.18f + 0.12f * n - (spots > 0.7f ? 0.05f : 0f);
        return new Color(g, g * 0.98f, g * 0.95f, 1f);
    });

    public static Texture2D Paper(string name, int seed) => Save(name, 256, 340, (u, v) =>
    {
        float n = Fbm(u, v, 8, 8, 4, seed);
        var c = new Color(0.85f, 0.81f, 0.7f) * (0.9f + 0.15f * n);
        bool line = v > 0.15f && v < 0.92f && ((v * 22f) % 1f) < 0.05f && u > 0.1f && u < 0.9f;
        bool scrawl = v > 0.15f && v < 0.85f && ((v * 22f) % 1f) > 0.55f && ((v * 22f) % 1f) < 0.8f &&
                      u > 0.12f && u < 0.3f + 0.55f * H(Mathf.FloorToInt(v * 22f), 0, seed) &&
                      Fbm(u, v, 60, 20, 2, seed + 5) > 0.45f;
        if (line) c = Mul(c, 0.86f);
        if (scrawl) c = new Color(0.2f, 0.16f, 0.15f);
        float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
        if (edge < 0.04f) c = Mul(c, 0.8f + edge * 5f);
        c.a = 1;
        return c;
    });

    // ---------------- Photographs ----------------

    static float Ell(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return dx * dx + dy * dy;
    }

    static Color Sepia(float val) => new Color(val * 1.05f, val * 0.9f, val * 0.7f, 1f);

    public static Texture2D Portrait(string name, int idx) => Save(name, 256, 320, (u, v) =>
    {
        int seed = 400 + idx * 17;
        float val = Mathf.Lerp(0.62f, 0.38f, v) * (0.85f + 0.25f * Fbm(u, v, 4, 5, 4, seed));

        float cx = 0.5f + (idx - 1.5f) * 0.015f, cy = 0.37f;
        float rx = 0.16f + idx * 0.008f, ry = 0.21f;
        float head = Ell(u, v, cx, cy, rx, ry);
        float shoulders = Ell(u, v, 0.5f, 1.02f, 0.5f, 0.38f);
        bool neck = Mathf.Abs(u - cx) < 0.07f && v > cy && v < 0.72f;

        bool hairBack = false, hairFront = false;
        switch (idx)
        {
            case 0: hairBack = Ell(u, v, cx, 0.12f, 0.09f, 0.07f) < 1f; hairFront = head < 1.15f && v < cy - 0.1f; break;
            case 1: hairFront = head < 1.12f && v < cy - 0.08f; break;
            case 2: hairBack = Ell(u, v, cx, cy + 0.08f, 0.24f, 0.32f) < 1f && v < 0.68f; hairFront = head < 1.15f && v < cy - 0.06f; break;
            case 3:
                hairFront = (Mathf.Abs(v - 0.17f) < 0.018f && Mathf.Abs(u - cx) < 0.26f) || (v < 0.17f && v > 0.03f && Mathf.Abs(u - cx) < 0.15f);
                break;
        }

        if (hairBack) val = 0.12f;
        if (shoulders < 1f) val = 0.16f + 0.06f * Fbm(u, v, 8, 8, 2, seed + 1);
        if (neck) val = 0.5f;
        if (head < 1f)
        {
            val = 0.66f - 0.3f * head;
            bool eye = Ell(u, v, cx - 0.062f, cy - 0.01f, 0.034f, 0.019f) < 1f || Ell(u, v, cx + 0.062f, cy - 0.01f, 0.034f, 0.019f) < 1f;
            if (eye) val = 0.12f;
            if (Mathf.Abs(v - (cy + 0.11f)) < 0.006f && Mathf.Abs(u - cx) < 0.045f) val *= 0.6f;
        }
        if (hairFront) val = 0.1f;

        val += (H(Mathf.FloorToInt(u * 256), Mathf.FloorToInt(v * 320), seed) - 0.5f) * 0.06f;
        if (H(Mathf.FloorToInt(u * 128), idx, seed) > 0.992f) val += 0.25f; // vertical scratches
        float d = Ell(u, v, 0.5f, 0.5f, 0.75f, 0.8f);
        val *= Mathf.Clamp01(1.15f - d * 0.6f);
        return Sepia(val);
    });

    public static Texture2D OldPhoto(string name, int idx) => Save(name, 256, 192, (u, v) =>
    {
        int seed = 900 + idx * 13;
        float horizon = 0.5f + (H(idx, 1, seed) - 0.5f) * 0.25f;
        float val = v < horizon ? Mathf.Lerp(0.75f, 0.55f, v / horizon) : Mathf.Lerp(0.4f, 0.25f, (v - horizon) / (1 - horizon));
        val *= 0.85f + 0.25f * Fbm(u, v, 4, 3, 4, seed);

        switch (idx % 3)
        {
            case 0: // house
                float hx = 0.3f + H(idx, 2, seed) * 0.4f;
                if (Mathf.Abs(u - hx) < 0.14f && v > horizon - 0.22f && v < horizon) val = 0.2f;
                if (v < horizon - 0.22f && v > horizon - 0.36f && Mathf.Abs(u - hx) < 0.14f - (horizon - 0.22f - v)) val = 0.15f;
                if (Mathf.Abs(u - hx + 0.05f) < 0.02f && Mathf.Abs(v - (horizon - 0.12f)) < 0.03f) val = 0.7f; // lit window
                break;
            case 1: // trees
                for (int k = 0; k < 5; k++)
                {
                    float tx = H(k, idx, seed), ts = 0.06f + H(k, 3, seed) * 0.06f;
                    if (Ell(u, v, tx, horizon - ts, ts * 0.8f, ts * 1.6f) < 1f) val = 0.18f;
                }
                break;
            case 2: // a lone figure far away
                float fx = 0.6f + (H(idx, 4, seed) - 0.5f) * 0.3f;
                if (Ell(u, v, fx, horizon - 0.12f, 0.012f, 0.016f) < 1f || (Mathf.Abs(u - fx) < 0.014f && v > horizon - 0.11f && v < horizon)) val = 0.08f;
                break;
        }
        val += (H(Mathf.FloorToInt(u * 256), Mathf.FloorToInt(v * 192), seed) - 0.5f) * 0.07f;
        float d = Ell(u, v, 0.5f, 0.5f, 0.7f, 0.7f);
        val *= Mathf.Clamp01(1.2f - d * 0.7f);
        return Sepia(val);
    });

    // ---------------- Decals ----------------

    public static Texture2D EyeSymbol(string name) => Save(name, 256, 256, (u, v) =>
    {
        float x = u * 2 - 1, y = (v - 0.45f) * 2;
        float lid = 0.42f * (1 - x * x);
        float paint = 0;
        if (Mathf.Abs(x) < 0.97f && Mathf.Abs(Mathf.Abs(y) - lid) < 0.045f) paint = 1;
        float r = Mathf.Sqrt(x * x + y * y);
        if (Mathf.Abs(r - 0.27f) < 0.04f) paint = 1;
        if (r < 0.12f) paint = 1;
        // drips
        for (int k = 0; k < 6; k++)
        {
            float dx = -0.6f + k * 0.24f + (H(k, 0, 3) - 0.5f) * 0.08f;
            float len = 0.15f + H(k, 1, 3) * 0.45f;
            float top = lid;
            if (Mathf.Abs(x - dx) < 0.018f && y > top - 0.02f && y < top + len) paint = 1;
        }
        float rough = 0.7f + 0.3f * Fbm(u, v, 16, 16, 3, 77);
        return new Color(0.62f, 0.06f, 0.04f, paint * rough);
    }, true, false);

    // ---------------- Chapter 2 ----------------

    public static Texture2D Tiles(string name, Color a, Color b, int n, int seed) => Save(name, 512, 512, (u, v) =>
    {
        int ix = Mathf.FloorToInt(u * n), iy = Mathf.FloorToInt(v * n);
        float lu = u * n - ix, lv = v * n - iy;
        Color c = ((ix + iy) % 2 == 0) ? a : b;
        c = Mul(c, 0.85f + 0.2f * Fbm(u, v, 16, 16, 3, seed));
        float dirt = Fbm(u, v, 4, 4, 4, seed + 3);
        if (dirt > 0.55f) c = Color.Lerp(c, new Color(0.2f, 0.17f, 0.12f), (dirt - 0.55f) * 1.6f);
        if (lu < 0.03f || lv < 0.03f) c = Mul(c, 0.45f);
        c.a = 1;
        return c;
    });

    public static Texture2D Carpet(string name, Color baseCol, int seed) => Save(name, 256, 256, (u, v) =>
    {
        float n = Fbm(u, v, 32, 32, 3, seed);
        float pattern = (Mathf.Sin(u * Mathf.PI * 16) * Mathf.Sin(v * Mathf.PI * 16) > 0.6f) ? 1.15f : 1f;
        var c = Mul(baseCol, (0.75f + 0.4f * n) * pattern);
        float stain = Fbm(u, v, 3, 3, 3, seed + 9);
        if (stain > 0.6f) c = Mul(c, 0.7f);
        c.a = 1;
        return c;
    });

    static void DrawClock(float u, float v, float cx, float cy, float r, int hour, int minute, ref float val)
    {
        float dx = u - cx, dy = v - cy;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        if (d < r) val = 0.78f;
        if (Mathf.Abs(d - r) < 0.012f) val = 0.15f;
        // hour ticks
        float ang = Mathf.Atan2(dx, -dy); // 0 at 12 o'clock, clockwise
        float a12 = ang / (Mathf.PI * 2f) * 12f;
        if (d < r * 0.95f && d > r * 0.82f && Mathf.Abs(a12 - Mathf.Round(a12)) < 0.06f) val = 0.2f;
        // hands
        void Hand(float angleDeg, float len, float width, ref float vv)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            Vector2 p = new Vector2(dx, dy);
            float along = Vector2.Dot(p, dir);
            float perp = Mathf.Abs(dir.x * p.y - dir.y * p.x);
            if (along > -0.01f && along < len && perp < width) vv = 0.08f;
        }
        Hand(minute * 6f, r * 0.85f, 0.008f, ref val);
        Hand((hour % 12) * 30f + minute * 0.5f, r * 0.55f, 0.013f, ref val);
        if (d < 0.015f) val = 0.08f;
    }

    /// A sepia photograph of the hall clock showing a specific time.
    public static Texture2D ClockPhoto(string name, int hour, int minute, int seed) => Save(name, 256, 320, (u, v) =>
    {
        float val = Mathf.Lerp(0.5f, 0.32f, v) * (0.85f + 0.25f * Fbm(u, v, 4, 5, 4, seed));
        // clock case
        if (Mathf.Abs(u - 0.5f) < 0.3f && v > 0.08f) val = 0.22f + 0.05f * Fbm(u, v, 3, 12, 2, seed + 1);
        DrawClock(u, v * 1.25f, 0.5f, 0.42f, 0.23f, hour, minute, ref val);
        val += (H(Mathf.FloorToInt(u * 256), Mathf.FloorToInt(v * 320), seed) - 0.5f) * 0.06f;
        float d = Ell(u, v, 0.5f, 0.5f, 0.75f, 0.8f);
        val *= Mathf.Clamp01(1.15f - d * 0.6f);
        return Sepia(val);
    });

    public static Texture2D ClockFace(string name) => Save(name, 256, 256, (u, v) =>
    {
        float val = 0f;
        float dx = u - 0.5f, dy = v - 0.5f;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        if (d < 0.48f) val = 0.82f - 0.1f * Fbm(u, v, 8, 8, 3, 61);
        if (Mathf.Abs(d - 0.47f) < 0.012f) val = 0.2f;
        float ang = Mathf.Atan2(dx, -dy);
        float a12 = ang / (Mathf.PI * 2f) * 12f;
        if (d < 0.44f && d > 0.36f && Mathf.Abs(a12 - Mathf.Round(a12)) < 0.07f) val = 0.15f;
        float a60 = ang / (Mathf.PI * 2f) * 60f;
        if (d < 0.44f && d > 0.41f && Mathf.Abs(a60 - Mathf.Round(a60)) < 0.08f) val = 0.25f;
        return new Color(val, val * 0.95f, val * 0.85f, d < 0.49f ? 1f : 0f);
    }, true, false);

    /// The Kerr family, 1979 — four people.
    public static Texture2D FamilyPhoto(string name) => Save(name, 512, 340, (u, v) =>
    {
        int seed = 707;
        float val = Mathf.Lerp(0.6f, 0.4f, v) * (0.85f + 0.25f * Fbm(u, v, 5, 4, 4, seed));
        float[] xs = { 0.18f, 0.36f, 0.64f, 0.82f };
        float[] hs = { 0.30f, 0.22f, 0.40f, 0.26f };   // head heights (child, adults…)
        for (int i = 0; i < 4; i++)
        {
            float hx = xs[i], hy = hs[i];
            float rx = 0.055f, ry = 0.085f;
            if (Ell(u, v, hx, hy + 0.32f + (0.5f - hy) * 0.3f, 0.11f, 0.32f) < 1f) val = 0.17f; // body
            if (Ell(u, v, hx, hy, rx, ry) < 1f)
            {
                val = 0.62f - 0.25f * Ell(u, v, hx, hy, rx, ry);
                if (Ell(u, v, hx - 0.02f, hy - 0.005f, 0.012f, 0.009f) < 1f || Ell(u, v, hx + 0.02f, hy - 0.005f, 0.012f, 0.009f) < 1f) val = 0.15f;
            }
            if (Ell(u, v, hx, hy - 0.04f, rx * 1.1f, ry * 0.75f) < 1f && v < hy - 0.03f) val = 0.1f; // hair
        }
        val += (H(Mathf.FloorToInt(u * 512), Mathf.FloorToInt(v * 340), seed) - 0.5f) * 0.06f;
        float dd = Ell(u, v, 0.5f, 0.5f, 0.75f, 0.75f);
        val *= Mathf.Clamp01(1.15f - dd * 0.6f);
        return Sepia(val);
    });

    /// The fifth person — only the lens sees her. Transparent overlay for the family photo.
    public static Texture2D FamilyFifth(string name) => Save(name, 512, 340, (u, v) =>
    {
        float hx = 0.5f, hy = 0.2f;
        float a = 0f, val = 0f;
        if (Ell(u, v, hx, 0.62f, 0.1f, 0.42f) < 1f) { a = 0.95f; val = 0.04f; }
        if (Ell(u, v, hx, hy, 0.05f, 0.08f) < 1f)
        {
            a = 1f; val = 0.85f;
            if (Ell(u, v, hx - 0.018f, hy, 0.011f, 0.012f) < 1f || Ell(u, v, hx + 0.018f, hy, 0.011f, 0.012f) < 1f) val = 0f;
            if (Ell(u, v, hx, hy + 0.045f, 0.012f, 0.004f) < 1f) val = 0.1f;
        }
        if (Ell(u, v, hx, hy - 0.02f, 0.065f, 0.09f) < 1f && (v < hy - 0.04f || Mathf.Abs(u - hx) > 0.042f)) { a = 1f; val = 0.03f; }
        return new Color(val, val * 0.96f, val * 0.9f, a);
    }, true, false);

    public static Texture2D Handprint(string name) => Save(name, 256, 256, (u, v) =>
    {
        bool palm = Ell(u, v, 0.5f, 0.66f, 0.2f, 0.2f) < 1f;
        bool fingers = false;
        float[] fx = { 0.36f, 0.45f, 0.55f, 0.64f };
        float[] len = { 0.22f, 0.27f, 0.26f, 0.2f };
        for (int i = 0; i < 4; i++) if (Ell(u, v, fx[i], 0.46f - len[i] * 0.5f, 0.035f, len[i] * 0.5f) < 1f) fingers = true;
        bool thumb = Ell(u, v, 0.27f, 0.6f, 0.04f, 0.11f) < 1f;
        float a = (palm || fingers || thumb) ? 0.85f * (0.6f + 0.4f * Fbm(u, v, 12, 12, 3, 9)) : 0f;
        return new Color(0.5f, 0.05f, 0.04f, a);
    }, true, false);

    public static Texture2D Footprint(string name) => Save(name, 128, 256, (u, v) =>
    {
        bool sole = Ell(u, v, 0.5f, 0.33f, 0.32f, 0.27f) < 1f;
        bool heel = Ell(u, v, 0.5f, 0.8f, 0.24f, 0.15f) < 1f;
        float a = (sole || heel) ? 0.85f * (0.6f + 0.4f * Fbm(u, v, 8, 16, 3, 5)) : 0f;
        return new Color(0.45f, 0.05f, 0.04f, a);
    }, true, false);
}
