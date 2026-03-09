using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays a floating "+X" score popup at a world position.
/// Entry: punch scale (small → big → settle) with simultaneous fade in.
/// Exit: float up with fade out.
///
/// SETUP:
/// 1. Create a Canvas in your Game scene with Render Mode = "World Space".
///    Set its scale to something small like 0.01 so it maps to world units.
/// 2. Create a child GameObject with a TMP_Text component — style it how you want
///    (font, size, outline via TMP settings). Save it as a prefab: ScorePopupPrefab.
/// 3. Create an empty GameObject "ScorePopupManager" and attach this script.
/// 4. Assign the prefab to the ScorePopupPrefab field in the Inspector.
/// 5. Call ScorePopup.Instance.Show(points, color, worldPosition) when a target is hit.
/// </summary>
public class ScorePopup : MonoBehaviour
{
    public static ScorePopup Instance { get; private set; }

    [Header("Canvas")]
    /// <summary>Reference to the Canvas with World Space rendering.</summary>
    public Canvas WorldCanvas;

    [Header("Prefab")]
    /// <summary>TMP_Text prefab used for each popup. Should be a child of a World Space Canvas.</summary>
    public GameObject ScorePopupPrefab;

    [Header("Entry — Punch Scale")]
    /// <summary>Duration of the punch scale + fade in phase.</summary>
    public float entryDuration  = 0.25f;

    /// <summary>How much bigger than normal the text gets at peak scale (1.0 = normal size).</summary>
    public float punchOvershoot = 1.35f;

    /// <summary>How long the text stays at full size before starting to float up.</summary>
    public float holdDuration   = 0.15f;

    [Header("Exit — Float Up + Fade Out")]
    /// <summary>Duration of the float up + fade out phase.</summary>
    public float exitDuration   = 0.55f;

    /// <summary>How many world units the text rises during the exit phase.</summary>
    public float floatDistance  = 1.2f;

    [Header("Visuals")]
    /// <summary>Base font size of the popup text.</summary>
    public float fontSize       = 5f;

    /// <summary>
    /// Outline color applied to the TMP text.
    /// A dark semi-transparent outline keeps the text readable on any background.
    /// </summary>
    public Color outlineColor   = new Color(0f, 0f, 0f, 0.85f);

    /// <summary>Outline thickness. TMP uses 0–1 range.</summary>
    [Range(0f, 1f)] public float outlineWidth = 0.3f;

    void Awake() => Instance = this;

    /// <summary>
    /// Spawns a score popup at the given world position.
    /// </summary>
    /// <param name="points">The point value to display (shown as "+X").</param>
    /// <param name="shapeColor">The color of the shape that was clicked — used to tint the text.</param>
    /// <param name="worldPos">World position where the popup appears (typically the shape's position).</param>
    public void Show(int points, Color shapeColor, Vector3 worldPos)
    {
        Debug.Log($"[POPUP] Show called, points={points}, pos={worldPos}");
        
        if (ScorePopupPrefab == null)
        {
            Debug.LogError("[POPUP] Prefab es NULL");
            return;
        }

        var go = Instantiate(ScorePopupPrefab, worldPos, Quaternion.identity, WorldCanvas.transform);
        var tmp = go.GetComponent<TMP_Text>();
        if (tmp == null)
            tmp = go.GetComponentInChildren<TMP_Text>();

        Debug.Log($"[POPUP] tmp={tmp}, go={go.name}");
        
        if (tmp == null)
        {
            Debug.LogError("[POPUP] No TMP_Text encontrado en el prefab");
            Destroy(go);
            return;
        }

        Debug.Log($"[POPUP] texto='{tmp.text}', color={tmp.color}, alpha={tmp.color.a}");
        // Configure text content and style
        tmp.text     = $"+{points}";
        tmp.fontSize = fontSize;

        // Brighten the shape color slightly so it pops against the dark background
        Color brightColor = new Color(
            Mathf.Clamp(shapeColor.r * 1.15f, 0f, 1f),
            Mathf.Clamp(shapeColor.g * 1.15f, 0f, 1f),
            Mathf.Clamp(shapeColor.b * 1.15f, 0f, 1f),
            1f
        );
        tmp.color = brightColor;

        // Apply outline for readability (requires TMP outline shader feature)
        tmp.outlineColor = outlineColor;
        tmp.outlineWidth = outlineWidth;

        // Run the animation coroutine
        StartCoroutine(AnimatePopup(go.transform, tmp));
    }

    IEnumerator AnimatePopup(Transform t, TMP_Text tmp)
    {
        // --- ENTRY: punch scale + fade in ---
        // Curve: scale goes 0 → punchOvershoot → 1, alpha goes 0 → 1

        float elapsed = 0f;
        while (elapsed < entryDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / entryDuration);

            // Punch scale curve: rises fast to overshoot, settles back to 1
            // Uses a custom ease: sin-based overshoot
            float scale = PunchCurve(progress, punchOvershoot);
            t.localScale = Vector3.one * scale;

            // Fade in: quick linear ramp to full opacity
            var c = tmp.color; c.a = Mathf.Clamp01(progress * 2.5f); tmp.color = c;

            yield return null;
        }

        // Ensure exact final state after entry
        t.localScale = Vector3.one;
        var fc = tmp.color; fc.a = 1f; tmp.color = fc;

        // --- HOLD: stay visible briefly ---
        yield return new WaitForSeconds(holdDuration);

        // --- EXIT: float up + fade out ---
        Vector3 startPos = t.position;
        Vector3 endPos   = startPos + Vector3.up * floatDistance;

        elapsed = 0f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / exitDuration);

            // Ease out the upward movement (fast at start, slow at end)
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);
            t.position = Vector3.Lerp(startPos, endPos, easedProgress);

            // Fade out starts after 30% of exit duration so the text is readable longer
            float fadeProgress = Mathf.Clamp01((progress - 0.3f) / 0.7f);
            var c = tmp.color; c.a = 1f - fadeProgress; tmp.color = c;

            yield return null;
        }

        Destroy(t.gameObject);
    }

    /// <summary>
    /// Generates a punch scale curve value for a given progress [0,1].
    /// Rises quickly to <paramref name="overshoot"/>, then settles back to 1.
    /// </summary>
    static float PunchCurve(float t, float overshoot)
    {
        // Phase 1 (0→0.6): grow from 0 to overshoot
        // Phase 2 (0.6→1): settle from overshoot back to 1
        if (t < 0.6f)
        {
            float p = t / 0.6f;
            return Mathf.Lerp(0f, overshoot, EaseOutQuad(p));
        }
        else
        {
            float p = (t - 0.6f) / 0.4f;
            return Mathf.Lerp(overshoot, 1f, EaseOutQuad(p));
        }
    }

    static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
}