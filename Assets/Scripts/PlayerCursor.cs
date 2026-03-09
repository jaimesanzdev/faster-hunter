using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles local player input for shape selection and hover highlighting.
/// Only the owning client processes input; selection requests are sent to the server via RPC.
/// Supports both mouse (desktop) and touch (mobile) input through the Unity Input System.
/// </summary>
public class PlayerCursor : NetworkBehaviour
{
    [Header("Selection")]
    /// <summary>LayerMask used to filter overlap queries to only hit shapes.</summary>
    public LayerMask shapesMask;

    /// <summary>Radius of the overlap circle used for shape detection. Recommended range: 0.08–0.14.</summary>
    public float selectionRadius = 0.12f;

    /// <summary>Optional offset applied to the selection point in world space.</summary>
    public Vector2 selectionOffset = Vector2.zero;

    [Header("Hover highlight (local only)")]
    /// <summary>
    /// How much to blend the hovered shape's color toward white.
    /// Range 0–1. Recommended range: 0.2–0.35.
    /// </summary>
    [Range(0f, 1f)] public float hoverWhiten = 0.25f;

    /// <summary>Reference to the scene camera, used to convert screen to world coordinates.</summary>
    Camera _cam;

    /// <summary>The SpriteRenderer of the shape currently under the cursor.</summary>
    SpriteRenderer _hoveredSR;

    /// <summary>The original color of the hovered shape, saved so it can be restored on hover exit.</summary>
    Color _hoveredOriginalColor;

    /// <summary>
    /// Attempts to find the main camera on startup.
    /// Camera may not yet be available if spawned before the scene is fully loaded.
    /// </summary>
    void Awake()
    {
        _cam = Camera.main;
        if (_cam == null) _cam = FindFirstObjectByType<Camera>();
    }

    /// <summary>
    /// Processes player input each frame. Only runs for the owning client.
    /// Retries camera acquisition if it was not available at Awake time (e.g. after a scene load).
    /// </summary>
    void Update()
    {
        if (!IsOwner) return;

        // Retry camera acquisition — may be null if the object spawned before the scene camera was ready
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindFirstObjectByType<Camera>();
            if (_cam == null) return; // Camera not ready yet, skip this frame
        }

        // Update hover highlight based on current pointer position
        UpdateHover();

        // On click or tap, send a selection request to the server
        if (WasSelectPressedThisFrame(out Vector2 screenPos))
        {
            Vector2 worldPoint = _cam.ScreenToWorldPoint(screenPos);
            Debug.Log($"[INPUT] IsOwner={IsOwner} clicked at screen={screenPos}");

            if (GameRoundManager.Instance != null)
            {
                Debug.Log($"[CLICK] Sending RPC...");
                GameRoundManager.Instance.SelectAtServerRpc(worldPoint);
            }
            else
            {
                Debug.Log($"[CLICK] GameRoundManager.Instance is NULL");
            }
        }
    }

    /// <summary>
    /// Updates the hover highlight effect for the shape under the cursor.
    /// Restores the previous shape's color and applies a whitening blend to the newly hovered one.
    /// Only runs on the local client — purely visual with no network impact.
    /// </summary>
    void UpdateHover()
    {
        Vector2 screenPos = GetPointerScreenPosition();
        Vector2 worldPoint = _cam.ScreenToWorldPoint(screenPos);

        var sr = GetTopmostShapeSpriteRenderer(worldPoint);
        if (sr == _hoveredSR) return; // Same shape as before — nothing to update

        // Restore the previously hovered shape to its original color
        if (_hoveredSR != null)
            _hoveredSR.color = _hoveredOriginalColor;

        // Apply the highlight to the newly hovered shape
        _hoveredSR = sr;
        if (_hoveredSR != null)
        {
            _hoveredOriginalColor = _hoveredSR.color;
            _hoveredSR.color = Color.Lerp(_hoveredOriginalColor, Color.white, hoverWhiten);
        }
    }

    /// <summary>
    /// Performs an overlap circle query at the given world position and returns
    /// the SpriteRenderer of the topmost shape, determined by sorting order and Z position.
    /// </summary>
    /// <param name="worldPoint">The world-space position to query.</param>
    /// <returns>The SpriteRenderer of the topmost overlapping shape, or null if none found.</returns>
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

            // Prefer higher sorting order; use Z as a tiebreaker
            if (order > bestOrder || (order == bestOrder && z > bestZ))
            {
                best = sr;
                bestOrder = order;
                bestZ = z;
            }
        }

        return best;
    }

    /// <summary>
    /// Returns true if the player pressed the select input this frame, and outputs the screen position.
    /// Checks touch input first (mobile), then falls back to mouse (desktop).
    /// </summary>
    /// <param name="screenPos">The screen-space position of the press, if detected.</param>
    static bool WasSelectPressedThisFrame(out Vector2 screenPos)
    {
        // Touch input (mobile)
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = ts.primaryTouch.position.ReadValue();
            return true;
        }

        // Mouse input (desktop)
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPos = mouse.position.ReadValue();
            return true;
        }

        screenPos = default;
        return false;
    }

    /// <summary>
    /// Returns the current screen-space position of the pointer (touch or mouse).
    /// Used for continuous hover detection rather than press detection.
    /// </summary>
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

    /// <summary>
    /// Draws the selection circle in the Scene View at the current pointer position.
    /// Useful for tuning selectionRadius and selectionOffset during development.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (_cam == null) return;

        Vector2 screenPos = GetPointerScreenPosition();

        // Correctly set the Z depth based on the camera's distance from the world origin
        Vector3 sp = new Vector3(
            screenPos.x,
            screenPos.y,
            -_cam.transform.position.z
        );

        Vector2 worldPoint = _cam.ScreenToWorldPoint(sp);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(worldPoint + selectionOffset, selectionRadius);
    }

    /// <summary>
    /// Client RPC sent exclusively to the player who scored points.
    /// Displays the score popup at the captured shape's position with the shape's color.
    /// Only the scoring client receives and executes this call.
    /// </summary>
    [Rpc(SendTo.Owner)]
    public void ShowScorePopupOwnerRpc(int points, int colorIndex, Vector3 worldPos)
    {
        if (ScorePopup.Instance != null)
            ScorePopup.Instance.Show(points, GameDefs.Palette[colorIndex], worldPos);
    }
}