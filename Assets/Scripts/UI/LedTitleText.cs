using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// LED-style color cycling effect for a TextMeshPro title.
/// Each character independently fades between the game's shape palette colors
/// at random intervals, creating a lively marquee effect.
///
/// SETUP:
/// 1. Add a TextMeshPro - Text (UI) component to your title GameObject.
/// 2. Attach this script to the same GameObject.
/// 3. Tune the speed and interval values in the Inspector.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LedTitleText : MonoBehaviour
{
    [Header("Timing")]
    /// <summary>Minimum time in seconds before a character starts fading to a new color.</summary>
    public float minInterval = 0.08f;

    /// <summary>Maximum time in seconds before a character starts fading to a new color.</summary>
    public float maxInterval = 0.5f;

    /// <summary>How many seconds the fade transition between two colors takes.</summary>
    public float fadeDuration = 0.35f;

    [Header("Brightness")]
    /// <summary>Multiplier applied to palette colors. >1 makes them brighter/more saturated.</summary>
    [Range(0.5f, 2f)] public float brightness = 1.2f;

    // The palette reused from GameDefs — same colors as the shapes in the game
    static readonly Color[] Palette = new Color[]
    {
        new Color(1f, 0.10f, 0.10f), // Red
        new Color(1f, 0.55f, 0f),    // Orange
        new Color(1f, 0.92f, 0f),    // Yellow
        new Color(0.2f, 1f, 0.2f),   // Green
        new Color(0f, 0.9f, 0.9f),   // Cyan
        new Color(0.2f, 0.4f, 1f),   // Blue
        new Color(0.65f, 0.25f, 1f), // Purple
        new Color(1f, 0.25f, 0.7f),  // Pink
        new Color(1f, 1f, 1f),       // White
        new Color(0.7f, 1f, 0f),     // Lime
    };

    TMP_Text _tmp;

    // Per-character state
    Color[] _currentColors;
    Color[] _targetColors;
    float[] _fadeProgress;   // 0..1 per character
    float[] _fadeSpeed;      // cached 1/fadeDuration per character
    float[] _nextChangeTimer; // countdown until next color pick

void Awake()
{
    _tmp = GetComponent<TMP_Text>();

    // UIFadeManager controls the global alpha via CanvasGroup
    // without interfering with the per-character colors of the LED effect
    if (GetComponent<CanvasGroup>() == null)
        gameObject.AddComponent<CanvasGroup>();
}

    void OnEnable()
    {
        // Force mesh update so characterCount is accurate
        _tmp.ForceMeshUpdate();
        InitCharacterArrays();
    }

    void InitCharacterArrays()
    {
        int count = _tmp.textInfo.characterCount;
        if (count <= 0) return;

        _currentColors   = new Color[count];
        _targetColors    = new Color[count];
        _fadeProgress    = new float[count];
        _fadeSpeed       = new float[count];
        _nextChangeTimer = new float[count];

        for (int i = 0; i < count; i++)
        {
            _currentColors[i]   = RandomPaletteColor();
            _targetColors[i]    = RandomPaletteColor();
            _fadeProgress[i]    = 1f; // Start fully transitioned
            _fadeSpeed[i]       = 1f / Mathf.Max(fadeDuration, 0.01f);
            _nextChangeTimer[i] = Random.Range(minInterval, maxInterval);
        }
    }

    void Update()
    {
        _tmp.ForceMeshUpdate();

        int count = _tmp.textInfo.characterCount;

        // Reinitialize if character count changed (e.g. text was updated)
        if (_currentColors == null || _currentColors.Length != count)
            InitCharacterArrays();

        if (count <= 0) return;

        var textInfo = _tmp.textInfo;

        for (int i = 0; i < count; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            // Tick down the timer — when it hits zero, pick a new target color
            _nextChangeTimer[i] -= Time.deltaTime;
            if (_nextChangeTimer[i] <= 0f)
            {
                _currentColors[i]   = _targetColors[i]; // Snap to current target
                _targetColors[i]    = RandomPaletteColor();
                _fadeProgress[i]    = 0f;
                _nextChangeTimer[i] = Random.Range(minInterval, maxInterval);
            }

            // Advance the fade
            if (_fadeProgress[i] < 1f)
                _fadeProgress[i] = Mathf.Min(1f, _fadeProgress[i] + _fadeSpeed[i] * Time.deltaTime);

            // Interpolated color this frame
            Color col = Color.Lerp(_currentColors[i], _targetColors[i], _fadeProgress[i]);

            // Apply brightness boost
            col.r = Mathf.Clamp(col.r * brightness, 0f, 1f);
            col.g = Mathf.Clamp(col.g * brightness, 0f, 1f);
            col.b = Mathf.Clamp(col.b * brightness, 0f, 1f);
            col.a = 1f;

            // Write color to all 4 vertices of this character
            int meshIndex   = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;

            var colors = textInfo.meshInfo[meshIndex].colors32;
            colors[vertexIndex + 0] = col;
            colors[vertexIndex + 1] = col;
            colors[vertexIndex + 2] = col;
            colors[vertexIndex + 3] = col;
        }

        // Push the updated vertex colors to the mesh
        _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    Color RandomPaletteColor()
    {
        return Palette[Random.Range(0, Palette.Length)];
    }
}