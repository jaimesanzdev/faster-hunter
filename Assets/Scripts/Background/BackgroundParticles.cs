using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// URP 2D compatible floating particle dots for the background.
/// Creates small GameObjects with SpriteRenderers and moves them each frame.
/// Works correctly in Unity URP 2D without any pipeline callbacks.
///
/// SETUP:
/// 1. Create an empty GameObject named "BackgroundParticles" in your scene.
/// 2. Attach this script to it.
/// 3. Place it alongside BackgroundGradient in the scene.
/// 4. Particles automatically sort behind shapes via sortingOrder.
/// </summary>
public class BackgroundParticles : MonoBehaviour
{
    [Header("Particles")]
    public int   particleCount = 50;
    public float minSize       = 0.05f;
    public float maxSize       = 0.18f;
    public float minSpeed      = 0.03f;
    public float maxSpeed      = 0.12f;
    [Range(0f, 1f)] public float particleAlpha = 0.22f;

    struct ParticleData
    {
        public Transform  tr;
        public Vector2    dir;
        public float      speed;
    }

    List<ParticleData> _particles = new();
    Sprite             _dotSprite;
    Camera             _cam;

    void Awake()
    {
        _cam = Camera.main;
        if (_cam == null) _cam = FindFirstObjectByType<Camera>();

        _dotSprite = MakeCircleSprite();
        SpawnParticles();
    }

    /// <summary>Generates a small white circle sprite procedurally.</summary>
    Sprite MakeCircleSprite()
    {
        int size = 16;
        var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
        };

        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float r  = size * 0.45f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx   = x + 0.5f - cx;
            float dy   = y + 0.5f - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(1f - (dist - (r - 1f)));
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void SpawnParticles()
    {
        Color[] palette =
        {
            new Color(0.4f, 0.7f, 1f),    // azul claro
            new Color(0.6f, 0.9f, 1f),    // cian suave
            new Color(0.8f, 0.8f, 1f),    // lavanda
            new Color(1f, 1f, 1f),         // blanco
            new Color(0.5f, 0.4f, 1f),    // violeta
        };

        float camH = _cam != null ? _cam.orthographicSize : 5f;
        float camW = _cam != null ? camH * _cam.aspect    : 8f;

        for (int i = 0; i < particleCount; i++)
        {
            var go = new GameObject($"Particle_{i}");
            go.transform.SetParent(transform);

            float size = Random.Range(minSize, maxSize);
            go.transform.position   = new Vector3(
                Random.Range(-camW, camW),
                Random.Range(-camH, camH),
                9f
            );
            go.transform.localScale = Vector3.one * size;

            var sr         = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _dotSprite;
            sr.sortingOrder = -99; // In front of gradient, behind shapes

            // Pick a random palette color with the configured alpha
            var col = palette[Random.Range(0, palette.Length)];
            col.a   = particleAlpha;
            sr.color = col;

            _particles.Add(new ParticleData
            {
                tr    = go.transform,
                dir   = Random.insideUnitCircle.normalized,
                speed = Random.Range(minSpeed, maxSpeed),
            });
        }
    }

    void Update()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) _cam = FindFirstObjectByType<Camera>();
            if (_cam == null) return;
        }

        float camH   = _cam.orthographicSize;
        float camW   = camH * _cam.aspect;
        var   camPos = _cam.transform.position;

        for (int i = 0; i < _particles.Count; i++)
        {
            var p   = _particles[i];
            var pos = p.tr.position;

            pos.x += p.dir.x * p.speed * Time.deltaTime;
            pos.y += p.dir.y * p.speed * Time.deltaTime;

            // Wrap around screen edges relative to camera position
            if (pos.x > camPos.x + camW + 0.5f) pos.x = camPos.x - camW - 0.5f;
            if (pos.x < camPos.x - camW - 0.5f) pos.x = camPos.x + camW + 0.5f;
            if (pos.y > camPos.y + camH + 0.5f) pos.y = camPos.y - camH - 0.5f;
            if (pos.y < camPos.y - camH - 0.5f) pos.y = camPos.y + camH + 0.5f;

            p.tr.position = pos;
        }
    }
}