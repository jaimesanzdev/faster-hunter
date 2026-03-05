using System.Collections.Generic;
using UnityEngine;

public static class ShapeColliderCache
{
    // Para 1 unidad, "justo": mantenemos todo algo dentro de 0.5
    public const float POLY_RADIUS = 0.46f;
    public const float CIRCLE_RADIUS = 0.45f;

    static bool _initialized;
    static readonly Dictionary<ShapeId, Vector2[]> _paths = new();

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _paths[ShapeId.Square] = MakeSquare();
        _paths[ShapeId.Triangle] = MakeTriangle();
        _paths[ShapeId.Pentagon] = MakeRegularPolygon(5, POLY_RADIUS);
        _paths[ShapeId.Hexagon] = MakeRegularPolygon(6, POLY_RADIUS);
        _paths[ShapeId.Diamond] = MakeDiamond();
        _paths[ShapeId.Trapezoid] = MakeTrapezoid();
        // Circle no usa polygon path
    }

    public static Vector2[] GetPolygonPath(ShapeId shape)
    {
        if (!_initialized) Initialize();
        return _paths[shape];
    }

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

    static Vector2[] MakeTriangle()
    {
        // Triángulo equilátero aproximado, "justo" y centrado
        // Punta arriba, base abajo
        return new[]
        {
            new Vector2( 0.00f,  POLY_RADIUS),
            new Vector2(-POLY_RADIUS, -POLY_RADIUS * 0.85f),
            new Vector2( POLY_RADIUS, -POLY_RADIUS * 0.85f),
        };
    }

    static Vector2[] MakeDiamond()
    {
        float r = POLY_RADIUS;
        return new[]
        {
            new Vector2( 0.0f,  r),
            new Vector2(-r,    0.0f),
            new Vector2( 0.0f, -r),
            new Vector2( r,    0.0f),
        };
    }

    static Vector2[] MakeTrapezoid()
    {
        // Base inferior más ancha, superior más estrecha.
        // Manteniendo dentro de r para ser "justo"
        float r = POLY_RADIUS;
        float top = r * 0.55f;
        return new[]
        {
            new Vector2(-r,   -r),
            new Vector2(-top,  r),
            new Vector2( top,  r),
            new Vector2( r,   -r),
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