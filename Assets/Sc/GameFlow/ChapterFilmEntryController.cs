using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Runs the final room-to-film transition after remote aiming/countdown: screen approach, letterbox,
/// white flash, camera switch, FilmRoot activation, and the onFilmEntered handoff.
/// </summary>
[DisallowMultipleComponent]
public class ChapterFilmEntryController : MonoBehaviour
{
    [Header("Chapter")]
    public ChapterRoomManager roomManager;
    public ChapterRoomLookController roomLookController;

    [Tooltip("L2 填 2，L3 填 3。填 0 表示不限制。")]
    public int requiredChapterIndex = 2;

    [Header("Scene Roots")]
    [Tooltip("房间根物体。可以为空。")]
    public GameObject roomRoot;

    [Tooltip("电影/关卡根物体。进入电影时会启用。")]
    public GameObject filmRoot;

    [Tooltip("房间模式下是否禁用 filmRoot。建议勾选。")]
    public bool disableFilmRootInRoomMode = true;

    [Tooltip("进入电影后是否禁用 roomRoot。一般不勾，避免房间消失造成引用问题。")]
    public bool disableRoomRootOnFilmEnter = false;

    [Header("Cameras")]
    public Camera roomCamera;
    public Camera movieCamera;

    [Tooltip("进入电影后是否启用 MovieCamera。")]
    public bool switchToMovieCamera = true;

    [Header("Optional Gameplay")]
    public InteractionManager interactionManager;

    [Tooltip("进入电影后是否打开电影玩法交互。第二关玩法没接好前建议先不勾。")]
    public bool enableInteractionManagerOnFilmEnter = false;

    [Header("Canvas Bars / White")]
    public RectTransform topBar;
    public RectTransform bottomBar;
    public CanvasGroup whiteFlashCanvasGroup;

    public float letterboxHeight = 200f;
    public float letterboxDuration = 0.8f;
    public float whiteFadeDuration = 0.8f;
    public float whiteHoldDuration = 0.15f;

    [Tooltip("进入电影后是否收回黑边。如果第二关开场也想保持电影黑边，可以不勾。")]
    public bool retractLetterboxAfterEnter = true;

    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("L1 Style Screen Approach")]
    public bool approachScreenBeforeWhite = true;
    public Transform screenTransform;
    public float approachDuration = 2f;
    public float finalDistanceFromScreen = 1.2f;
    public bool alignCameraToScreen = true;

    [Header("L1 Style Timing")]
    public float flashInDuration = 0.1f;
    public float flashOutDuration = 0.4f;
    public float letterboxInDuration = 2f;
    public float letterboxOutDuration = 2f;

    [Header("Room Mode UI Cleanup")]
    [Tooltip("回到房间时要隐藏的东西：倒计时、电影屏幕文字、旧提示文字等。")]
    public GameObject[] hideInRoomMode;

    [Tooltip("回到房间时要显示的东西。一般可以空着。")]
    public GameObject[] showInRoomMode;

    [Header("Entry UI")]
    [Tooltip("进入电影转场期间显示的对象：电影屏幕文字、倒计时背景等。")]
    public GameObject[] showDuringEntry;

    [Tooltip("转场结束后是否隐藏 showDuringEntry。")]
    public bool hideEntryObjectsAfterEnter = true;

    [Tooltip("进入电影后显示的对象。比如第二关 UI 根物体。")]
    public GameObject[] showAfterFilmEntered;

    [Header("Countdown Optional")]
    public bool useCountdown = true;
    public TMP_Text countdownText;

    [Tooltip("可以填 3,2,1，也可以填 Look, Closer, Now 之类。")]
    public string[] countdownSteps = new string[] { "3", "2", "1" };

    public float countdownStepDuration = 0.55f;

    [Tooltip("倒计时结束后是否隐藏 countdownText。")]
    public bool hideCountdownAfterFinished = true;

    [Header("Safety / Testing")]
    [Tooltip("如果没有 MovieCamera / FilmRoot / onFilmEntered 目标，播放完测试转场后自动回到房间，避免卡死。")]
    public bool restoreRoomControlIfNoFilmTarget = true;

    [Tooltip("FilmRoot 没有子物体时不把它当成已完成的电影目标，方便章节搭建期间安全返回房间。")]
    public bool treatEmptyFilmRootAsNoFilmTarget = true;

    [Tooltip("Start 时清理一次房间 UI。建议勾选。")]
    public bool prepareRoomModeOnStart = true;

    [Tooltip("自动监听 ChapterRoomManager.onRoomRevealed，用来清理 L2/L3 房间 UI。")]
    public bool autoBindRoomRevealed = true;

    [Tooltip("自动监听 ChapterRoomManager.onEnterFilmRequested。建议勾选，这样不用手动拖事件。")]
    public bool autoBindEnterFilmRequested = true;

    public bool verboseDebug = true;

    [Header("Events")]
    public UnityEvent onEntryStarted;
    public UnityEvent onFilmCameraActivated;
    public UnityEvent onFilmEntered;
    public UnityEvent onReturnedToRoomForDebug;

    [Header("Debug State")]
    [SerializeField] private bool isEnteringFilm;
    [SerializeField] private bool hasRegisteredEvents;

    private Coroutine enterRoutine;
    private Vector3 roomCameraPositionBeforeApproach;
    private Quaternion roomCameraRotationBeforeApproach;
    private bool hasRoomCameraPoseBeforeApproach;

    void Awake()
    {
        ResolveReferences();

        if (prepareRoomModeOnStart)
            PrepareRoomMode(false);
    }

    void Start()
    {
        ResolveReferences();
        RegisterRoomManagerEvents();

        if (prepareRoomModeOnStart)
            PrepareRoomMode(false);
    }

    void OnEnable()
    {
        ResolveReferences();
        RegisterRoomManagerEvents();
    }

    void OnDisable()
    {
        UnregisterRoomManagerEvents();
    }

    private void ResolveReferences()
    {
        if (roomManager == null)
            roomManager = GetComponent<ChapterRoomManager>();

        if (roomManager == null)
            roomManager = FindFirstObjectByType<ChapterRoomManager>();

        if (roomLookController == null)
            roomLookController = GetComponent<ChapterRoomLookController>();

        if (roomLookController == null)
            roomLookController = FindFirstObjectByType<ChapterRoomLookController>();

        if (roomManager != null)
        {
            if (roomCamera == null)
                roomCamera = roomManager.roomCamera;

            if (movieCamera == null)
                movieCamera = roomManager.movieCamera;

            if (roomRoot == null)
                roomRoot = roomManager.roomRoot;

            if (filmRoot == null)
                filmRoot = roomManager.filmRoot;

            if (interactionManager == null)
                interactionManager = roomManager.interactionManager;
        }
    }

    private void RegisterRoomManagerEvents()
    {
        if (hasRegisteredEvents)
            return;

        if (roomManager == null)
            return;

        if (autoBindEnterFilmRequested && roomManager.onEnterFilmRequested != null)
        {
            roomManager.onEnterFilmRequested.RemoveListener(EnterFilm);
            roomManager.onEnterFilmRequested.AddListener(EnterFilm);
        }

        if (autoBindRoomRevealed && roomManager.onRoomRevealed != null)
        {
            roomManager.onRoomRevealed.RemoveListener(HandleRoomRevealed);
            roomManager.onRoomRevealed.AddListener(HandleRoomRevealed);
        }

        hasRegisteredEvents = true;
    }

    private void UnregisterRoomManagerEvents()
    {
        if (!hasRegisteredEvents || roomManager == null)
            return;

        if (autoBindEnterFilmRequested && roomManager.onEnterFilmRequested != null)
            roomManager.onEnterFilmRequested.RemoveListener(EnterFilm);

        if (autoBindRoomRevealed && roomManager.onRoomRevealed != null)
            roomManager.onRoomRevealed.RemoveListener(HandleRoomRevealed);

        hasRegisteredEvents = false;
    }

    private void HandleRoomRevealed()
    {
        PrepareRoomMode(false);

        if (verboseDebug)
            Debug.Log("ChapterFilmEntryController: room revealed, entry UI cleaned.");
    }

    [ContextMenu("Prepare Room Mode")]
    public void PrepareRoomMode()
    {
        PrepareRoomMode(false);
    }

    public void PrepareRoomMode(bool enableRoomInput)
    {
        isEnteringFilm = false;

        RestoreRoomCameraPose();

        SetLetterboxImmediate(0f);
        SetWhiteImmediate(0f);

        SetObjectsActive(hideInRoomMode, false);
        SetObjectsActive(showInRoomMode, true);
        SetObjectsActive(showDuringEntry, false);
        SetObjectsActive(showAfterFilmEntered, false);

        if (countdownText != null)
        {
            countdownText.text = "";
            if (hideCountdownAfterFinished)
                countdownText.gameObject.SetActive(false);
        }

        if (filmRoot != null && disableFilmRootInRoomMode)
            filmRoot.SetActive(false);

        if (roomRoot != null)
            roomRoot.SetActive(true);

        if (roomCamera != null)
            roomCamera.enabled = true;

        if (movieCamera != null)
            movieCamera.enabled = false;

        if (interactionManager != null)
            interactionManager.canInteract = false;

        if (roomManager != null)
        {
            roomManager.allowR2ToEnterFilm = false;

            if (enableRoomInput)
                roomManager.roomInputEnabled = true;
        }

        if (enableRoomInput && roomLookController != null)
            roomLookController.EnableControl();

        if (verboseDebug)
            Debug.Log("ChapterFilmEntryController: prepared room mode.");
    }

    [ContextMenu("Debug Enable Room Control")]
    public void DebugEnableRoomControl()
    {
        PrepareRoomMode(true);
    }

    [ContextMenu("Enter Film")]
    public void EnterFilm()
    {
        if (isEnteringFilm)
            return;

        ResolveReferences();

        if (requiredChapterIndex > 0 && roomManager != null && roomManager.chapterIndex != requiredChapterIndex)
        {
            if (verboseDebug)
            {
                Debug.Log(
                    "ChapterFilmEntryController: ignored EnterFilm because chapter mismatch. Required=" +
                    requiredChapterIndex +
                    ", actual=" +
                    roomManager.chapterIndex
                );
            }

            return;
        }

        if (enterRoutine != null)
            StopCoroutine(enterRoutine);

        enterRoutine = StartCoroutine(EnterFilmRoutine());
    }

    private IEnumerator EnterFilmRoutine()
    {
        isEnteringFilm = true;

        if (verboseDebug)
            Debug.Log("ChapterFilmEntryController: entry started.");

        if (roomManager != null)
        {
            roomManager.roomInputEnabled = false;
            roomManager.allowR2ToEnterFilm = false;
        }

        if (roomLookController != null)
            roomLookController.DisableControl();

        if (interactionManager != null)
            interactionManager.canInteract = false;

        onEntryStarted?.Invoke();

        SetObjectsActive(showDuringEntry, true);
        SetObjectsActive(hideInRoomMode, false);

        if (countdownText != null)
            countdownText.gameObject.SetActive(useCountdown);

        if (approachScreenBeforeWhite && roomCamera != null && screenTransform != null)
            yield return ApproachScreenAndLetterboxRoutine();
        else
            yield return AnimateLetterbox(GetCurrentLetterboxHeight(), letterboxHeight, letterboxInDuration);

        if (useCountdown)
            yield return PlayCountdownRoutine();

        yield return FadeWhite(GetWhiteAlpha(), 1f, flashInDuration);

        if (whiteHoldDuration > 0f)
            yield return new WaitForSeconds(whiteHoldDuration);

        bool hasFilmTarget = HasFilmTarget();

        if (hasFilmTarget)
        {
            ActivateFilmView();
            onFilmCameraActivated?.Invoke();

            yield return null;

            yield return FadeWhite(1f, 0f, flashOutDuration);

            if (retractLetterboxAfterEnter)
                yield return AnimateLetterbox(letterboxHeight, 0f, letterboxOutDuration);

            FinalizeFilmEntry();
        }
        else
        {
            Debug.LogWarning(
                "ChapterFilmEntryController: no FilmRoot, MovieCamera, or onFilmEntered target. Returning to room for setup safety."
            );

            yield return FadeWhite(1f, 0f, flashOutDuration);
            yield return AnimateLetterbox(letterboxHeight, 0f, letterboxOutDuration);

            if (restoreRoomControlIfNoFilmTarget)
            {
                PrepareRoomMode(true);
                onReturnedToRoomForDebug?.Invoke();
            }
        }

        isEnteringFilm = false;
        enterRoutine = null;
    }

    private IEnumerator ApproachScreenAndLetterboxRoutine()
    {
        Transform cameraTransform = roomCamera.transform;
        roomCameraPositionBeforeApproach = cameraTransform.position;
        roomCameraRotationBeforeApproach = cameraTransform.rotation;
        hasRoomCameraPoseBeforeApproach = true;

        Vector3 screenToCamera = roomCameraPositionBeforeApproach - screenTransform.position;
        Vector3 screenNormal = screenTransform.forward;

        if (Vector3.Dot(screenNormal, screenToCamera) < 0f)
            screenNormal = -screenNormal;

        Vector3 targetPosition =
            screenTransform.position +
            screenNormal.normalized * Mathf.Max(0.01f, finalDistanceFromScreen);
        Vector3 lookDirection = screenTransform.position - targetPosition;
        Quaternion targetRotation = lookDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
            : roomCameraRotationBeforeApproach;
        float fromBarHeight = GetCurrentLetterboxHeight();
        float duration = Mathf.Max(0f, approachDuration);

        if (duration <= 0f)
        {
            cameraTransform.position = targetPosition;

            if (alignCameraToScreen)
                cameraTransform.rotation = targetRotation;

            SetLetterboxImmediate(letterboxHeight);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curved = EvaluateCurve(t);

            cameraTransform.position = Vector3.Lerp(roomCameraPositionBeforeApproach, targetPosition, curved);

            if (alignCameraToScreen)
                cameraTransform.rotation = Quaternion.Slerp(roomCameraRotationBeforeApproach, targetRotation, curved);

            float barProgress = letterboxInDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / letterboxInDuration);
            SetLetterboxImmediate(Mathf.Lerp(fromBarHeight, letterboxHeight, EvaluateCurve(barProgress)));

            yield return null;
        }

        cameraTransform.position = targetPosition;

        if (alignCameraToScreen)
            cameraTransform.rotation = targetRotation;

        SetLetterboxImmediate(letterboxHeight);
    }

    private void RestoreRoomCameraPose()
    {
        if (!hasRoomCameraPoseBeforeApproach || roomCamera == null)
            return;

        roomCamera.transform.SetPositionAndRotation(
            roomCameraPositionBeforeApproach,
            roomCameraRotationBeforeApproach
        );
        hasRoomCameraPoseBeforeApproach = false;
    }

    private void ActivateFilmView()
    {
        if (filmRoot != null)
            filmRoot.SetActive(true);

        if (disableRoomRootOnFilmEnter && roomRoot != null)
            roomRoot.SetActive(false);

        if (switchToMovieCamera)
        {
            if (roomCamera != null)
                roomCamera.enabled = false;

            if (movieCamera != null)
            {
                movieCamera.gameObject.SetActive(true);
                movieCamera.enabled = true;
            }
        }

        if (interactionManager != null)
            interactionManager.canInteract = enableInteractionManagerOnFilmEnter;

        if (verboseDebug)
            Debug.Log("ChapterFilmEntryController: film view activated.");
    }

    private void FinalizeFilmEntry()
    {
        SetWhiteImmediate(0f);

        if (retractLetterboxAfterEnter)
            SetLetterboxImmediate(0f);
        else
            SetLetterboxImmediate(letterboxHeight);

        if (hideEntryObjectsAfterEnter)
            SetObjectsActive(showDuringEntry, false);

        if (countdownText != null && hideCountdownAfterFinished)
        {
            countdownText.text = "";
            countdownText.gameObject.SetActive(false);
        }

        SetObjectsActive(showAfterFilmEntered, true);

        onFilmEntered?.Invoke();

        if (verboseDebug)
            Debug.Log("ChapterFilmEntryController: film entered.");
    }

    private IEnumerator PlayCountdownRoutine()
    {
        if (countdownText == null || countdownSteps == null || countdownSteps.Length == 0)
            yield break;

        countdownText.gameObject.SetActive(true);

        for (int i = 0; i < countdownSteps.Length; i++)
        {
            countdownText.text = countdownSteps[i];

            if (countdownStepDuration > 0f)
                yield return new WaitForSeconds(countdownStepDuration);
            else
                yield return null;
        }

        if (hideCountdownAfterFinished)
        {
            countdownText.text = "";
            countdownText.gameObject.SetActive(false);
        }
    }

    private IEnumerator AnimateLetterbox(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetLetterboxImmediate(to);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curved = EvaluateCurve(t);

            SetLetterboxImmediate(Mathf.Lerp(from, to, curved));

            yield return null;
        }

        SetLetterboxImmediate(to);
    }

    private IEnumerator FadeWhite(float from, float to, float duration)
    {
        if (whiteFlashCanvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            SetWhiteImmediate(to);
            yield break;
        }

        if (whiteFlashCanvasGroup.gameObject != null)
            whiteFlashCanvasGroup.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curved = EvaluateCurve(t);

            SetWhiteImmediate(Mathf.Lerp(from, to, curved));

            yield return null;
        }

        SetWhiteImmediate(to);
    }

    public void SetLetterboxImmediate(float height)
    {
        height = Mathf.Max(0f, height);

        if (topBar != null)
        {
            if (!topBar.gameObject.activeSelf)
                topBar.gameObject.SetActive(true);

            Vector2 size = topBar.sizeDelta;
            size.y = height;
            topBar.sizeDelta = size;
        }

        if (bottomBar != null)
        {
            if (!bottomBar.gameObject.activeSelf)
                bottomBar.gameObject.SetActive(true);

            Vector2 size = bottomBar.sizeDelta;
            size.y = height;
            bottomBar.sizeDelta = size;
        }
    }

    public void SetWhiteImmediate(float alpha)
    {
        if (whiteFlashCanvasGroup == null)
            return;

        alpha = Mathf.Clamp01(alpha);

        if (!whiteFlashCanvasGroup.gameObject.activeSelf)
            whiteFlashCanvasGroup.gameObject.SetActive(true);

        whiteFlashCanvasGroup.alpha = alpha;
        whiteFlashCanvasGroup.blocksRaycasts = alpha > 0.01f;
        whiteFlashCanvasGroup.interactable = false;
    }

    private float GetCurrentLetterboxHeight()
    {
        if (topBar != null)
            return Mathf.Abs(topBar.sizeDelta.y);

        if (bottomBar != null)
            return Mathf.Abs(bottomBar.sizeDelta.y);

        return 0f;
    }

    private float GetWhiteAlpha()
    {
        if (whiteFlashCanvasGroup == null)
            return 0f;

        return whiteFlashCanvasGroup.alpha;
    }

    private bool HasFilmTarget()
    {
        bool hasUsableFilmRoot =
            filmRoot != null &&
            (!treatEmptyFilmRootAsNoFilmTarget || filmRoot.transform.childCount > 0);

        if (hasUsableFilmRoot)
            return true;

        if (HasAnyObject(showAfterFilmEntered))
            return true;

        if (onFilmEntered != null && onFilmEntered.GetPersistentEventCount() > 0)
            return true;

        // A standalone MovieCamera can be a valid target. When an explicitly assigned
        // FilmRoot is empty, the empty root wins so setup scenes return to the room.
        if (movieCamera != null && filmRoot == null)
            return true;

        return false;
    }

    private bool HasAnyObject(GameObject[] objects)
    {
        if (objects == null)
            return false;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                return true;
        }

        return false;
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        ResolveReferences();

        if (roomManager == null || roomLookController == null)
            Debug.LogWarning("ChapterFilmEntryController: room-stage references are incomplete.", this);
        if (roomCamera == null || movieCamera == null)
            Debug.LogWarning("ChapterFilmEntryController: roomCamera or movieCamera is missing.", this);
        if (topBar == null || bottomBar == null || whiteFlashCanvasGroup == null)
            Debug.LogWarning("ChapterFilmEntryController: letterbox/white-flash references are incomplete.", this);
        if (approachScreenBeforeWhite && screenTransform == null)
            Debug.LogWarning("ChapterFilmEntryController: screen approach is enabled but screenTransform is missing.", this);
    }

    [ContextMenu("Auto Wire L2 References")]
    public void AutoWireL2References()
    {
        ResolveReferences();
        ValidateSetup();
    }

    [ContextMenu("Debug Enter Film")]
    public void DebugEnterFilm()
    {
        EnterFilm();
    }

    private void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(active);
        }
    }

    private float EvaluateCurve(float t)
    {
        if (transitionCurve == null || transitionCurve.length == 0)
            return t;

        return transitionCurve.Evaluate(t);
    }
}

