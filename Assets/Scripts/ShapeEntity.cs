using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class ShapeEntity : NetworkBehaviour
{
    public NetworkVariable<int> Shape = new NetworkVariable<int>();
    public NetworkVariable<int> ColorIndex = new NetworkVariable<int>();

    SpriteRenderer _sr;
    PolygonCollider2D _poly;
    CircleCollider2D _circle;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _poly = GetComponent<PolygonCollider2D>();
        _circle = GetComponent<CircleCollider2D>();

        // Buenas defaults
        _poly.isTrigger = true;
        _circle.isTrigger = true;
    }

    public override void OnNetworkSpawn()
    {
        // Asegura caches
        ShapeSpriteCache.Initialize();
        ShapeColliderCache.Initialize();

        ApplyVisualsAndCollider();

        Shape.OnValueChanged += (_, __) => ApplyVisualsAndCollider();
        ColorIndex.OnValueChanged += (_, __) => ApplyVisualsAndCollider();
    }

    void ApplyVisualsAndCollider()
    {
        var shapeId = (ShapeId)Shape.Value;
        int c = ColorIndex.Value;

        // 1) Sprite + color
        _sr.sprite = ShapeSpriteCache.Get(shapeId);

        if (c >= 0 && c < GameDefs.Palette.Length)
            _sr.color = GameDefs.Palette[c];
        else
            _sr.color = Color.white;

        // 2) Collider "justo"
        if (shapeId == ShapeId.Circle)
        {
            _poly.enabled = false;

            _circle.enabled = true;
            _circle.radius = ShapeColliderCache.CIRCLE_RADIUS;
            _circle.offset = Vector2.zero;
        }
        else
        {
            _circle.enabled = false;

            _poly.enabled = true;
            _poly.pathCount = 1;
            _poly.SetPath(0, ShapeColliderCache.GetPolygonPath(shapeId));
            _poly.offset = Vector2.zero;
        }
    }

    public void ForceRefreshServerOnly()
    {
        // Solo para que el servidor vea sprite/collider actualizado inmediatamente
        // (en clientes se actualiza por replicación)
        // Puedes llamar internamente a ApplyVisualsAndCollider()
        ApplyVisualsAndCollider();
    }

    
}