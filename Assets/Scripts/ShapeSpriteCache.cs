using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static cache that procedurally generates and stores sprites for all shape types at runtime.
/// Sprites are created once using CPU-side texture painting and reused for the entire session.
/// All sprites are generated in white so they can be tinted at runtime via SpriteRenderer.color.
/// </summary>
public static class ShapeSpriteCache
{
    /// <summary>
    /// Resolution of each generated texture in pixels.
    /// Increase for sharper sprites (64 / 128 / 256). 128 is a good balance.
    /// </summary>
    public const int TEX_SIZE = 128;

    static bool _initialized;

    /// <summary>Stores the generated sprite for each shape type.</summary>
    static readonly Dictionary<ShapeId, Sprite> _sprites = new();

    /// <summary>
    /// Generates sprites for all shape types. Safe to call multiple times — only runs once.
    /// Should be called before any shape entity attempts to read from the cache.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _sprites[ShapeId.Circle]    = MakeCircleSprite("Circle");
        _sprites[ShapeId.Square]    = MakePolygonSprite("Square",    MakeSquare());
        _sprites[ShapeId.Triangle]  = MakePolygonSprite("Triangle",  MakeTriangle());
        _sprites[ShapeId.Pentagon]  = MakePolygonSprite("Pentagon",  MakeRegularPolygon(5, 0.50f));
        _sprites[ShapeId.Hexagon]   = MakePolygonSprite("Hexagon",   MakeRegularPolygon(6, 0.50f));
        _sprites[ShapeId.Diamond]   = MakePolygonSprite("Diamond",   MakeDiamond());
        _sprites[ShapeId.Trapezoid] = MakePolygonSprite("Trapezoid", MakeTrapezoid());
    }

    /// <summary>
    /// Returns the cached sprite for the given shape type.
    /// Initializes the cache automatically if not yet done.
    /// </summary>
    /// <param name="shape">The shape type to retrieve the sprite for.</param>
    public static Sprite Get(ShapeId shape)
    {
        if (!_initialized) Initialize();
        return _sprites[shape];
    }

    // --- Sprite builders ---

    /// <summary>
    /// Generates a white circle sprite by painting pixels within a circular radius.
    /// A light edge feather is applied to reduce aliasing.
    /// </summary>
    static Sprite MakeCircleSprite(string name)
    {
        var tex = NewTransparentTexture(name);
        int w = tex.width;
        int h = tex.height;

        // Compute center and radius in pixel space
        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float r  = Mathf.Min(w, h) * 0.42f; // Slight inset to leave a clean border
        float r2 = r * r;

        // Fill all pixels within the circular radius with white
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

        FeatherEdge(tex, 1);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TEX_SIZE);
    }

    /// <summary>
    /// Generates a white polygon sprite from a set of vertices in normalized [-0.5, 0.5] space.
    /// Uses a point-in-polygon test to fill pixels inside the shape boundary.
    /// A light edge feather is applied to reduce aliasing.
    /// </summary>
    /// <param name="name">Name assigned to the generated texture asset.</param>
    /// <param name="poly">Polygon vertices in normalized local space [-0.5, 0.5].</param>
    static Sprite MakePolygonSprite(string name, Vector2[] poly)
    {
        var tex = NewTransparentTexture(name);
        int w = tex.width;
        int h = tex.height;

        // Remap polygon vertices from [-0.5, 0.5] normalized space to pixel coordinates
        Vector2[] pts = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++)
        {
            float px = (poly[i].x + 0.5f) * (w - 1);
            float py = (poly[i].y + 0.5f) * (h - 1);
            pts[i] = new Vector2(px, py);
        }

        // Fill all pixels inside the polygon using a point-in-polygon test
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), pts))
                    tex.SetPixel(x, y, Color.white);

        FeatherEdge(tex, 1);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TEX_SIZE);
    }

    /// <summary>
    /// Creates a new fully transparent RGBA32 texture of the standard size.
    /// Used as the base canvas for all procedurally generated shape sprites.
    /// </summary>
    static Texture2D NewTransparentTexture(string name)
    {
        var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        tex.name      = $"Proc_{name}";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        // Initialize all pixels to fully transparent
        var clear = new Color32(0, 0, 0, 0);
        var arr   = new Color32[TEX_SIZE * TEX_SIZE];
        for (int i = 0; i < arr.Length; i++) arr[i] = clear;
        tex.SetPixels32(arr);

        return tex;
    }

    /// <summary>
    /// Applies a soft alpha feather to edge pixels to reduce hard aliasing on shape borders.
    /// For each opaque pixel adjacent to transparent pixels, the alpha is slightly reduced
    /// proportional to the density of transparent neighbors.
    /// Cheap enough for 128x128 textures and only runs once at initialization.
    /// </summary>
    /// <param name="tex">The texture to feather in-place.</param>
    /// <param name="radius">Neighborhood radius to sample for transparent neighbors.</param>
    static void FeatherEdge(Texture2D tex, int radius)
    {
        int w = tex.width;
        int h = tex.height;

        var src = tex.GetPixels32();
        var dst = new Color32[src.Length];
        src.CopyTo(dst, 0);

        int idx(int x, int y) => y * w + x;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var c = src[idx(x, y)];
                if (c.a == 0) continue; // Skip fully transparent pixels

                // Count transparent neighbors within the given radius
                int emptyNeighbors = 0;
                int total          = 0;

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

                // Reduce alpha slightly on border pixels based on neighbor transparency density
                if (emptyNeighbors > 0)
                {
                    float t    = Mathf.Clamp01(emptyNeighbors / (float)total);
                    byte newA  = (byte)Mathf.Clamp(c.a * (1f - 0.35f * t), 0f, 255f);
                    dst[idx(x, y)] = new Color32(255, 255, 255, newA);
                }
            }
        }

        tex.SetPixels32(dst);
    }

    /// <summary>
    /// Ray casting algorithm to determine if a point lies inside a polygon.
    /// Counts how many times a horizontal ray from the point crosses a polygon edge;
    /// an odd count means the point is inside.
    /// A small epsilon (1e-6) is added to the denominator to avoid division by zero.
    /// </summary>
    /// <param name="p">The point to test.</param>
    /// <param name="poly">The polygon vertices in pixel space.</param>
    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        int  n      = poly.Length;

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

    // --- Shape vertex definitions in normalized local space [-0.5, 0.5] ---
    // These are used only for sprite generation; collider paths are in ShapeColliderCache.

    /// <summary>Returns square vertices in normalized space.</summary>
    static Vector2[] MakeSquare() => new[]
    {
        new Vector2(-0.45f, -0.45f),
        new Vector2(-0.45f,  0.45f),
        new Vector2( 0.45f,  0.45f),
        new Vector2( 0.45f, -0.45f),
    };

    /// <summary>Returns equilateral triangle vertices in normalized space, tip pointing up.</summary>
    static Vector2[] MakeTriangle() => new[]
    {
        new Vector2( 0.00f,  0.48f),
        new Vector2(-0.45f, -0.40f),
        new Vector2( 0.45f, -0.40f),
    };

    /// <summary>Returns diamond (rhombus) vertices in normalized space.</summary>
    static Vector2[] MakeDiamond() => new[]
    {
        new Vector2( 0.00f,  0.50f),
        new Vector2(-0.42f,  0.00f),
        new Vector2( 0.00f, -0.50f),
        new Vector2( 0.42f,  0.00f),
    };

    /// <summary>Returns trapezoid vertices in normalized space, wider at the base.</summary>
    static Vector2[] MakeTrapezoid() => new[]
    {
        new Vector2(-0.48f, -0.45f),
        new Vector2(-0.26f,  0.45f),
        new Vector2( 0.26f,  0.45f),
        new Vector2( 0.48f, -0.45f),
    };

    /// <summary>
    /// Returns vertices for a regular polygon with the given number of sides and radius.
    /// The first vertex is placed at the top for a natural upright orientation.
    /// </summary>
    static Vector2[] MakeRegularPolygon(int sides, float radius)
    {
        var   pts    = new Vector2[sides];
        float step   = Mathf.PI * 2f / sides;
        float offset = Mathf.PI / 2f; // Top vertex

        for (int i = 0; i < sides; i++)
        {
            float a = offset + i * step;
            pts[i]  = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        return pts;
    }
}