using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCursor : NetworkBehaviour
{
    [Header("Selection")]
    public LayerMask shapesMask;                       // pon aquí "Shapes"
    public float selectionRadius = 0.12f;
    public Vector2 selectionOffset = Vector2.zero;

    [Header("Hover highlight (local only)")]
    [Range(0f, 1f)] public float hoverWhiten = 0.25f;  // 0.2–0.35 suele ir bien

    Camera _cam;
    SpriteRenderer _hoveredSR;
    Color _hoveredOriginalColor;

    void Awake()
    {
        _cam = Camera.main;
        if (_cam == null) _cam = FindFirstObjectByType<Camera>();
    }

    void Update()
    {
        if (!IsOwner) return;
        if (_cam == null) return;

        // 1) Hover highlight (local)
        UpdateHover();

        // 2) Click / tap -> server selection
        if (WasSelectPressedThisFrame(out Vector2 screenPos))
        {
            Vector2 worldPoint = _cam.ScreenToWorldPoint(screenPos);
            if (GameRoundManager.Instance != null)
                GameRoundManager.Instance.SelectAtServerRpc(worldPoint);
        }
    }

    void UpdateHover()
    {
        Vector2 screenPos = GetPointerScreenPosition();
        Vector2 worldPoint = _cam.ScreenToWorldPoint(screenPos);

        var sr = GetTopmostShapeSpriteRenderer(worldPoint);
        if (sr == _hoveredSR) return;

        // restore previous
        if (_hoveredSR != null)
            _hoveredSR.color = _hoveredOriginalColor;

        _hoveredSR = sr;
        if (_hoveredSR != null)
        {
            _hoveredOriginalColor = _hoveredSR.color;
            _hoveredSR.color = Color.Lerp(_hoveredOriginalColor, Color.white, hoverWhiten);
        }
    }

    SpriteRenderer GetTopmostShapeSpriteRenderer(Vector2 worldPoint)
    {
        Vector2 p = worldPoint + selectionOffset;
        var hits = Physics2D.OverlapCircleAll(p, selectionRadius, shapesMask);
        if (hits == null || hits.Length == 0) return null;

        SpriteRenderer best = null;
        int bestOrder = int.MinValue;
        float bestZ = float.MinValue;

        foreach (var h in hits)
        {
            var sr = h.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            int order = sr.sortingOrder;
            float z = h.transform.position.z;

            if (order > bestOrder || (order == bestOrder && z > bestZ))
            {
                best = sr;
                bestOrder = order;
                bestZ = z;
            }
        }

        return best;
    }

    static bool WasSelectPressedThisFrame(out Vector2 screenPos)
    {
        // Touch (mobile)
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = ts.primaryTouch.position.ReadValue();
            return true;
        }

        // Mouse (desktop)
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPos = mouse.position.ReadValue();
            return true;
        }

        screenPos = default;
        return false;
    }

    static Vector2 GetPointerScreenPosition()
    {
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.isPressed)
            return ts.primaryTouch.position.ReadValue();

        var mouse = Mouse.current;
        if (mouse != null)
            return mouse.position.ReadValue();

        return Vector2.zero;
    }

    // Para visualizar la caja de selección en Scene View mientras juegas (opcional)
    void OnDrawGizmosSelected()
    {
        if (_cam == null) return;

        Vector2 screenPos = GetPointerScreenPosition();

        // IMPORTANTE: ajustar Z correctamente
        Vector3 sp = new Vector3(
            screenPos.x,
            screenPos.y,
            -_cam.transform.position.z
        );

        Vector2 worldPoint = _cam.ScreenToWorldPoint(sp);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(worldPoint + selectionOffset, selectionRadius);
    }
}