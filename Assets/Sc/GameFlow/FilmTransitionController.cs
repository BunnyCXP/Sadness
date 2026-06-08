using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FilmTransitionController : MonoBehaviour
{
    public enum SceneTransitionMode
    {
        FullExitAndEnter,
        FromExistingWhiteCover
    }

    [Header("Overlay")]
    public RectTransform topBar;
    public RectTransform bottomBar;
    public CanvasGroup whiteFlashCanvasGroup;

    [Header("Timing")]
    public float letterboxHeight = 200f;
    public float letterboxDuration = 0.8f;
    public float whiteFadeDuration = 0.8f;
    public float whiteHoldDuration = 0.2f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Existing White Cover")]
    public float existingWhiteCoverLetterboxHeight = 200f;
    public bool forceCleanupAtEnd = true;
    public bool revealWhiteBeforeLetterbox = true;

    [Header("Setup")]
    public bool autoRegisterToGameFlow = true;
    public bool createOverlayIfMissing = true;
    public bool verboseDebug = true;

    private bool isTransitioning;
    private GameObject runtimeOverlayRoot;

    private void Awake()
    {
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.transitionController != null &&
            GameFlowManager.Instance.transitionController != this)
        {
            if (verboseDebug)
            {
                Debug.Log(
                    "FilmTransitionController: disabling duplicate controller from " +
                    gameObject.scene.name);
            }

            enabled = false;
            return;
        }

        DontDestroyOnLoad(gameObject);

        if (createOverlayIfMissing)
            EnsureOverlay();

        SetWhiteImmediate(0f);
        SetLetterboxImmediate(0f);

        if (verboseDebug)
            Debug.Log("FilmTransitionController: persistent transition controller ready.");
    }

    private void Start()
    {
        if (autoRegisterToGameFlow)
            RegisterToGameFlow();
    }

    public void RegisterToGameFlow()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.RegisterTransitionController(this);
    }

    public Coroutine PlayExitToScene(string sceneName)
    {
        return PlayExitToScene(sceneName, SceneTransitionMode.FullExitAndEnter);
    }

    public Coroutine PlayExitToScene(string sceneName, SceneTransitionMode mode)
    {
        float inheritedHeight = mode == SceneTransitionMode.FromExistingWhiteCover
            ? existingWhiteCoverLetterboxHeight
            : letterboxHeight;

        return StartTransition(sceneName, mode, inheritedHeight);
    }

    public Coroutine PlayExitToSceneFromExistingWhiteCover(
        string sceneName,
        float inheritedLetterboxHeight)
    {
        return StartTransition(
            sceneName,
            SceneTransitionMode.FromExistingWhiteCover,
            inheritedLetterboxHeight);
    }

    private Coroutine StartTransition(
        string sceneName,
        SceneTransitionMode mode,
        float inheritedLetterboxHeight)
    {
        if (isTransitioning)
        {
            if (verboseDebug)
                Debug.Log("FilmTransition: transition already running.");

            return null;
        }

        return StartCoroutine(
            PlayExitToSceneRoutineInternal(sceneName, mode, inheritedLetterboxHeight));
    }

    public IEnumerator PlayExitToSceneRoutine(string sceneName)
    {
        yield return PlayExitToSceneRoutine(sceneName, SceneTransitionMode.FullExitAndEnter);
    }

    public IEnumerator PlayExitToSceneRoutine(string sceneName, SceneTransitionMode mode)
    {
        float inheritedHeight = mode == SceneTransitionMode.FromExistingWhiteCover
            ? existingWhiteCoverLetterboxHeight
            : letterboxHeight;

        yield return PlayExitToSceneRoutineInternal(sceneName, mode, inheritedHeight);
    }

    public IEnumerator PlayExitToSceneFromExistingWhiteCoverRoutine(
        string sceneName,
        float inheritedLetterboxHeight)
    {
        yield return PlayExitToSceneRoutineInternal(
            sceneName,
            SceneTransitionMode.FromExistingWhiteCover,
            inheritedLetterboxHeight);
    }

    private IEnumerator PlayExitToSceneRoutineInternal(
        string sceneName,
        SceneTransitionMode mode,
        float inheritedLetterboxHeight)
    {
        if (isTransitioning)
            yield break;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("FilmTransition: scene name is empty.");
            yield break;
        }

        isTransitioning = true;
        EnsureOverlay();

        try
        {
            if (verboseDebug)
                Debug.Log("FilmTransition: exiting to " + sceneName + " using " + mode);

            if (mode == SceneTransitionMode.FromExistingWhiteCover)
            {
                yield return PlayFromExistingWhiteCoverRoutine(
                    sceneName,
                    inheritedLetterboxHeight);
            }
            else
            {
                yield return AnimateLetterbox(0f, letterboxHeight, letterboxDuration);
                yield return AnimateWhite(0f, 1f, whiteFadeDuration);

                if (whiteHoldDuration > 0f)
                    yield return new WaitForSecondsRealtime(whiteHoldDuration);

                yield return LoadSceneAndRevealRoom(sceneName, letterboxHeight);
            }

            if (verboseDebug)
                Debug.Log("FilmTransition: entered " + sceneName);
        }
        finally
        {
            if (forceCleanupAtEnd)
                ForceClearOverlay();

            isTransitioning = false;

            if (verboseDebug)
                Debug.Log("FilmTransitionController: transition cleanup finished");
        }
    }

    public IEnumerator PlayFromExistingWhiteCoverRoutine(string sceneName)
    {
        yield return PlayFromExistingWhiteCoverRoutine(
            sceneName,
            existingWhiteCoverLetterboxHeight);
    }

    private IEnumerator PlayFromExistingWhiteCoverRoutine(
        string sceneName,
        float inheritedLetterboxHeight)
    {
        EnsureOverlay();
        float safeHeight = GetSafeInheritedLetterboxHeight(inheritedLetterboxHeight);

        SetWhiteImmediate(1f);
        SetLetterboxImmediate(safeHeight);

        if (verboseDebug)
        {
            Debug.Log(
                "FilmTransition: taking over existing white cover for " +
                sceneName +
                " at letterbox height " +
                safeHeight);
        }

        yield return LoadSceneAndRevealRoom(sceneName, safeHeight);
    }

    public IEnumerator PlayEnterFromWhite()
    {
        yield return RevealRoomFromWhiteAndLetterbox(letterboxHeight);
    }

    public IEnumerator RevealRoomFromWhiteAndLetterbox(float inheritedLetterboxHeight)
    {
        EnsureOverlay();
        float safeHeight = GetSafeInheritedLetterboxHeight(inheritedLetterboxHeight);

        SetLetterboxImmediate(safeHeight);
        SetWhiteImmediate(1f);

        if (revealWhiteBeforeLetterbox)
        {
            yield return AnimateWhite(1f, 0f, whiteFadeDuration);
            yield return AnimateLetterbox(safeHeight, 0f, letterboxDuration);
        }
        else
        {
            yield return AnimateLetterbox(safeHeight, 0f, letterboxDuration);
            yield return AnimateWhite(1f, 0f, whiteFadeDuration);
        }

        ForceClearOverlay();
    }

    public void SetWhiteImmediate(float alpha)
    {
        if (whiteFlashCanvasGroup != null)
            whiteFlashCanvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void SetLetterboxImmediate(float height)
    {
        SetBarHeight(topBar, height);
        SetBarHeight(bottomBar, height);
    }

    public void ForceClearOverlay()
    {
        SetWhiteImmediate(0f);
        SetLetterboxImmediate(0f);
    }

    private void RegisterNewSceneServices()
    {
        RegisterToGameFlow();

        ChapterRoomManager roomManager =
            FindFirstObjectByType<ChapterRoomManager>(FindObjectsInactive.Include);

        if (roomManager != null)
            roomManager.ApplyChapterStateFromGameFlow();
    }

    private IEnumerator LoadSceneAndRevealRoom(string sceneName, float revealLetterboxHeight)
    {
        if (verboseDebug)
            Debug.Log("FilmTransitionController: before LoadScene " + sceneName);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);

        if (loadOperation == null)
        {
            Debug.LogWarning("FilmTransition: failed to start loading scene " + sceneName);
            yield break;
        }

        while (!loadOperation.isDone)
            yield return null;

        if (verboseDebug)
            Debug.Log("FilmTransitionController: after LoadScene " + sceneName);

        SetWhiteImmediate(1f);
        SetLetterboxImmediate(revealLetterboxHeight);

        yield return null;
        yield return null;

        ChapterRoomManager roomManager =
            FindFirstObjectByType<ChapterRoomManager>(FindObjectsInactive.Include);

        RegisterToGameFlow();

        if (roomManager != null)
        {
            if (verboseDebug)
                Debug.Log("FilmTransitionController: applying chapter state");

            roomManager.ApplyChapterStateFromGameFlow();
        }
        else
        {
            Debug.LogWarning("FilmTransitionController: ChapterRoomManager was not found after loading " + sceneName);
        }

        if (verboseDebug)
            Debug.Log("FilmTransitionController: revealing room");

        yield return RevealRoomFromWhiteAndLetterbox(revealLetterboxHeight);

        if (roomManager != null)
        {
            if (verboseDebug)
                Debug.Log("FilmTransitionController: calling OnRoomRevealComplete");

            roomManager.OnRoomRevealComplete();
        }
    }

    private float GetSafeInheritedLetterboxHeight(float inheritedLetterboxHeight)
    {
        if (inheritedLetterboxHeight > 0f)
            return inheritedLetterboxHeight;

        if (existingWhiteCoverLetterboxHeight > 0f)
            return existingWhiteCoverLetterboxHeight;

        return Mathf.Max(0f, letterboxHeight);
    }

    private IEnumerator AnimateWhite(float from, float to, float duration)
    {
        if (whiteFlashCanvasGroup == null)
            yield break;

        float safeDuration = Mathf.Max(0.0001f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EvaluateTransitionCurve(elapsed / safeDuration);
            whiteFlashCanvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }

        whiteFlashCanvasGroup.alpha = to;
    }

    private IEnumerator AnimateLetterbox(float from, float to, float duration)
    {
        float safeDuration = Mathf.Max(0.0001f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EvaluateTransitionCurve(elapsed / safeDuration);
            SetLetterboxImmediate(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetLetterboxImmediate(to);
    }

    private float EvaluateTransitionCurve(float t)
    {
        t = Mathf.Clamp01(t);

        if (transitionCurve == null || transitionCurve.length == 0)
            return Mathf.SmoothStep(0f, 1f, t);

        return Mathf.Clamp01(transitionCurve.Evaluate(t));
    }

    private void EnsureOverlay()
    {
        if (topBar != null && bottomBar != null && whiteFlashCanvasGroup != null)
        {
            EnsureOverlayRenderOrder();
            return;
        }

        if (!createOverlayIfMissing)
            return;

        if (runtimeOverlayRoot == null)
            CreateRuntimeOverlay();

        EnsureOverlayRenderOrder();
    }

    private void CreateRuntimeOverlay()
    {
        runtimeOverlayRoot = new GameObject(
            "TransitionCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        runtimeOverlayRoot.transform.SetParent(transform, false);

        Canvas canvas = runtimeOverlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10000;

        CanvasScaler scaler = runtimeOverlayRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        whiteFlashCanvasGroup = CreateWhiteFlash(runtimeOverlayRoot.transform);
        topBar = CreateBar("TopBar", runtimeOverlayRoot.transform, true);
        bottomBar = CreateBar("BottomBar", runtimeOverlayRoot.transform, false);

        topBar.SetAsLastSibling();
        bottomBar.SetAsLastSibling();
    }

    private void EnsureOverlayRenderOrder()
    {
        if (topBar == null || bottomBar == null || whiteFlashCanvasGroup == null)
            return;

        Transform parent = whiteFlashCanvasGroup.transform.parent;

        if (topBar.parent != parent || bottomBar.parent != parent)
            return;

        whiteFlashCanvasGroup.transform.SetAsFirstSibling();
        topBar.SetAsLastSibling();
        bottomBar.SetAsLastSibling();
    }

    private RectTransform CreateBar(string objectName, Transform parent, bool top)
    {
        GameObject barObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        barObject.transform.SetParent(parent, false);

        RectTransform rect = barObject.GetComponent<RectTransform>();
        rect.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
        rect.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
        rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        Image image = barObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        return rect;
    }

    private CanvasGroup CreateWhiteFlash(Transform parent)
    {
        GameObject flashObject = new GameObject(
            "WhiteFlash",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup)
        );

        flashObject.transform.SetParent(parent, false);

        RectTransform rect = flashObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = flashObject.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;

        CanvasGroup group = flashObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        return group;
    }

    private void SetBarHeight(RectTransform bar, float height)
    {
        if (bar == null)
            return;

        Vector2 size = bar.sizeDelta;
        size.y = Mathf.Max(0f, height);
        bar.sizeDelta = size;
    }
}
