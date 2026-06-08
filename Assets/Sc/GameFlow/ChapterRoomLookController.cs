using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Controls room free-look, HandCursor movement, remote hover, and the remote pickup request.
/// Held-remote animation, countdown, and movie-camera switching belong to later entry stages.
/// </summary>
[DisallowMultipleComponent]
public class ChapterRoomLookController : MonoBehaviour
{
    public enum StickSource
    {
        LeftStick,
        RightStick
    }

    [Header("References")]
    public ChapterRoomManager roomManager;

    [Tooltip("房间相机。L2/L3 的 RoomCamera。")]
    public Camera roomCamera;

    [Tooltip("真正旋转的对象。不拖则默认旋转 RoomCamera.transform。")]
    public Transform lookRoot;

    [Header("Enable Rules")]
    [Tooltip("开场先禁用，等 L1 -> L2 转场结束后再允许控制。建议勾选。")]
    public bool startDisabled = true;

    [Tooltip("自动等待 ChapterRoomManager.roomInputEnabled = true 后启用。")]
    public bool autoEnableWhenRoomReady = true;

    [Tooltip("控制时必须要求 ChapterRoomManager.roomInputEnabled = true。")]
    public bool requireRoomManagerPermission = true;

    [Tooltip("只允许当前 ChapterRoomManager.chapterIndex 匹配时运行。L2 填 2，L3 填 3。填 0 表示不限制。")]
    public int requiredChapterIndex = 2;

    [Tooltip("禁用 ChapterRoomManager 自己的直接 R2 进入电影，避免玩家不指向遥控器也能进电影。")]
    public bool disableChapterRoomManagerDirectR2 = true;

    [Header("Camera Look")]
    public float yawSpeed = 75f;
    public float pitchSpeed = 55f;

    [Tooltip("相对初始角度，向左最多转多少度。")]
    public float minYaw = -35f;

    [Tooltip("相对初始角度，向右最多转多少度。")]
    public float maxYaw = 35f;

    [Tooltip("相对初始角度，向下最多看多少度。")]
    public float minPitch = -18f;

    [Tooltip("相对初始角度，向上最多看多少度。")]
    public float maxPitch = 18f;

    public bool invertY = false;
    public float stickDeadZone = 0.12f;
    public bool resetLookOnEnable = false;

    [Header("Stick Mapping")]
    [Tooltip("Stick used to rotate the room camera.")]
    public StickSource lookStickSource = StickSource.LeftStick;

    [Tooltip("Stick used to move the HandCursor.")]
    public StickSource cursorStickSource = StickSource.RightStick;

    [Header("Hand Cursor")]
    [Tooltip("手形光标 UI。建议放在 Screen Space Overlay Canvas 下。")]
    public RectTransform handCursor;

    [Tooltip("手形光标所在 Canvas。可不拖，脚本会自动从父级查找。")]
    public Canvas cursorCanvas;

    public Image cursorImage;

    public float cursorSpeed = 850f;
    public Vector2 cursorPadding = new Vector2(60f, 60f);
    public bool resetCursorOnEnable = true;

    public Color cursorNormalColor = Color.white;
    public Color cursorHoverColor = new Color(1f, 0.92f, 0.55f, 1f);
    public float cursorHoverScale = 1.15f;

    [Header("Cursor Spawn Animation")]
    public bool animateCursorOnEnable = true;
    public CanvasGroup cursorCanvasGroup;
    public float cursorPopDuration = 0.24f;
    public float cursorPopStartScale = 0.65f;
    public float cursorPopOvershootScale = 1.12f;
    public float cursorPopEndScale = 1f;
    public AnimationCurve cursorPopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Remote Interaction")]
    [Tooltip("遥控器根物体。光标射线打到它或它的子物体时，可按 R2 互动。")]
    public Transform remoteRoot;

    [Tooltip("遥控器 Collider。可选；拖了会优先判断这个 Collider。")]
    public Collider remoteCollider;

    [Tooltip("如果不用指定对象，也可以用 Tag。留空则不用 Tag。")]
    public string remoteTag = "";

    public LayerMask interactMask = ~0;
    public float raycastDistance = 30f;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Tooltip("互动成功后隐藏遥控器。通常不需要，先不勾。")]
    public bool hideRemoteAfterUse = false;

    [Tooltip("Optional emission/outline feedback displayed while the cursor is over the remote.")]
    public ObjectHoverFeedback remoteHoverFeedback;

    [Header("Enter Film")]
    [Tooltip("遥控器互动成功后，自动调用 ChapterRoomManager.EnterFilm()。")]
    public bool autoCallRoomManagerEnterFilm = true;

    [Tooltip("遥控器确认后直接进入电影。默认关闭，让遥控器抬起/对准屏幕流程先接管。")]
    public bool callRoomManagerEnterFilmOnRemoteConfirmed = false;

    public UnityEvent onRemoteConfirmed;
    public UnityEvent onRemotePickedUp;

    [Header("Debug Input")]
    public bool enableKeyboardMouseDebug = true;

    [Tooltip("按住鼠标右键时，用鼠标移动视角。")]
    public bool mouseRightButtonLook = true;

    public float mouseLookMultiplier = 12f;

    [Tooltip("调试时用鼠标位置直接移动光标。")]
    public bool mouseControlsCursor = false;

    [Tooltip("调试互动键。")]
    public KeyCode debugInteractKey = KeyCode.E;

    [Tooltip("调试时允许鼠标左键确认互动。L2 正式手柄流程建议关闭。")]
    public bool mouseLeftButtonInteract = true;

    [Header("Debug State")]
    [SerializeField] private bool controlEnabled;
    [SerializeField] private bool isHoveringRemote;
    [SerializeField] private bool hasConfirmedRemote;

    private Quaternion baseLocalRotation;
    private float currentYaw;
    private float currentPitch;

    private Vector2 cursorStartAnchoredPosition;
    private Vector3 cursorStartScale = Vector3.one;
    private Coroutine cursorPopRoutine;

    void Awake()
    {
        ResolveReferences();
        CaptureBaseLookRotation();
        CaptureCursorState();

        if (disableChapterRoomManagerDirectR2 && roomManager != null)
            roomManager.allowR2ToEnterFilm = false;

        if (startDisabled)
            DisableControlImmediate();
        else
            EnableControl();
    }

    void Start()
    {
        ResolveReferences();

        if (startDisabled)
            DisableControlImmediate();
    }

    void Update()
    {
        ResolveReferencesLazy();

        if (!controlEnabled && autoEnableWhenRoomReady && CanUseRoomControl() && !hasConfirmedRemote)
            EnableControl();

        if (!controlEnabled)
            return;

        if (requireRoomManagerPermission && !CanUseRoomControl())
        {
            DisableControlImmediate();
            return;
        }

        UpdateCameraLook();
        UpdateHandCursor();
        UpdateRemoteHover();

        if (ReadInteractPressedThisFrame())
            TryConfirmRemote();
    }

    public void EnableControl()
    {
        if (hasConfirmedRemote)
            return;

        ResolveReferences();

        if (requiredChapterIndex > 0 && roomManager != null && roomManager.chapterIndex != requiredChapterIndex)
            return;

        if (disableChapterRoomManagerDirectR2 && roomManager != null)
            roomManager.allowR2ToEnterFilm = false;

        if (roomCamera != null)
            roomCamera.enabled = true;

        if (lookRoot == null && roomCamera != null)
            lookRoot = roomCamera.transform;

        if (resetLookOnEnable)
        {
            currentYaw = 0f;
            currentPitch = 0f;
            CaptureBaseLookRotation();
            ApplyLookRotation();
        }

        if (resetCursorOnEnable && handCursor != null)
            handCursor.anchoredPosition = cursorStartAnchoredPosition;

        controlEnabled = true;
        ShowCursor();
        UpdateRemoteHover();

        Debug.Log("ChapterRoomLookController: control enabled.");
    }

    public void DisableControl()
    {
        controlEnabled = false;
        HideCursor();
        isHoveringRemote = false;
        ApplyCursorVisual(false);
        ApplyRemoteHoverFeedback(false);

        Debug.Log("ChapterRoomLookController: control disabled.");
    }

    public void DisableControlImmediate()
    {
        controlEnabled = false;
        HideCursor();
        isHoveringRemote = false;
        ApplyCursorVisual(false);
        ApplyRemoteHoverFeedback(false);
    }

    public void ResetConfirmedState()
    {
        hasConfirmedRemote = false;
    }

    private void ResolveReferences()
    {
        if (roomManager == null)
            roomManager = FindFirstObjectByType<ChapterRoomManager>();

        if (roomCamera == null && roomManager != null)
            roomCamera = roomManager.roomCamera;

        if (roomCamera == null)
            roomCamera = Camera.main;

        if (lookRoot == null && roomCamera != null)
            lookRoot = roomCamera.transform;

        if (handCursor != null && cursorCanvas == null)
            cursorCanvas = handCursor.GetComponentInParent<Canvas>();

        if (handCursor != null && cursorImage == null)
            cursorImage = handCursor.GetComponent<Image>();

        if (handCursor != null && cursorCanvasGroup == null)
        {
            cursorCanvasGroup = handCursor.GetComponent<CanvasGroup>();

            if (cursorCanvasGroup == null)
                cursorCanvasGroup = handCursor.gameObject.AddComponent<CanvasGroup>();
        }

        if (remoteHoverFeedback == null && remoteRoot != null)
            remoteHoverFeedback = remoteRoot.GetComponent<ObjectHoverFeedback>();
    }

    private void ResolveReferencesLazy()
    {
        if (roomManager == null || roomCamera == null || lookRoot == null)
            ResolveReferences();
    }

    private void CaptureBaseLookRotation()
    {
        if (lookRoot == null && roomCamera != null)
            lookRoot = roomCamera.transform;

        if (lookRoot != null)
            baseLocalRotation = lookRoot.localRotation;
    }

    private void CaptureCursorState()
    {
        if (handCursor == null)
            return;

        cursorStartAnchoredPosition = handCursor.anchoredPosition;
        cursorStartScale = handCursor.localScale;
    }

    private bool CanUseRoomControl()
    {
        if (roomManager == null)
            return true;

        if (requiredChapterIndex > 0 && roomManager.chapterIndex != requiredChapterIndex)
            return false;

        return roomManager.roomInputEnabled;
    }

    private void UpdateCameraLook()
    {
        if (lookRoot == null)
            return;

        Vector2 rightStick = ReadStick(lookStickSource, true);

        if (rightStick.sqrMagnitude <= 0.0001f)
            return;

        float yawDelta = rightStick.x * yawSpeed * Time.deltaTime;

        float yInput = invertY ? -rightStick.y : rightStick.y;
        float pitchDelta = -yInput * pitchSpeed * Time.deltaTime;

        currentYaw = Mathf.Clamp(currentYaw + yawDelta, minYaw, maxYaw);
        currentPitch = Mathf.Clamp(currentPitch + pitchDelta, minPitch, maxPitch);

        ApplyLookRotation();
    }

    private void ApplyLookRotation()
    {
        if (lookRoot == null)
            return;

        Quaternion offset = Quaternion.Euler(currentPitch, currentYaw, 0f);
        lookRoot.localRotation = baseLocalRotation * offset;
    }

    private void UpdateHandCursor()
    {
        if (handCursor == null)
            return;

        if (enableKeyboardMouseDebug && mouseControlsCursor)
        {
            Vector2 screen = Input.mousePosition;
            SetCursorToScreenPosition(screen);
            return;
        }

        Vector2 leftStick = ReadStick(cursorStickSource, false);

        if (leftStick.sqrMagnitude <= 0.0001f)
            return;

        Vector2 pos = handCursor.anchoredPosition;
        pos += leftStick * cursorSpeed * Time.deltaTime;

        RectTransform parentRect = handCursor.parent as RectTransform;
        if (parentRect != null)
        {
            Rect rect = parentRect.rect;

            float minX = rect.xMin + cursorPadding.x;
            float maxX = rect.xMax - cursorPadding.x;
            float minY = rect.yMin + cursorPadding.y;
            float maxY = rect.yMax - cursorPadding.y;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
        }

        handCursor.anchoredPosition = pos;
    }

    private void SetCursorToScreenPosition(Vector2 screenPosition)
    {
        if (handCursor == null)
            return;

        RectTransform parentRect = handCursor.parent as RectTransform;
        if (parentRect == null)
        {
            handCursor.position = screenPosition;
            return;
        }

        Camera uiCamera = null;

        if (cursorCanvas != null && cursorCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = cursorCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition, uiCamera, out Vector2 localPoint))
        {
            localPoint.x = Mathf.Clamp(
                localPoint.x,
                parentRect.rect.xMin + cursorPadding.x,
                parentRect.rect.xMax - cursorPadding.x
            );

            localPoint.y = Mathf.Clamp(
                localPoint.y,
                parentRect.rect.yMin + cursorPadding.y,
                parentRect.rect.yMax - cursorPadding.y
            );

            handCursor.anchoredPosition = localPoint;
        }
    }

    private void UpdateRemoteHover()
    {
        bool hovering = TryGetRemoteHit(out _);

        if (hovering == isHoveringRemote)
            return;

        isHoveringRemote = hovering;
        ApplyCursorVisual(isHoveringRemote);
        ApplyRemoteHoverFeedback(isHoveringRemote);
    }

    private void TryConfirmRemote()
    {
        if (!TryGetRemoteHit(out RaycastHit hit))
            return;

        hasConfirmedRemote = true;

        if (roomManager != null)
            roomManager.roomInputEnabled = false;

        DisableControl();

        if (hideRemoteAfterUse)
        {
            if (remoteRoot != null)
                remoteRoot.gameObject.SetActive(false);
            else if (hit.collider != null)
                hit.collider.gameObject.SetActive(false);
        }

        Debug.Log("ChapterRoomLookController: remote confirmed.");

        onRemoteConfirmed?.Invoke();
        onRemotePickedUp?.Invoke();

        if ((autoCallRoomManagerEnterFilm || callRoomManagerEnterFilmOnRemoteConfirmed) && roomManager != null)
            roomManager.EnterFilm();
    }

    private bool TryGetRemoteHit(out RaycastHit hit)
    {
        hit = default;

        if (roomCamera == null)
            return false;

        Vector2 screenPoint = GetCursorScreenPoint();
        Ray ray = roomCamera.ScreenPointToRay(screenPoint);

        if (!Physics.Raycast(ray, out hit, raycastDistance, interactMask, triggerInteraction))
            return false;

        return IsRemoteHit(hit.collider);
    }

    private bool IsRemoteHit(Collider hitCollider)
    {
        if (hitCollider == null)
            return false;

        if (remoteCollider != null)
        {
            if (hitCollider == remoteCollider)
                return true;

            if (hitCollider.transform.IsChildOf(remoteCollider.transform))
                return true;
        }

        if (remoteRoot != null)
        {
            if (hitCollider.transform == remoteRoot)
                return true;

            if (hitCollider.transform.IsChildOf(remoteRoot))
                return true;
        }

        if (!string.IsNullOrEmpty(remoteTag))
        {
            if (hitCollider.CompareTag(remoteTag))
                return true;

            Transform root = hitCollider.transform.root;
            if (root != null && root.CompareTag(remoteTag))
                return true;
        }

        return false;
    }

    private Vector2 GetCursorScreenPoint()
    {
        if (handCursor == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Camera uiCamera = null;

        if (cursorCanvas != null && cursorCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = cursorCanvas.worldCamera;

        return RectTransformUtility.WorldToScreenPoint(uiCamera, handCursor.position);
    }

    private void ShowCursor()
    {
        if (handCursor == null)
            return;

        handCursor.gameObject.SetActive(true);

        if (cursorPopRoutine != null)
            StopCoroutine(cursorPopRoutine);

        if (animateCursorOnEnable)
            cursorPopRoutine = StartCoroutine(CursorPopRoutine());
        else
        {
            if (cursorCanvasGroup != null)
                cursorCanvasGroup.alpha = 1f;

            handCursor.localScale = cursorStartScale;
        }
    }

    private void HideCursor()
    {
        if (cursorPopRoutine != null)
        {
            StopCoroutine(cursorPopRoutine);
            cursorPopRoutine = null;
        }

        if (cursorCanvasGroup != null)
            cursorCanvasGroup.alpha = 0f;

        if (handCursor != null)
        {
            handCursor.localScale = cursorStartScale;
            handCursor.gameObject.SetActive(false);
        }
    }

    private void ApplyCursorVisual(bool hovering)
    {
        if (cursorPopRoutine != null)
        {
            if (cursorImage != null)
                cursorImage.color = hovering ? cursorHoverColor : cursorNormalColor;

            return;
        }

        if (handCursor != null)
            handCursor.localScale = hovering ? cursorStartScale * cursorHoverScale : cursorStartScale;

        if (cursorImage != null)
            cursorImage.color = hovering ? cursorHoverColor : cursorNormalColor;
    }

    private void ApplyRemoteHoverFeedback(bool hovering)
    {
        if (remoteHoverFeedback != null)
            remoteHoverFeedback.SetHovering(hovering);
    }

    private IEnumerator CursorPopRoutine()
    {
        if (handCursor == null)
            yield break;

        if (cursorCanvasGroup != null)
            cursorCanvasGroup.alpha = 0f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, cursorPopDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale;

            if (t < 0.7f)
            {
                float phase = EvaluateCursorPopCurve(t / 0.7f);
                scale = Mathf.Lerp(cursorPopStartScale, cursorPopOvershootScale, phase);
            }
            else
            {
                float phase = EvaluateCursorPopCurve((t - 0.7f) / 0.3f);
                scale = Mathf.Lerp(cursorPopOvershootScale, cursorPopEndScale, phase);
            }

            handCursor.localScale = cursorStartScale * scale;

            if (cursorCanvasGroup != null)
                cursorCanvasGroup.alpha = Mathf.Clamp01(t / 0.65f);

            yield return null;
        }

        if (cursorCanvasGroup != null)
            cursorCanvasGroup.alpha = 1f;

        handCursor.localScale = isHoveringRemote
            ? cursorStartScale * cursorHoverScale
            : cursorStartScale * cursorPopEndScale;
        cursorPopRoutine = null;
    }

    private float EvaluateCursorPopCurve(float t)
    {
        if (cursorPopCurve == null || cursorPopCurve.length == 0)
            return t;

        return cursorPopCurve.Evaluate(t);
    }

    private Vector2 ReadStick(StickSource source, bool forLook)
    {
        return source == StickSource.LeftStick
            ? ReadLeftStick(forLook)
            : ReadRightStick(forLook);
    }

    private Vector2 ReadLeftStick(bool forLook)
    {
        Vector2 value = GameInputHub.LeftStick;

        if (enableKeyboardMouseDebug && !forLook)
        {
            value.x += Input.GetAxisRaw("Horizontal");
            value.y += Input.GetAxisRaw("Vertical");
        }

        return ApplyDeadZone(value);
    }

    private Vector2 ReadRightStick(bool forLook)
    {
        Vector2 value = GameInputHub.RightStick;

        if (forLook && enableKeyboardMouseDebug && mouseRightButtonLook && Input.GetMouseButton(1))
        {
            value.x += Input.GetAxis("Mouse X") * mouseLookMultiplier;
            value.y += Input.GetAxis("Mouse Y") * mouseLookMultiplier;
        }

        return ApplyDeadZone(value);
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        ResolveReferences();

        if (roomManager == null)
            Debug.LogWarning("ChapterRoomLookController: roomManager is missing.", this);
        if (roomCamera == null)
            Debug.LogWarning("ChapterRoomLookController: roomCamera is missing.", this);
        if (lookRoot == null)
            Debug.LogWarning("ChapterRoomLookController: lookRoot is missing.", this);
        if (handCursor == null || cursorImage == null)
            Debug.LogWarning("ChapterRoomLookController: HandCursor references are incomplete.", this);
        if (remoteRoot == null || remoteCollider == null)
            Debug.LogWarning("ChapterRoomLookController: remote references are incomplete.", this);
    }

    [ContextMenu("Auto Wire L2 References")]
    public void AutoWireL2References()
    {
        ResolveReferences();
        ValidateSetup();
    }

    private bool ReadInteractPressedThisFrame()
    {
        if (GameInputHub.R2PressedThisFrame)
            return true;

        if (enableKeyboardMouseDebug)
        {
            if (Input.GetKeyDown(debugInteractKey))
                return true;

            if (mouseLeftButtonInteract && Input.GetMouseButtonDown(0))
                return true;
        }

        return false;
    }

    private Vector2 ApplyDeadZone(Vector2 value)
    {
        if (value.magnitude < stickDeadZone)
            return Vector2.zero;

        return Vector2.ClampMagnitude(value, 1f);
    }
}

