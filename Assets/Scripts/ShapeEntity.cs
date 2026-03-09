using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked component that represents a single shape object in the game world.
/// Stores the shape type and color as NetworkVariables, which are replicated to all clients.
/// Applies the correct sprite and collider geometry whenever the visual state changes.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ShapeEntity : NetworkBehaviour
{
    /// <summary>
    /// The shape type index, mapped to <see cref="ShapeId"/>.
    /// Replicated to all clients. Changes trigger a visual and collider refresh.
    /// </summary>
    public NetworkVariable<int> Shape = new NetworkVariable<int>();

    /// <summary>
    /// The color index into <see cref="GameDefs.Palette"/>.
    /// Replicated to all clients. Changes trigger a visual refresh.
    /// </summary>
    public NetworkVariable<int> ColorIndex = new NetworkVariable<int>();

    SpriteRenderer _sr;
    PolygonCollider2D _poly;
    CircleCollider2D _circle;

    /// <summary>
    /// Caches component references and sets both colliders as triggers by default.
    /// </summary>
    void Awake()
    {
        _sr     = GetComponent<SpriteRenderer>();
        _poly   = GetComponent<PolygonCollider2D>();
        _circle = GetComponent<CircleCollider2D>();

        // Both colliders act as triggers — they detect overlaps but do not block physics
        _poly.isTrigger   = true;
        _circle.isTrigger = true;
    }

    /// <summary>
    /// Called on all clients when the object is spawned on the network.
    /// Ensures sprite and collider caches are ready, applies initial visuals,
    /// and subscribes to NetworkVariable change callbacks for live updates.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // Ensure procedural sprite and collider data is generated before applying visuals
        ShapeSpriteCache.Initialize();
        ShapeColliderCache.Initialize();

        // Apply initial visual state based on the current NetworkVariable values
        ApplyVisualsAndCollider();

        // Re-apply visuals whenever the shape or color changes on any client
        Shape.OnValueChanged      += (_, __) => ApplyVisualsAndCollider();
        ColorIndex.OnValueChanged += (_, __) => ApplyVisualsAndCollider();
    }

    /// <summary>
    /// Applies the correct sprite, color, and collider geometry based on the current
    /// Shape and ColorIndex values. Switches between CircleCollider2D and PolygonCollider2D
    /// depending on the shape type — only one is active at a time.
    /// </summary>
    void ApplyVisualsAndCollider()
    {
        var shapeId = (ShapeId)Shape.Value;
        int c = ColorIndex.Value;

        // 1) Assign the procedurally generated sprite and tint it with the palette color
        _sr.sprite = ShapeSpriteCache.Get(shapeId);
        _sr.color  = (c >= 0 && c < GameDefs.Palette.Length) ? GameDefs.Palette[c] : Color.white;

        // 2) Activate the appropriate collider type and configure its geometry
        if (shapeId == ShapeId.Circle)
        {
            // Circles use a CircleCollider2D for accurate round hit detection
            _poly.enabled = false;

            _circle.enabled = true;
            _circle.radius  = ShapeColliderCache.CIRCLE_RADIUS;
            _circle.offset  = Vector2.zero;
        }
        else
        {
            // All other shapes use a PolygonCollider2D with pre-computed vertex paths
            _circle.enabled = false;

            _poly.enabled   = true;
            _poly.pathCount = 1;
            _poly.SetPath(0, ShapeColliderCache.GetPolygonPath(shapeId));
            _poly.offset    = Vector2.zero;
        }
    }

    /// <summary>
    /// Forces an immediate visual and collider refresh on the server.
    /// Called after setting NetworkVariable values to ensure the server-side representation
    /// is up to date before clients receive the replicated state.
    /// </summary>
    public void ForceRefreshServerOnly()
    {
        ApplyVisualsAndCollider();
    }
}