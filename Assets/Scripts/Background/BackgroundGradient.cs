using UnityEngine;

/// <summary>
/// URP 2D compatible animated gradient background.
/// Generates a gradient texture and applies it to a SpriteRenderer that fills the screen.
/// This approach works correctly in Unity URP 2D without any pipeline callbacks.
///
/// SETUP:
/// 1. Create an empty GameObject named "Background" in your scene.
/// 2. Attach this script to it.
/// 3. Set its Z position to something behind your shapes, e.g. Z = 10.
/// 4. Set Camera Clear Flags to "Solid Color" and Background to pure black.
/// 5. Optionally call BackgroundGradient.Instance.TriggerRoundTransition()
///    from GameRoundManager.StartNewRound().
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundGradient : MonoBehaviour
{
    public static BackgroundGradient Instance { get; private set; }

    [Header("Base Colors")]
    public Color topColor    = new Color(0.05f, 0.09f, 0.20f);
    public Color bottomColor = new Color(0.12f, 0.20f, 0.35f);

    [Header("Animation")]
    public float idleShiftSpeed  = 0.5f;
    public float transitionSpeed = 1.2f;

    [Header("Texture Resolution")]
    public int texWidth  = 4;  // A few pixels wide is enough
    public int texHeight = 64; // Low res is fine for a smooth gradient

    Color _currentTop, _currentBottom;
    Color _targetTop,  _targetBottom;
    float _idleTimer;

    SpriteRenderer _sr;
    Texture2D      _tex;
    Camera         _cam;

    static readonly (Color top, Color bottom)[] Palettes =
    {
        (new Color(0.05f, 0.09f, 0.20f), new Color(0.12f, 0.20f, 0.35f)), // Navy
        (new Color(0.10f, 0.04f, 0.20f), new Color(0.20f, 0.08f, 0.32f)), // Purple
        (new Color(0.04f, 0.13f, 0.15f), new Color(0.07f, 0.23f, 0.26f)), // Teal
        (new Color(0.16f, 0.05f, 0.05f), new Color(0.26f, 0.10f, 0.10f)), // Crimson
        (new Color(0.04f, 0.12f, 0.06f), new Color(0.07f, 0.22f, 0.11f)), // Forest
    };

    int _paletteIndex = 0;

    void Awake()
    {
        Instance = this;

        _cam = Camera.main;
        if (_cam == null) _cam = FindFirstObjectByType<Camera>();

        _sr = GetComponent<SpriteRenderer>();
        _sr.sortingOrder = -100; // Behind everything

        _currentTop    = _targetTop    = topColor;
        _currentBottom = _targetBottom = bottomColor;

        // Create the gradient texture
        _tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        ApplyGradientToTexture();
        FitToScreen();
    }

    void Update()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindFirstObjectByType<Camera>();
            if (_cam == null) return;
        }

        // Smoothly interpolate toward the target palette
        _currentTop    = Color.Lerp(_currentTop,    _targetTop,    transitionSpeed * Time.deltaTime);
        _currentBottom = Color.Lerp(_currentBottom, _targetBottom, transitionSpeed * Time.deltaTime);

        // Subtle idle breathing
        _idleTimer += Time.deltaTime * idleShiftSpeed;
        float breathe = Mathf.Sin(_idleTimer) * 0.012f;

        // Rebuild the gradient texture each frame (cheap at 4×64)
        for (int y = 0; y < texHeight; y++)
        {
            float t   = y / (float)(texHeight - 1);
            Color col = Color.Lerp(_currentBottom, _currentTop, t) + Color.white * breathe;
            for (int x = 0; x < texWidth; x++)
                _tex.SetPixel(x, y, col);
        }
        _tex.Apply();

        // Keep the quad fitted to the screen in case the camera changes
        FitToScreen();
    }

    /// <summary>Advances to the next color palette. Call from GameRoundManager.StartNewRound().</summary>
    public void TriggerRoundTransition()
    {
        _paletteIndex = (_paletteIndex + 1) % Palettes.Length;
        _targetTop    = Palettes[_paletteIndex].top;
        _targetBottom = Palettes[_paletteIndex].bottom;
    }

    void ApplyGradientToTexture()
    {
        for (int y = 0; y < texHeight; y++)
        {
            float t = y / (float)(texHeight - 1);
            Color col = Color.Lerp(_currentBottom, _currentTop, t);
            for (int x = 0; x < texWidth; x++)
                _tex.SetPixel(x, y, col);
        }
        _tex.Apply();

        // pixelsPerUnit = 1 so the sprite is texWidth x texHeight units — FitToScreen handles scaling
        var sprite = Sprite.Create(
            _tex,
            new Rect(0, 0, texWidth, texHeight),
            new Vector2(0.5f, 0.5f),
            1f
        );
        _sr.sprite = sprite;
    }

    void FitToScreen()
    {
        if (_cam == null) return;

        float camH = _cam.orthographicSize * 2f;
        float camW = camH * _cam.aspect;

        // Sprite is texWidth x texHeight units (pixelsPerUnit = 1), so scale accordingly
        transform.localScale = new Vector3(camW / texWidth, camH / texHeight, 1f);

        // Keep it centered on the camera
        var cp = _cam.transform.position;
        transform.position = new Vector3(cp.x, cp.y, transform.position.z);
    }
}