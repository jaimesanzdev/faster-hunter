using System.Collections.Generic;
using UnityEngine;

public static class ShapeSpriteCache
{
    // Ajusta si quieres más nitidez (64/128/256)
    public const int TEX_SIZE = 128;

    static bool _initialized;
    static readonly Dictionary<ShapeId, Sprite> _sprites = new();

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _sprites[ShapeId.Circle] = MakeCircleSprite("Circle");
        _sprites[ShapeId.Square] = MakePolygonSprite("Square", MakeSquare());
        _sprites[ShapeId.Triangle] = MakePolygonSprite("Triangle", MakeTriangle());
        _sprites[ShapeId.Pentagon] = MakePolygonSprite("Pentagon", MakeRegularPolygon(5, 0.50f));
        _sprites[ShapeId.Hexagon] = MakePolygonSprite("Hexagon", MakeRegularPolygon(6, 0.50f));
        _sprites[ShapeId.Diamond] = MakePolygonSprite("Diamond", MakeDiamond());
        _sprites[ShapeId.Trapezoid] = MakePolygonSprite("Trapezoid", MakeTrapezoid());
    }

    public static Sprite Get(ShapeId shape)
    {
        if (!_initialized) Initialize();
        return _sprites[shape];
    }

    // ---------- Sprite builders ----------

    static Sprite MakeCircleSprite(string name)
    {
        var tex = NewTransparentTexture(name);
        int w = tex.width;
        int h = tex.height;

        // Centro y radio en píxeles
        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float r = Mathf.Min(w, h) * 0.42f; // deja borde
        float r2 = r * r;

        // Relleno (blanco)
        for (int y = 0; y < h; y++)
        {
            float dy = y - cy;
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                if (dx * dx + dy * dy <= r2)
                    tex.SetPixel(x, y, Color.white);
            }
        }

        // Suavizado simple de borde (opcional): alpha feather
        FeatherEdge(tex, 1);

        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TEX_SIZE);
    }

    static Sprite MakePolygonSprite(string name, Vector2[] poly)
    {
        var tex = NewTransparentTexture(name);
        int w = tex.width;
        int h = tex.height;

        // Convertimos polígono de espacio [-0.5..0.5] a píxeles con margen
        // Mapeo: -0.5..0.5 -> 0..w-1
        Vector2[] pts = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            float px = (poly[i].x + 0.5f) * (w - 1);
            float py = (poly[i].y + 0.5f) * (h - 1);
            pts[i] = new Vector2(px, py);
        }

        // Relleno por test "point in polygon"
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), pts))
                    tex.SetPixel(x, y, Color.white);
            }
        }

        FeatherEdge(tex, 1);

        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TEX_SIZE);
    }

    static Texture2D NewTransparentTexture(string name)
    {
        var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        tex.name = $"Proc_{name}";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        // Clear a transparente
        var clear = new Color32(0, 0, 0, 0);
        var arr = new Color32[TEX_SIZE * TEX_SIZE];
        for (int i = 0; i < arr.Length; i++) arr[i] = clear;
        tex.SetPixels32(arr);

        return tex;
    }

    // Suaviza borde: reduce alpha en vecindad de píxeles transparentes.
    // Muy barato para 128x128 y se hace una vez.
    static void FeatherEdge(Texture2D tex, int radius)
    {
        int w = tex.width;
        int h = tex.height;

        // Copia alpha inicial
        var src = tex.GetPixels32();
        var dst = new Color32[src.Length];
        src.CopyTo(dst, 0);

        int idx(int x, int y) => y * w + x;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = src[idx(x, y)];
                if (c.a == 0) continue;

                // Si alrededor hay transparencia, bajamos un poco alpha
                int emptyNeighbors = 0;
                int total = 0;
                for (int oy = -radius; oy <= radius; oy++)
                {
                    int yy = y + oy;
                    if (yy < 0 || yy >= h) continue;
                    for (int ox = -radius; ox <= radius; ox++)
                    {
                        int xx = x + ox;
                        if (xx < 0 || xx >= w) continue;
                        total++;
                        if (src[idx(xx, yy)].a == 0) emptyNeighbors++;
                    }
                }

                // Feather leve
                if (emptyNeighbors > 0)
                {
                    float t = Mathf.Clamp01(emptyNeighbors / (float)total);
                    byte newA = (byte)Mathf.Clamp(c.a * (1f - 0.35f * t), 0f, 255f);
                    dst[idx(x, y)] = new Color32(255, 255, 255, newA);
                }
            }
        }

        tex.SetPixels32(dst);
    }

    // Ray casting algorithm
    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        int n = poly.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[j];
            bool intersect = ((a.y > p.y) != (b.y > p.y)) &&
                             (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y + 1e-6f) + a.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    // ---------- Shape definitions in local space [-0.5..0.5] ----------

    static Vector2[] MakeSquare()
    {
        return new[]
        {
            new Vector2(-0.45f, -0.45f),
            new Vector2(-0.45f,  0.45f),
            new Vector2( 0.45f,  0.45f),
            new Vector2( 0.45f, -0.45f),
        };
    }

    static Vector2[] MakeTriangle()
    {
        return new[]
        {
            new Vector2( 0.00f,  0.48f),
            new Vector2(-0.45f, -0.40f),
            new Vector2( 0.45f, -0.40f),
        };
    }

    static Vector2[] MakeDiamond()
    {
        return new[]
        {
            new Vector2( 0.00f,  0.50f),
            new Vector2(-0.42f,  0.00f),
            new Vector2( 0.00f, -0.50f),
            new Vector2( 0.42f,  0.00f),
        };
    }

    static Vector2[] MakeTrapezoid()
    {
        // base inferior más ancha
        return new[]
        {
            new Vector2(-0.48f, -0.45f),
            new Vector2(-0.26f,  0.45f),
            new Vector2( 0.26f,  0.45f),
            new Vector2( 0.48f, -0.45f),
        };
    }

    static Vector2[] MakeRegularPolygon(int sides, float radius)
    {
        var pts = new Vector2[sides];
        float step = Mathf.PI * 2f / sides;
        float offset = Mathf.PI / 2f; // vértice arriba
        for (int i = 0; i < sides; i++)
        {
            float a = offset + i * step;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
        return pts;
    }
}