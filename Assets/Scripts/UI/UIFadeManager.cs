using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages fade in/out transitions for UI elements (Text, TMP_Text, Button, Image, CanvasGroup).
/// Elements assigned to the fadeInTargets list start hidden (alpha = 0) in Awake.
/// Call PlayTransition() to fade out one group while fading in another simultaneously.
///
/// SETUP:
/// 1. Attach this script to any GameObject in your UI scene (e.g. the Canvas).
/// 2. Assign UI elements to fadeInTargets and fadeOutTargets in the Inspector.
/// 3. Call UIFadeManager.Instance.PlayTransition() from any button or script.
///
/// SUPPORTED COMPONENTS (auto-detected per element):
///   - CanvasGroup        (recommended — handles entire panels at once)
///   - TMP_Text
///   - Text
///   - Image
///   - RawImage
///   - Button (fades its Image component)
/// </summary>
public class UIFadeManager : MonoBehaviour
{
    public static UIFadeManager Instance { get; private set; }

    [Header("Fade In")]
    /// <summary>Elements that start hidden and fade in when PlayTransition() is called.</summary>
    public List<GameObject> fadeInTargets = new();

    [Header("Fade Out")]
    /// <summary>Elements that are visible and fade out when PlayTransition() is called.</summary>
    public List<GameObject> fadeOutTargets = new();

    [Header("Settings")]
    /// <summary>Duration in seconds of each fade.</summary>
    public float fadeDuration = 0.4f;

    /// <summary>If true, fade out and fade in happen simultaneously. If false, fade out finishes first.</summary>
    public bool parallel = true;

    /// <summary>If true, fadeOut elements are disabled (SetActive false) after fading out.</summary>
    public bool disableAfterFadeOut = true;

    Coroutine _currentTransition;

    void Awake()
    {
        Instance = this;

        // All fade-in targets start invisible and disabled
        foreach (var go in fadeInTargets)
        {
            if (go == null) continue;
            SetAlpha(go, 0f);
            go.SetActive(false);
        }
    }

    /// <summary>
    /// Fades out the fadeOutTargets and fades in the fadeInTargets.
    /// If a transition is already running, it is stopped before starting the new one.
    /// </summary>
    public void PlayTransition()
    {
        if (_currentTransition != null)
            StopCoroutine(_currentTransition);

        _currentTransition = StartCoroutine(TransitionRoutine(fadeOutTargets, fadeInTargets));
    }

    /// <summary>
    /// Reverses the transition: fades out the fadeInTargets and fades in the fadeOutTargets.
    /// Useful for "Back" buttons that need to undo the previous transition.
    /// </summary>
    public void PlayReverseTransition()
    {
        if (_currentTransition != null)
            StopCoroutine(_currentTransition);

        _currentTransition = StartCoroutine(TransitionRoutine(fadeInTargets, fadeOutTargets));
    }

    /// <summary>
    /// Custom transition: fade out a specific set of elements and fade in another.
    /// Useful if you have multiple transitions in the same scene.
    /// </summary>
    public void PlayTransition(List<GameObject> fadeOut, List<GameObject> fadeIn)
    {
        if (_currentTransition != null)
            StopCoroutine(_currentTransition);

        _currentTransition = StartCoroutine(TransitionRoutine(fadeOut, fadeIn));
    }

    IEnumerator TransitionRoutine(List<GameObject> fadeOut, List<GameObject> fadeIn)
    {
        // Activate fade-in targets at alpha 0 before starting
        foreach (var go in fadeIn)
        {
            if (go == null) continue;
            SetAlpha(go, 0f);
            go.SetActive(true);
        }

        if (parallel)
        {
            // Run both fades at the same time
            yield return StartCoroutine(RunFades(fadeOut, fadeIn));
        }
        else
        {
            // Fade out first, then fade in
            yield return StartCoroutine(FadeGroup(fadeOut, 1f, 0f));
            yield return StartCoroutine(FadeGroup(fadeIn,  0f, 1f));
        }

        // Disable faded-out elements after transition
        if (disableAfterFadeOut)
            foreach (var go in fadeOut)
                if (go != null) go.SetActive(false);
    }

    IEnumerator RunFades(List<GameObject> fadeOut, List<GameObject> fadeIn)
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            foreach (var go in fadeOut)
                if (go != null) SetAlpha(go, 1f - t);

            foreach (var go in fadeIn)
                if (go != null) SetAlpha(go, t);

            yield return null;
        }

        // Ensure final values are exact
        foreach (var go in fadeOut) if (go != null) SetAlpha(go, 0f);
        foreach (var go in fadeIn)  if (go != null) SetAlpha(go, 1f);
    }

    IEnumerator FadeGroup(List<GameObject> targets, float from, float to)
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            foreach (var go in targets)
                if (go != null) SetAlpha(go, Mathf.Lerp(from, to, t));

            yield return null;
        }

        foreach (var go in targets) if (go != null) SetAlpha(go, to);
    }

    /// <summary>
    /// Sets the alpha of a UI element. Supports CanvasGroup, TMP_Text, Text, Image, and RawImage.
    /// CanvasGroup is checked first — if present, it controls the entire panel at once.
    /// </summary>
    void SetAlpha(GameObject go, float alpha)
    {
        // CanvasGroup overrides everything — use it for panels
        var cg = go.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = alpha;
            return;
        }

        // TMP_Text
        var tmp = go.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            var c = tmp.color; c.a = alpha; tmp.color = c;
        }

        // Legacy Text
        var txt = go.GetComponent<Text>();
        if (txt != null)
        {
            var c = txt.color; c.a = alpha; txt.color = c;
        }

        // Image (also covers Button backgrounds)
        var img = go.GetComponent<Image>();
        if (img != null)
        {
            var c = img.color; c.a = alpha; img.color = c;
        }

        // RawImage
        var raw = go.GetComponent<RawImage>();
        if (raw != null)
        {
            var c = raw.color; c.a = alpha; raw.color = c;
        }
    }
}