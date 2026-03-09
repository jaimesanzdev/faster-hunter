using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Arcade-style outline button that cycles through the game's color palette on hover.
/// No fill — just a sharp glowing border and text that lights up on hover.
/// Attach to any Button GameObject alongside an Image (for the outline) and a TMP_Text child.
///
/// SETUP:
/// 1. Create a UI Button in your Canvas.
/// 2. Remove the default Sprite from the Button's Image component (leave it as a solid color).
/// 3. Set the Image color to fully transparent (alpha = 0) — the outline is drawn separately.
/// 4. Add a second Image as a child named "Outline" — this will be the border.
///    Set its Image Type to "Sliced" and use a border sprite, OR leave it as a solid
///    color block and use the OutlineThickness approach below.
/// 5. Attach this script to the Button GameObject.
/// 6. Assign the TMP_Text child and the Outline Image in the Inspector.
///
/// SIMPLER SETUP (recommended):
/// 1. Create a UI Panel, set Image color to dark semi-transparent.
/// 2. Add TMP_Text child.
/// 3. Attach this script — it creates the outline GameObject automatically.
/// </summary>
[RequireComponent(typeof(Button))]
public class ArcadeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References")]
    /// <summary>The button label. Auto-found in children if not assigned.</summary>
    public TMP_Text label;

    [Header("Colors")]
    /// <summary>Text and outline color when the button is idle.</summary>
    public Color idleColor   = new Color(1f, 1f, 1f, 0.55f);

    /// <summary>Background tint when idle (very dark, nearly invisible).</summary>
    public Color idleBgColor = new Color(1f, 1f, 1f, 0.04f);

    /// <summary>Background tint when hovered.</summary>
    public Color hoverBgColor = new Color(1f, 1f, 1f, 0.10f);

    [Header("Hover Cycle")]
    /// <summary>How fast the outline color cycles through the palette on hover (cycles per second).</summary>
    public float cycleSpeed = 1.2f;

    /// <summary>How fast the color transitions when entering/exiting hover.</summary>
    public float transitionSpeed = 8f;

    [Header("Outline")]
    public float outlineThickness = 2f;

    // --- Internal ---
    Image          _bg;
    Image          _outlineTop, _outlineBottom, _outlineLeft, _outlineRight;
    Button         _button;
    bool           _hovered;
    float          _cycleTimer;
    Color          _currentOutlineColor;
    Coroutine      _clickPunch;

    // Same palette as the game shapes
    static readonly Color[] Palette =
    {
        new Color(1f, 0.10f, 0.10f),
        new Color(1f, 0.55f, 0f),
        new Color(1f, 0.92f, 0f),
        new Color(0.2f, 1f, 0.2f),
        new Color(0f, 0.9f, 0.9f),
        new Color(0.2f, 0.4f, 1f),
        new Color(0.65f, 0.25f, 1f),
        new Color(1f, 0.25f, 0.7f),
        new Color(1f, 1f, 1f),
        new Color(0.7f, 1f, 0f),
    };

    void Awake()
    {
        _button = GetComponent<Button>();
        _button.transition = Selectable.Transition.None;

        _bg = GetComponent<Image>();
        if (_bg == null) _bg = gameObject.AddComponent<Image>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>();

        // Ensure a CanvasGroup exists — UIFadeManager controls global alpha through it,
        // while ArcadeButton only touches individual element colors (no alpha conflicts).
        if (GetComponent<CanvasGroup>() == null)
            gameObject.AddComponent<CanvasGroup>();

        BuildOutline();
        ResetState();
    }

    void OnEnable() => ResetState();

    void ResetState()
    {
        _hovered             = false;
        _cycleTimer          = 0f;
        _currentOutlineColor = idleColor;

        if (_bg != null)   _bg.color   = idleBgColor;
        if (label != null) label.color = idleColor;
        ApplyColor(idleColor);
    }

    /// <summary>Creates 4 thin Image lines as the button border.</summary>
    void BuildOutline()
    {
        _outlineTop    = CreateOutlineLine("OutlineTop");
        _outlineBottom = CreateOutlineLine("OutlineBottom");
        _outlineLeft   = CreateOutlineLine("OutlineLeft");
        _outlineRight  = CreateOutlineLine("OutlineRight");

        LayoutOutline();
    }

    Image CreateOutlineLine(string lineName)
    {
        var go  = new GameObject(lineName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        // Place outline behind the label
        go.transform.SetSiblingIndex(0);

        var img   = go.GetComponent<Image>();
        img.color = idleColor;
        return img;
    }

    void LayoutOutline()
    {
        var rt = GetComponent<RectTransform>();
        float w = rt.rect.width;
        float h = rt.rect.height;
        float t = outlineThickness;

        SetLine(_outlineTop,    0,  h * 0.5f - t * 0.5f, w, t);
        SetLine(_outlineBottom, 0, -h * 0.5f + t * 0.5f, w, t);
        SetLine(_outlineLeft,  -w * 0.5f + t * 0.5f, 0, t, h);
        SetLine(_outlineRight,  w * 0.5f - t * 0.5f, 0, t, h);
    }

    void SetLine(Image img, float x, float y, float w, float h)
    {
        var rt         = img.GetComponent<RectTransform>();
        rt.anchorMin   = new Vector2(0.5f, 0.5f);
        rt.anchorMax   = new Vector2(0.5f, 0.5f);
        rt.pivot       = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta   = new Vector2(w, h);
    }

    void Update()
    {
        // Re-layout outline each frame to handle any RectTransform changes
        LayoutOutline();

        Color targetOutline;

        if (_hovered)
        {
            // Cycle through palette colors smoothly using a sine-interpolated index
            _cycleTimer += Time.deltaTime * cycleSpeed;
            float t          = _cycleTimer % Palette.Length;
            int   indexA     = (int)t % Palette.Length;
            int   indexB     = (indexA + 1) % Palette.Length;
            float blend      = t - Mathf.Floor(t);
            targetOutline    = Color.Lerp(Palette[indexA], Palette[indexB], blend);
        }
        else
        {
            targetOutline = idleColor;
        }

        // Smooth transition toward target color
        _currentOutlineColor = Color.Lerp(_currentOutlineColor, targetOutline, transitionSpeed * Time.deltaTime);

        // Apply to all outline lines and label
        ApplyColor(_currentOutlineColor);

        // Background tint
        _bg.color = Color.Lerp(_bg.color, _hovered ? hoverBgColor : idleBgColor, transitionSpeed * Time.deltaTime);
    }

    void ApplyColor(Color col)
    {
        _outlineTop.color    = col;
        _outlineBottom.color = col;
        _outlineLeft.color   = col;
        _outlineRight.color  = col;

        if (label != null)
        {
            var lc = col;
            lc.a       = _hovered ? 1f : 0.75f;
            label.color = Color.Lerp(label.color, lc, transitionSpeed * Time.deltaTime);
        }
    }

    public void OnPointerEnter(PointerEventData _)
    {
        _hovered = true;
    }

    public void OnPointerExit(PointerEventData _)
    {
        _hovered    = false;
        _cycleTimer = 0f;
    }

    public void OnPointerClick(PointerEventData _)
    {
        if (_clickPunch != null) StopCoroutine(_clickPunch);
        _clickPunch = StartCoroutine(ClickPunch());
    }

    /// <summary>Brief scale punch on click for tactile feedback.</summary>
    IEnumerator ClickPunch()
    {
        float duration = 0.12f;
        float elapsed  = 0f;
        Vector3 originalScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / duration;
            // Quick scale up then back
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.06f;
            transform.localScale = originalScale * scale;
            yield return null;
        }

        transform.localScale = originalScale;
    }
}