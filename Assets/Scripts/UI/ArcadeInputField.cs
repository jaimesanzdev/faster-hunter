using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Arcade-style outline effect for TMP_InputField.
/// Mirrors ArcadeButton visuals: idle = white dim outline, focused = color cycling outline.
/// Cycles through the game's shape palette colors while the field is focused.
///
/// SETUP:
/// 1. Select your TMP_InputField GameObject.
/// 2. Set its Image color to black with ~10% alpha (nearly transparent background).
/// 3. Attach this script to the same GameObject.
/// 4. Done — outline is created automatically.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public class ArcadeInputField : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Colors")]
    /// <summary>Outline and text color when idle and unfocused.</summary>
    public Color idleColor    = new Color(1f, 1f, 1f, 0.4f);

    /// <summary>Background tint when idle.</summary>
    public Color idleBgColor  = new Color(1f, 1f, 1f, 0.04f);

    /// <summary>Background tint when hovered or focused.</summary>
    public Color activeBgColor = new Color(1f, 1f, 1f, 0.10f);

    [Header("Hover / Focus Cycle")]
    /// <summary>Palette cycle speed in cycles per second.</summary>
    public float cycleSpeed       = 1.2f;

    /// <summary>Speed of the color lerp transition.</summary>
    public float transitionSpeed  = 8f;

    [Header("Outline")]
    public float outlineThickness = 2f;

    // --- Internal ---
    TMP_InputField _input;
    UnityEngine.UI.Image _bg;
    UnityEngine.UI.Image _outlineTop, _outlineBottom, _outlineLeft, _outlineRight;

    bool  _hovered;
    float _cycleTimer;
    Color _currentOutlineColor;

    TMP_Text _textComponent;
    TMP_Text _placeholderComponent;

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
        _input = GetComponent<TMP_InputField>();

        _bg = GetComponent<UnityEngine.UI.Image>();
        if (_bg == null) _bg = gameObject.AddComponent<UnityEngine.UI.Image>();

        // Ensure a CanvasGroup exists — UIFadeManager controls global alpha through it
        if (GetComponent<CanvasGroup>() == null)
            gameObject.AddComponent<CanvasGroup>();
        _bg.color = idleBgColor;

        _currentOutlineColor = idleColor;

        _textComponent        = _input.textComponent;
        _placeholderComponent = _input.placeholder?.GetComponent<TMP_Text>();

        if (_placeholderComponent != null)
            _placeholderComponent.color = idleColor;

        BuildOutline();
        ResetState();
    }

    void OnEnable() => ResetState();

    void ResetState()
    {
        _hovered             = false;
        _cycleTimer          = 0f;
        _currentOutlineColor = idleColor;

        if (_bg != null) _bg.color = idleBgColor;
        if (_outlineTop != null)
        {
            _outlineTop.color    = idleColor;
            _outlineBottom.color = idleColor;
            _outlineLeft.color   = idleColor;
            _outlineRight.color  = idleColor;
        }
        if (_textComponent != null)        _textComponent.color        = idleColor;
        if (_placeholderComponent != null) _placeholderComponent.color = idleColor;
    }

    void BuildOutline()
    {
        _outlineTop    = CreateLine("OutlineTop");
        _outlineBottom = CreateLine("OutlineBottom");
        _outlineLeft   = CreateLine("OutlineLeft");
        _outlineRight  = CreateLine("OutlineRight");
        LayoutOutline();
    }

    UnityEngine.UI.Image CreateLine(string lineName)
    {
        var go  = new GameObject(lineName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(transform, false);
        go.transform.SetSiblingIndex(0);
        var img   = go.GetComponent<UnityEngine.UI.Image>();
        img.color = idleColor;
        return img;
    }

    void LayoutOutline()
    {
        var rt = GetComponent<RectTransform>();
        float w = rt.rect.width;
        float h = rt.rect.height;
        float t = outlineThickness;

        SetLine(_outlineTop,     0,  h * 0.5f - t * 0.5f, w, t);
        SetLine(_outlineBottom,  0, -h * 0.5f + t * 0.5f, w, t);
        SetLine(_outlineLeft,   -w * 0.5f + t * 0.5f, 0,  t, h);
        SetLine(_outlineRight,   w * 0.5f - t * 0.5f, 0,  t, h);
    }

    void SetLine(UnityEngine.UI.Image img, float x, float y, float w, float h)
    {
        var rt              = img.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
    }

    void Update()
    {
        LayoutOutline();

        // Active = hovered OR currently focused (user typing)
        bool active = _hovered || _input.isFocused;

        Color targetOutline;

        if (active)
        {
            _cycleTimer += Time.deltaTime * cycleSpeed;
            float t      = _cycleTimer % Palette.Length;
            int   indexA = (int)t % Palette.Length;
            int   indexB = (indexA + 1) % Palette.Length;
            float blend  = t - Mathf.Floor(t);
            targetOutline = Color.Lerp(Palette[indexA], Palette[indexB], blend);
        }
        else
        {
            _cycleTimer   = 0f;
            targetOutline = idleColor;
        }

        _currentOutlineColor = Color.Lerp(_currentOutlineColor, targetOutline, transitionSpeed * Time.deltaTime);

        _outlineTop.color    = _currentOutlineColor;
        _outlineBottom.color = _currentOutlineColor;
        _outlineLeft.color   = _currentOutlineColor;
        _outlineRight.color  = _currentOutlineColor;

        _bg.color = Color.Lerp(_bg.color, active ? activeBgColor : idleBgColor, transitionSpeed * Time.deltaTime);

        // User text cycles while focused
        if (_textComponent != null)
        {
            Color textTarget = _input.isFocused ? _currentOutlineColor : idleColor;
            _textComponent.color = Color.Lerp(_textComponent.color, textTarget, transitionSpeed * Time.deltaTime);
        }

        // Placeholder cycles on hover/focus, dims when idle
        if (_placeholderComponent != null)
        {
            Color placeholderTarget = active ? _currentOutlineColor : idleColor;
            _placeholderComponent.color = Color.Lerp(_placeholderComponent.color, placeholderTarget, transitionSpeed * Time.deltaTime);
        }
    }

    public void OnPointerEnter(PointerEventData _) => _hovered = true;
    public void OnPointerExit(PointerEventData _)  => _hovered = false;
}