using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Runs the held-remote stage: lift the remote, look/aim at the screen, show prompts, and count down.
/// It hands off to ChapterFilmEntryController after the countdown and does not switch cameras itself.
/// </summary>
[DisallowMultipleComponent]
public class ChapterRemoteAimEntryController : MonoBehaviour
{
    [Header("References")]
    public ChapterRoomManager roomManager;
    public ChapterRoomLookController roomLookController;
    public ChapterFilmEntryController filmEntryController;

    public int requiredChapterIndex = 2;

    public Camera roomCamera;
    public Transform lookRoot;

    [Header("Remote")]
    public GameObject worldRemoteObject;
    public GameObject heldRemoteObject;
    public Transform heldRemoteStartPoint;
    public Transform heldRemoteAimPoint;
    public float remoteLiftDuration = 0.6f;
    public AnimationCurve remoteLiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public bool hideWorldRemoteWhileAiming = true;

    [Header("Screen Aim")]
    public Transform screenTarget;
    public Collider screenCollider;
    public LayerMask screenMask = ~0;
    public float aimRayDistance = 30f;
    public float aimAssistAngle = 8f;
    public bool requireAimAtScreen = true;

    [Header("Automatic Screen Look")]
    public bool autoLookAtScreenOnBegin = true;
    public float autoLookAtScreenDuration = 0.75f;
    public bool allowAimInputAfterAutoLook = true;

    [Header("Aiming Look")]
    public float aimYawSpeed = 55f;
    public float aimPitchSpeed = 40f;
    public float minYaw = -25f;
    public float maxYaw = 25f;
    public float minPitch = -12f;
    public float maxPitch = 12f;
    public bool invertY = false;
    public float stickDeadZone = 0.12f;

    [Header("UI")]
    public TMP_Text screenPromptText;
    public string aimPromptText = "进入第二部电影";
    public string aimConfirmText = "按 R2";
    public bool showPromptBeforeCountdown = true;

    public TMP_Text screenCountdownText;
    public GameObject[] screenTextsToShowWhenAiming;
    public GameObject[] hideWhenAiming;
    public string[] countdownSteps = new string[] { "3", "2", "1" };
    public float countdownStepDuration = 0.55f;
    public bool hideCountdownAfterFinished = true;

    [Header("Debug")]
    public bool allowDebugKeyboard = true;
    public KeyCode debugConfirmKey = KeyCode.E;
    public bool verboseDebug = true;

    [Header("Events")]
    public UnityEvent onRemoteLiftStarted;
    public UnityEvent onRemoteLiftFinished;
    public UnityEvent onScreenAimed;
    public UnityEvent onCountdownStarted;
    public UnityEvent onCountdownFinished;
    public UnityEvent onAimCancelled;

    [Header("Debug State")]
    [SerializeField] private bool isLifting;
    [SerializeField] private bool isAiming;
    [SerializeField] private bool isCounting;
    [SerializeField] private bool isScreenAimed;

    private Coroutine activeRoutine;
    private Quaternion baseLocalRotation;
    private float currentYaw;
    private float currentPitch;
    private bool registeredReturnListener;

    void Awake()
    {
        ResolveReferences();
        ResetAimVisuals(false);
    }

    void OnEnable()
    {
        ResolveReferences();
        RegisterReturnListener();
    }

    void Start()
    {
        ResolveReferences();
        RegisterReturnListener();
        ResetAimVisuals(false);
    }

    void OnDisable()
    {
        UnregisterReturnListener();
    }

    void Update()
    {
        if (!isAiming || isCounting)
            return;

        if (allowAimInputAfterAutoLook)
            UpdateAimingLook();

        FollowAimPoint();

        bool aimedNow = IsAimingAtScreen();

        if (aimedNow && !isScreenAimed)
        {
            isScreenAimed = true;

            if (screenPromptText != null)
                screenPromptText.text = aimConfirmText;

            onScreenAimed?.Invoke();

            if (verboseDebug)
                Debug.Log("ChapterRemoteAimEntryController: screen aimed.");
        }
        else if (!aimedNow)
        {
            isScreenAimed = false;

            if (screenPromptText != null && showPromptBeforeCountdown)
                screenPromptText.text = aimPromptText;
        }

        if ((!requireAimAtScreen || aimedNow) && ReadConfirmPressedThisFrame())
            ConfirmScreenAndStartCountdown();
    }

    [ContextMenu("Begin Remote Aim")]
    public void BeginRemoteAim()
    {
        if (isLifting || isAiming || isCounting)
            return;

        ResolveReferences();

        if (requiredChapterIndex > 0 &&
            roomManager != null &&
            roomManager.chapterIndex != requiredChapterIndex)
        {
            if (verboseDebug)
                Debug.Log("ChapterRemoteAimEntryController: ignored begin because chapter does not match.");

            return;
        }

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(BeginRemoteAimRoutine());
    }

    public void CancelRemoteAim()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        isLifting = false;
        isAiming = false;
        isCounting = false;
        isScreenAimed = false;

        ResetAimVisuals(true);
        onAimCancelled?.Invoke();

        if (verboseDebug)
            Debug.Log("ChapterRemoteAimEntryController: aim cancelled, room control restored.");
    }

    public void ConfirmScreenAndStartCountdown()
    {
        if (!isAiming || isCounting)
            return;

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(CountdownRoutine());
    }

    private IEnumerator BeginRemoteAimRoutine()
    {
        isLifting = true;
        isScreenAimed = false;

        if (roomLookController != null)
            roomLookController.DisableControl();

        if (roomManager != null)
            roomManager.roomInputEnabled = false;

        SetObjectsActive(hideWhenAiming, false);
        SetObjectsActive(screenTextsToShowWhenAiming, true);

        if (hideWorldRemoteWhileAiming && worldRemoteObject != null)
            worldRemoteObject.SetActive(false);

        if (screenPromptText != null)
        {
            screenPromptText.text = showPromptBeforeCountdown ? aimPromptText : "";
            screenPromptText.gameObject.SetActive(showPromptBeforeCountdown);
        }

        if (screenCountdownText != null)
        {
            screenCountdownText.text = "";
            screenCountdownText.gameObject.SetActive(false);
        }

        onRemoteLiftStarted?.Invoke();

        if (heldRemoteObject != null)
        {
            heldRemoteObject.SetActive(true);

            Transform held = heldRemoteObject.transform;
            Vector3 fromPosition = heldRemoteStartPoint != null
                ? heldRemoteStartPoint.position
                : held.position;
            Quaternion fromRotation = heldRemoteStartPoint != null
                ? heldRemoteStartPoint.rotation
                : held.rotation;
            Vector3 toPosition = heldRemoteAimPoint != null
                ? heldRemoteAimPoint.position
                : fromPosition;
            Quaternion toRotation = heldRemoteAimPoint != null
                ? heldRemoteAimPoint.rotation
                : fromRotation;

            held.SetPositionAndRotation(fromPosition, fromRotation);

            float elapsed = 0f;
            float duration = Mathf.Max(0f, remoteLiftDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                float curved = EvaluateLiftCurve(t);

                held.position = Vector3.Lerp(fromPosition, toPosition, curved);
                held.rotation = Quaternion.Slerp(fromRotation, toRotation, curved);

                yield return null;
            }

            held.SetPositionAndRotation(toPosition, toRotation);
        }

        if (autoLookAtScreenOnBegin && screenTarget != null)
            yield return AutoLookAtScreenRoutine();

        CaptureAimRotation();
        isLifting = false;
        isAiming = true;
        activeRoutine = null;
        onRemoteLiftFinished?.Invoke();

        if (verboseDebug)
            Debug.Log("ChapterRemoteAimEntryController: remote lifted, waiting for screen aim.");
    }

    private IEnumerator CountdownRoutine()
    {
        isCounting = true;
        onCountdownStarted?.Invoke();

        if (screenPromptText != null)
        {
            screenPromptText.text = "";
            screenPromptText.gameObject.SetActive(false);
        }

        if (screenCountdownText != null)
            screenCountdownText.gameObject.SetActive(true);

        if (countdownSteps != null)
        {
            for (int i = 0; i < countdownSteps.Length; i++)
            {
                if (screenCountdownText != null)
                    screenCountdownText.text = countdownSteps[i];

                if (countdownStepDuration > 0f)
                    yield return new WaitForSeconds(countdownStepDuration);
                else
                    yield return null;
            }
        }

        if (screenCountdownText != null && hideCountdownAfterFinished)
        {
            screenCountdownText.text = "";
            screenCountdownText.gameObject.SetActive(false);
        }

        isCounting = false;
        isAiming = false;
        activeRoutine = null;
        onCountdownFinished?.Invoke();

        if (filmEntryController != null)
        {
            filmEntryController.EnterFilm();
        }
        else
        {
            Debug.LogWarning("ChapterRemoteAimEntryController: filmEntryController is missing. Returning to room.");
            CancelRemoteAim();
        }
    }

    private void ResolveReferences()
    {
        if (roomManager == null)
            roomManager = GetComponent<ChapterRoomManager>();

        if (roomLookController == null)
            roomLookController = GetComponent<ChapterRoomLookController>();

        if (filmEntryController == null)
            filmEntryController = GetComponent<ChapterFilmEntryController>();

        if (roomCamera == null && roomManager != null)
            roomCamera = roomManager.roomCamera;

        if (lookRoot == null && roomCamera != null)
            lookRoot = roomCamera.transform;
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        ResolveReferences();

        if (roomManager == null || roomLookController == null || filmEntryController == null)
            Debug.LogWarning("ChapterRemoteAimEntryController: stage-controller references are incomplete.", this);
        if (roomCamera == null || lookRoot == null)
            Debug.LogWarning("ChapterRemoteAimEntryController: room camera/look root is missing.", this);
        if (worldRemoteObject == null || heldRemoteObject == null)
            Debug.LogWarning("ChapterRemoteAimEntryController: world or held remote is missing.", this);
        if (screenTarget == null && screenCollider == null)
            Debug.LogWarning("ChapterRemoteAimEntryController: no screen target or collider is assigned.", this);
        if (screenPromptText == null || screenCountdownText == null)
            Debug.LogWarning("ChapterRemoteAimEntryController: screen prompt/countdown text is incomplete.", this);
    }

    [ContextMenu("Auto Wire L2 References")]
    public void AutoWireL2References()
    {
        ResolveReferences();
        ValidateSetup();
    }

    private void RegisterReturnListener()
    {
        if (registeredReturnListener || filmEntryController == null)
            return;

        filmEntryController.onReturnedToRoomForDebug.RemoveListener(HandleReturnedToRoom);
        filmEntryController.onReturnedToRoomForDebug.AddListener(HandleReturnedToRoom);
        registeredReturnListener = true;
    }

    private void UnregisterReturnListener()
    {
        if (!registeredReturnListener || filmEntryController == null)
            return;

        filmEntryController.onReturnedToRoomForDebug.RemoveListener(HandleReturnedToRoom);
        registeredReturnListener = false;
    }

    private void HandleReturnedToRoom()
    {
        ResetAimVisuals(true);

        if (verboseDebug)
            Debug.Log("ChapterRemoteAimEntryController: test entry returned to room.");
    }

    private void ResetAimVisuals(bool restoreRoomControl)
    {
        isLifting = false;
        isAiming = false;
        isCounting = false;
        isScreenAimed = false;

        if (worldRemoteObject != null)
            worldRemoteObject.SetActive(true);

        if (heldRemoteObject != null)
            heldRemoteObject.SetActive(false);

        SetObjectsActive(screenTextsToShowWhenAiming, false);

        if (screenPromptText != null)
        {
            screenPromptText.text = "";
            screenPromptText.gameObject.SetActive(false);
        }

        if (screenCountdownText != null)
        {
            screenCountdownText.text = "";

            if (hideCountdownAfterFinished)
                screenCountdownText.gameObject.SetActive(false);
        }

        if (!restoreRoomControl)
            return;

        SetObjectsActive(hideWhenAiming, true);

        if (roomManager != null)
            roomManager.roomInputEnabled = true;

        if (roomLookController != null)
        {
            roomLookController.ResetConfirmedState();
            roomLookController.EnableControl();
        }
    }

    private void CaptureAimRotation()
    {
        if (lookRoot == null)
            return;

        baseLocalRotation = lookRoot.localRotation;
        currentYaw = 0f;
        currentPitch = 0f;
    }

    private IEnumerator AutoLookAtScreenRoutine()
    {
        Transform pivot = lookRoot != null
            ? lookRoot
            : roomCamera != null
                ? roomCamera.transform
                : null;

        if (pivot == null || screenTarget == null)
            yield break;

        Vector3 direction = screenTarget.position - pivot.position;

        if (direction.sqrMagnitude <= 0.0001f)
            yield break;

        Quaternion from = pivot.rotation;
        Quaternion to = Quaternion.LookRotation(direction.normalized, Vector3.up);
        float duration = Mathf.Max(0f, autoLookAtScreenDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float curved = Mathf.SmoothStep(0f, 1f, t);

            pivot.rotation = Quaternion.Slerp(from, to, curved);
            FollowAimPoint();

            yield return null;
        }

        pivot.rotation = to;
    }

    private void UpdateAimingLook()
    {
        if (lookRoot == null)
            return;

        Vector2 input = ApplyDeadZone(GameInputHub.RightStick);

        if (input.sqrMagnitude <= 0.0001f)
            return;

        currentYaw = Mathf.Clamp(currentYaw + input.x * aimYawSpeed * Time.deltaTime, minYaw, maxYaw);

        float yInput = invertY ? -input.y : input.y;
        currentPitch = Mathf.Clamp(currentPitch - yInput * aimPitchSpeed * Time.deltaTime, minPitch, maxPitch);

        lookRoot.localRotation =
            baseLocalRotation *
            Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    private void FollowAimPoint()
    {
        if (heldRemoteObject == null || heldRemoteAimPoint == null)
            return;

        heldRemoteObject.transform.SetPositionAndRotation(
            heldRemoteAimPoint.position,
            heldRemoteAimPoint.rotation
        );
    }

    private bool IsAimingAtScreen()
    {
        if (roomCamera == null)
            return false;

        if (screenCollider != null)
        {
            Ray ray = roomCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, aimRayDistance, screenMask, QueryTriggerInteraction.Collide))
            {
                if (hit.collider == screenCollider)
                    return true;

                if (hit.collider.transform.IsChildOf(screenCollider.transform))
                    return true;
            }
        }

        if (screenTarget == null)
            return !requireAimAtScreen;

        Vector3 direction = screenTarget.position - roomCamera.transform.position;

        if (direction.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Angle(roomCamera.transform.forward, direction.normalized) <= aimAssistAngle;
    }

    private bool ReadConfirmPressedThisFrame()
    {
        if (GameInputHub.R2PressedThisFrame)
            return true;

        return allowDebugKeyboard && Input.GetKeyDown(debugConfirmKey);
    }

    private Vector2 ApplyDeadZone(Vector2 value)
    {
        if (value.magnitude < stickDeadZone)
            return Vector2.zero;

        return Vector2.ClampMagnitude(value, 1f);
    }

    private float EvaluateLiftCurve(float t)
    {
        if (remoteLiftCurve == null || remoteLiftCurve.length == 0)
            return t;

        return remoteLiftCurve.Evaluate(t);
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
}
