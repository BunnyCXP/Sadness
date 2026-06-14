using System.Collections;
using UnityEngine;

/// <summary>
/// Runs Film 2's opening camera focus, dedicated letterbox retract, and control-panel reveal.
/// It does not control Film 2 gameplay or room-entry flow.
/// </summary>
[DisallowMultipleComponent]
public class Film2IntroFocusSequence : MonoBehaviour
{
    [Header("References")]
    public Camera movieCamera;
    public Transform flowerFocusPoint;
    public RectTransform controlPanelRoot;
    public RectTransform letterboxTop;
    public RectTransform letterboxBottom;
    public CanvasGroup controlPanelCanvasGroup;

    [Header("Camera Focus")]
    public float focusedFov = 24f;
    public float normalFov = 42f;
    public float focusHoldDuration = 2f;
    public float zoomOutDuration = 3f;
    public AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Letterbox")]
    public float letterboxHeight = 160f;
    public float letterboxOutDuration = 2f;
    public AnimationCurve letterboxCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Control Panel Slide")]
    public float panelHiddenY = -420f;
    public float panelShownY = 0f;
    public float panelSlideDuration = 1.4f;
    public AnimationCurve panelSlideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Debug")]
    public bool playOnEnableForDirectTest;
    public bool sequencePlaying;

    private Coroutine introRoutine;

    private void OnEnable()
    {
        PrepareInitialState();

        if (playOnEnableForDirectTest)
            BeginIntro();
    }

    private void OnDisable()
    {
        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introRoutine = null;
        sequencePlaying = false;
    }

    public void BeginIntro()
    {
        if (sequencePlaying)
            return;

        introRoutine = StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        sequencePlaying = true;
        PrepareInitialState();

        Quaternion normalRotation = movieCamera != null
            ? movieCamera.transform.rotation
            : Quaternion.identity;
        Quaternion focusedRotation = normalRotation;

        if (movieCamera != null)
        {
            movieCamera.fieldOfView = focusedFov;

            if (flowerFocusPoint != null)
            {
                Vector3 focusDirection = flowerFocusPoint.position - movieCamera.transform.position;

                if (focusDirection.sqrMagnitude > 0.0001f)
                {
                    focusedRotation = Quaternion.LookRotation(focusDirection.normalized, Vector3.up);
                    movieCamera.transform.rotation = focusedRotation;
                }
            }
            else
            {
                Debug.LogWarning(
                    "Film2IntroFocusSequence: flowerFocusPoint is missing; keeping the current MovieCamera direction.",
                    this);
            }
        }
        else
        {
            Debug.LogWarning("Film2IntroFocusSequence: movieCamera is missing.", this);
        }

        if (focusHoldDuration > 0f)
            yield return new WaitForSeconds(focusHoldDuration);

        float zoomDuration = Mathf.Max(0f, zoomOutDuration);

        if (zoomDuration <= 0f)
        {
            ApplyCameraZoom(1f, focusedRotation, normalRotation);
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < zoomDuration)
            {
                elapsed += Time.deltaTime;
                float t = EvaluateCurve(zoomCurve, elapsed / zoomDuration);
                ApplyCameraZoom(t, focusedRotation, normalRotation);
                yield return null;
            }

            ApplyCameraZoom(1f, focusedRotation, normalRotation);
        }

        yield return RevealPanelAndRetractLetterbox();

        sequencePlaying = false;
        introRoutine = null;
        Debug.Log("Film2IntroFocusSequence: intro finished", this);
    }

    private IEnumerator RevealPanelAndRetractLetterbox()
    {
        float barDuration = Mathf.Max(0f, letterboxOutDuration);

        if (barDuration <= 0f)
        {
            SetLetterboxHeight(0f);
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < barDuration)
            {
                elapsed += Time.deltaTime;
                float t = EvaluateCurve(letterboxCurve, elapsed / barDuration);
                SetLetterboxHeight(Mathf.Lerp(letterboxHeight, 0f, t));
                yield return null;
            }

            SetLetterboxHeight(0f);
        }

        float slideDuration = Mathf.Max(0f, panelSlideDuration);

        if (slideDuration <= 0f)
        {
            SetPanelState(panelShownY, 1f);
            yield break;
        }

        float slideElapsed = 0f;

        while (slideElapsed < slideDuration)
        {
            slideElapsed += Time.deltaTime;
            float t = EvaluateCurve(panelSlideCurve, slideElapsed / slideDuration);
            SetPanelState(Mathf.Lerp(panelHiddenY, panelShownY, t), t);
            yield return null;
        }

        SetPanelState(panelShownY, 1f);
    }

    private void PrepareInitialState()
    {
        SetLetterboxHeight(letterboxHeight);
        SetPanelState(panelHiddenY, 0f);
    }

    private void ApplyCameraZoom(float t, Quaternion focusedRotation, Quaternion normalRotation)
    {
        if (movieCamera == null)
            return;

        movieCamera.fieldOfView = Mathf.Lerp(focusedFov, normalFov, t);
        movieCamera.transform.rotation = Quaternion.Slerp(focusedRotation, normalRotation, t);
    }

    private void SetPanelState(float y, float alpha)
    {
        if (controlPanelRoot != null)
        {
            Vector2 position = controlPanelRoot.anchoredPosition;
            position.y = y;
            controlPanelRoot.anchoredPosition = position;
        }

        if (controlPanelCanvasGroup != null)
            controlPanelCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void SetLetterboxHeight(float height)
    {
        SetRectHeight(letterboxTop, height);
        SetRectHeight(letterboxBottom, height);
    }

    private static void SetRectHeight(RectTransform rect, float height)
    {
        if (rect == null)
            return;

        Vector2 size = rect.sizeDelta;
        size.y = Mathf.Max(0f, height);
        rect.sizeDelta = size;
    }

    private static float EvaluateCurve(AnimationCurve curve, float t)
    {
        t = Mathf.Clamp01(t);
        return curve == null ? t : curve.Evaluate(t);
    }
}
