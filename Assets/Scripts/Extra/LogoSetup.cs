using UnityEngine;

/// <summary>
/// TEMPORARY — Only for logo capture. Delete after use.
/// Spawns static colored shapes at fixed positions for a clean logo screenshot.
/// Attach to any GameObject in the Game scene, disable GameRoundManager and Canvas UI before entering Play.
/// </summary>
public class LogoSetup : MonoBehaviour
{
    void Start()
    {
        ShapeSpriteCache.Initialize();

        // Each entry: ShapeId, color from palette, world position, scale
        var shapes = new (ShapeId shape, int colorIndex, Vector2 pos, float scale)[]
        {
            (ShapeId.Hexagon,   5, new Vector2(-2f,  1.2f), 1.2f), // Blue
            (ShapeId.Circle,    3, new Vector2( 0.0f,  1.5f), 1.0f), // Green
            (ShapeId.Diamond,   7, new Vector2( 0.8f,  0.75f), 1f), // Pink
            (ShapeId.Triangle,  1, new Vector2(-1.8f, -0.6f), 1f), // Orange
            (ShapeId.Pentagon,  6, new Vector2( -0.5f, -1.2f), 1.0f), // Purple
            (ShapeId.Square,    2, new Vector2( 0.0f, -0.2f), 0.9f), // Yellow
            (ShapeId.Trapezoid, 4, new Vector2(-0.8f,  0.5f), 0.85f),// Cyan
        };

        foreach (var s in shapes)
        {
            var go = new GameObject($"Logo_{s.shape}");
            go.transform.position   = s.pos;
            go.transform.localScale = Vector3.one * s.scale;

            var sr      = go.AddComponent<SpriteRenderer>();
            sr.sprite   = ShapeSpriteCache.Get(s.shape);
            sr.color    = GameDefs.Palette[s.colorIndex];
            sr.sortingOrder = 1;
        }
    }
}