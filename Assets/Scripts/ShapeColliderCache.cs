using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static cache that generates and stores polygon collider paths for all shape types.
/// Initialized once and reused throughout the session to avoid repeated allocations.
/// Circles use a CircleCollider2D and do not require a polygon path.
/// </summary>
public static class ShapeColliderCache
{
    /// <summary>
    /// Radius used for all polygon-based colliders.
    /// Kept slightly below 0.5 to produce a "tight" fit within a 1-unit object.
    /// </summary>
    public const float POLY_RADIUS = 0.46f;

    /// <summary>
    /// Radius used for the CircleCollider2D on circle shapes.
    /// </summary>
    public const float CIRCLE_RADIUS = 0.45f;

    static bool _initialized;

    /// <summary>Stores the polygon path (array of vertices) for each non-circle shape.</summary>
    static readonly Dictionary<ShapeId, Vector2[]> _paths = new();

    /// <summary>
    /// Initializes all polygon paths. Safe to call multiple times — only runs once.
    /// Should be called before any shape entities are spawned.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _paths[ShapeId.Square]    = MakeSquare();
        _paths[ShapeId.Triangle]  = MakeTriangle();
        _paths[ShapeId.Pentagon]  = MakeRegularPolygon(5, POLY_RADIUS);
        _paths[ShapeId.Hexagon]   = MakeRegularPolygon(6, POLY_RADIUS);
        _paths[ShapeId.Diamond]   = MakeDiamond();
        _paths[ShapeId.Trapezoid] = MakeTrapezoid();
        // Circle uses CircleCollider2D — no polygon path needed
    }

    /// <summary>
    /// Returns the cached polygon collider path for the given shape.
    /// Initializes the cache automatically if it has not been set up yet.
    /// </summary>
    /// <param name="shape">The shape type to retrieve the path for.</param>
    public static Vector2[] GetPolygonPath(ShapeId shape)
    {
        if (!_initialized) Initialize();
        return _paths[shape];
    }

    // --- Polygon path generators ---
    // All shapes are defined in local space, centered at the origin.
    // Vertices are wound counter-clockwise and kept within POLY_RADIUS.

    /// <summary>Generates a square polygon path centered at the origin.</summary>
    static Vector2[] MakeSquare()
    {
        float r = POLY_RADIUS;
        return new[]
        {
            new Vector2(-r, -r),
            new Vector2(-r,  r),
            new Vector2( r,  r),
            new Vector2( r, -r),
        };
    }

    /// <summary>
    /// Generates an approximate equilateral triangle polygon path.
    /// Tip pointing up, base at the bottom.
    /// </summary>
    static Vector2[] MakeTriangle()
    {
        return new[]
        {
            new Vector2( 0.00f,           POLY_RADIUS),
            new Vector2(-POLY_RADIUS,    -POLY_RADIUS * 0.85f),
            new Vector2( POLY_RADIUS,    -POLY_RADIUS * 0.85f),
        };
    }

    /// <summary>Generates a diamond (rhombus) polygon path centered at the origin.</summary>
    static Vector2[] MakeDiamond()
    {
        float r = POLY_RADIUS;
        return new[]
        {
            new Vector2( 0.0f,  r),
            new Vector2(-r,     0.0f),
            new Vector2( 0.0f, -r),
            new Vector2( r,     0.0f),
        };
    }

    /// <summary>
    /// Generates a trapezoid polygon path with a wider base and narrower top.
    /// Both top and bottom are kept within POLY_RADIUS.
    /// </summary>
    static Vector2[] MakeTrapezoid()
    {
        float r   = POLY_RADIUS;
        float top = r * 0.55f; // Top edge is 55% the width of the base

        return new[]
        {
            new Vector2(-r,   -r),
            new Vector2(-top,  r),
            new Vector2( top,  r),
            new Vector2( r,   -r),
        };
    }

    /// <summary>
    /// Generates a regular polygon with the given number of sides and radius.
    /// The first vertex is placed at the top (90° offset) for a natural upright orientation.
    /// </summary>
    /// <param name="sides">Number of sides (e.g. 5 for pentagon, 6 for hexagon).</param>
    /// <param name="radius">Circumradius of the polygon.</param>
    static Vector2[] MakeRegularPolygon(int sides, float radius)
    {
        var pts   = new Vector2[sides];
        float step   = Mathf.PI * 2f / sides;
        float offset = Mathf.PI / 2f; // Start at the top vertex

        for (int i = 0; i < sides; i++)
        {
            float a = offset + i * step;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        return pts;
    }
}