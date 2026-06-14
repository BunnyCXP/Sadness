using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Optional chapter-arrival vignette. It temporarily owns room input/camera framing, presents a ticket,
/// plays reusable dialogue, then returns control to ChapterRoomLookController.
/// </summary>
[DisallowMultipleComponent]
public class ChapterArrivalTicketSequence : MonoBehaviour
{
    public ChapterRoomManager roomManager;
    public ChapterRoomLookController roomLookController;
    public Camera roomCamera;

    [Header("Playback")]
    public bool playOnRoomRevealed = false;
    public bool onlyPlayAfterFilmOne = true;
    public bool debugSkipArrivalSequence = false;

    [Header("Ticket")]
    public GameObject heldTicketObject;
    public Transform ticketStartPoint;
    public Transform ticketViewPoint;
    public Transform ticketLookTarget;
    public TMP_Text ticketBackText;
    public string ticketBackMessage = "let's look forward";

    [Header("Simple Cube Ticket Flip")]
    public bool useSimpleCubeFlip = true;
    public float ticketFlipDuration = 0.7f;
    public Vector3 simpleFlipEuler = new Vector3(0f, 180f, 0f);
    public AnimationCurve simpleFlipCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Ticket Withdraw")]
    public Transform ticketExitPoint;
    public float ticketWithdrawDuration = 0.65f;
    public AnimationCurve ticketWithdrawCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public bool hideTicketAfterWithdraw = true;

    [Header("Timing")]
    public float lookDownDuration = 0.8f;
    public float ticketRaiseDuration = 0.8f;
    public float ticketHoldDuration = 0.6f;
    public float afterFlipHoldDuration = 1.2f;

    [Header("Dialogue")]
    public ChapterDialogueSubtitlePlayer subtitlePlayer;
    public DialogueLine[] ticketRevealLines;
    public DialogueLine[] afterTicketLines;

    [Header("Events")]
    public UnityEvent onSequenceStarted;
    public UnityEvent onTicketBackRevealed;
    public UnityEvent onSequenceFinished;

    [SerializeField] private bool isPlaying;

    private Coroutine sequenceRoutine;
    private Quaternion originalCameraRotation;
    private Quaternion ticketFrontRotation;
    private Quaternion ticketBackRotation;
    private bool hasTicketFrontRotation;
    private bool listenerRegistered;

    void Awake()
    {
        ResolveReferences();
        CaptureTicketFrontRotation();
        ResetTicketVisuals();
    }

    void OnEnable()
    {
        ResolveReferences();
        CaptureTicketFrontRotation();
        RegisterListener();
    }

    void OnDisable()
    {
        UnregisterListener();
    }

    [ContextMenu("Debug Play Arrival Sequence")]
    public void PlaySequence()
    {
        if (isPlaying)
            return;

        ResolveReferences();

        if (debugSkipArrivalSequence)
        {
            RestoreRoomControl();
            return;
        }

        if (onlyPlayAfterFilmOne &&
            GameFlowManager.Instance != null &&
            GameFlowManager.Instance.lastFinishedFilmIndex != 0 &&
            GameFlowManager.Instance.lastFinishedFilmIndex != 1)
        {
            RestoreRoomControl();
            return;
        }

        sequenceRoutine = StartCoroutine(PlaySequenceRoutine());
    }

    private IEnumerator PlaySequenceRoutine()
    {
        isPlaying = true;
        onSequenceStarted?.Invoke();

        if (roomManager != null)
            roomManager.roomInputEnabled = false;

        if (roomLookController != null)
            roomLookController.DisableControl();

        if (roomCamera != null)
            originalCameraRotation = roomCamera.transform.rotation;

        if (heldTicketObject != null)
            heldTicketObject.SetActive(true);

        ResetTicketToFront();

        if (heldTicketObject != null)
        {
            Transform ticket = heldTicketObject.transform;
            Vector3 startPosition = ticketStartPoint != null ? ticketStartPoint.position : ticket.position;
            Vector3 viewPosition = ticketViewPoint != null ? ticketViewPoint.position : startPosition;
            ticket.position = startPosition;

            yield return LookAtTicketRoutine();
            yield return MoveTicketPositionRoutine(
                ticket,
                startPosition,
                viewPosition,
                ticketRaiseDuration
            );

            Debug.Log("Ticket sequence: arrived at view point", this);
            Debug.Log(
                "Ticket arrived view point. Held rotation=" + heldTicketObject.transform.eulerAngles,
                this
            );

            if (useSimpleCubeFlip)
                yield return SimpleFlipTicketCubeRoutine();

            onTicketBackRevealed?.Invoke();

            if (ticketHoldDuration > 0f)
                yield return new WaitForSeconds(ticketHoldDuration);

            if (subtitlePlayer != null && ticketRevealLines != null && ticketRevealLines.Length > 0)
                yield return subtitlePlayer.PlayLinesRoutine(ticketRevealLines);

            if (afterFlipHoldDuration > 0f)
                yield return new WaitForSeconds(afterFlipHoldDuration);

            if (subtitlePlayer != null && afterTicketLines != null && afterTicketLines.Length > 0)
                yield return subtitlePlayer.PlayLinesRoutine(afterTicketLines);

            yield return WithdrawTicketRoutine();
        }

        yield return RestoreCameraRoutine();
        RestoreRoomControl();

        isPlaying = false;
        sequenceRoutine = null;
        onSequenceFinished?.Invoke();
    }

    private IEnumerator LookAtTicketRoutine()
    {
        if (roomCamera == null || ticketLookTarget == null)
            yield break;

        Transform cameraTransform = roomCamera.transform;
        Vector3 direction = ticketLookTarget.position - cameraTransform.position;

        if (direction.sqrMagnitude <= 0.0001f)
            yield break;

        Quaternion from = cameraTransform.rotation;
        Quaternion to = Quaternion.LookRotation(direction.normalized, Vector3.up);
        yield return RotateTransformRoutine(cameraTransform, from, to, lookDownDuration);
    }

    private IEnumerator RestoreCameraRoutine()
    {
        if (roomCamera == null)
            yield break;

        Transform cameraTransform = roomCamera.transform;
        yield return RotateTransformRoutine(
            cameraTransform,
            cameraTransform.rotation,
            originalCameraRotation,
            lookDownDuration
        );
    }

    private IEnumerator RotateTransformRoutine(Transform target, Quaternion from, Quaternion to, float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)) : 1f;
            target.rotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }

        target.rotation = to;
    }

    private IEnumerator MoveTicketPositionRoutine(
        Transform target,
        Vector3 fromPosition,
        Vector3 toPosition,
        float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)) : 1f;
            target.position = Vector3.Lerp(fromPosition, toPosition, t);

            yield return null;
        }

        target.position = toPosition;
    }

    private IEnumerator WithdrawTicketRoutine()
    {
        if (heldTicketObject == null)
            yield break;

        if (ticketExitPoint == null)
        {
            Debug.LogWarning(
                "ChapterArrivalTicketSequence: ticketExitPoint is missing, hiding ticket immediately.",
                this
            );
            heldTicketObject.SetActive(false);
            yield break;
        }

        Transform ticket = heldTicketObject.transform;
        Vector3 fromPosition = ticket.position;
        Vector3 toPosition = ticketExitPoint.position;
        float duration = Mathf.Max(0f, ticketWithdrawDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float curved = ticketWithdrawCurve != null && ticketWithdrawCurve.length > 0
                ? ticketWithdrawCurve.Evaluate(t)
                : t;

            ticket.position = Vector3.Lerp(fromPosition, toPosition, curved);

            yield return null;
        }

        ticket.position = toPosition;

        if (hideTicketAfterWithdraw)
            heldTicketObject.SetActive(false);
    }

    private void RestoreRoomControl()
    {
        if (roomManager != null)
            roomManager.roomInputEnabled = true;

        if (roomLookController != null)
            roomLookController.EnableControl();
    }

    private void ResolveReferences()
    {
        if (roomManager == null)
            roomManager = GetComponent<ChapterRoomManager>();
        if (roomLookController == null)
            roomLookController = GetComponent<ChapterRoomLookController>();
        if (roomCamera == null && roomManager != null)
            roomCamera = roomManager.roomCamera;
        if (subtitlePlayer == null)
            subtitlePlayer = GetComponent<ChapterDialogueSubtitlePlayer>();
    }

    private void RegisterListener()
    {
        if (listenerRegistered || !playOnRoomRevealed || roomManager == null)
            return;

        roomManager.onRoomRevealed.RemoveListener(PlaySequence);
        roomManager.onRoomRevealed.AddListener(PlaySequence);
        listenerRegistered = true;
    }

    private void UnregisterListener()
    {
        if (!listenerRegistered || roomManager == null)
            return;

        roomManager.onRoomRevealed.RemoveListener(PlaySequence);
        listenerRegistered = false;
    }

    private void ResetTicketVisuals()
    {
        if (heldTicketObject != null)
            heldTicketObject.SetActive(false);

        ResetTicketToFront();
    }

    private void ResetTicketToFront()
    {
        CaptureTicketFrontRotation();

        if (heldTicketObject != null && hasTicketFrontRotation)
            heldTicketObject.transform.rotation = ticketFrontRotation;

        ApplyTicketBackMessage();
        Debug.Log("Ticket sequence: reset to FRONT", this);
    }

    private IEnumerator SimpleFlipTicketCubeRoutine()
    {
        if (heldTicketObject == null)
            yield break;

        Transform ticket = heldTicketObject.transform;
        Quaternion startRotation = ticket.rotation;
        Quaternion endRotation = ticketBackRotation;
        float duration = Mathf.Max(0f, ticketFlipDuration);
        float elapsed = 0f;

        ApplyTicketBackMessage();
        Debug.Log("Ticket sequence: simple cube flip start", this);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float raw = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float t = simpleFlipCurve != null && simpleFlipCurve.length > 0
                ? simpleFlipCurve.Evaluate(raw)
                : raw;

            ticket.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }

        ticket.rotation = endRotation;
        Debug.Log("Ticket sequence: simple cube flip finished", this);
    }

    private void CaptureTicketFrontRotation()
    {
        if (hasTicketFrontRotation || heldTicketObject == null)
            return;

        ticketFrontRotation = heldTicketObject.transform.rotation;
        ticketBackRotation = ticketFrontRotation * Quaternion.Euler(simpleFlipEuler);
        hasTicketFrontRotation = true;
    }

    private void ApplyTicketBackMessage()
    {
        if (ticketBackText != null)
        {
            ticketBackText.text = ticketBackMessage;
            return;
        }

        Debug.LogWarning("ChapterArrivalTicketSequence: ticketBackText is missing.", this);
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        ResolveReferences();

        if (roomManager == null || roomLookController == null || roomCamera == null)
            Debug.LogWarning("ChapterArrivalTicketSequence: room references are incomplete.", this);
        if (heldTicketObject == null || ticketStartPoint == null || ticketViewPoint == null)
            Debug.LogWarning("ChapterArrivalTicketSequence: ticket presentation references are incomplete.", this);
        if (ticketExitPoint == null)
            Debug.LogWarning("ChapterArrivalTicketSequence: ticketExitPoint is missing.", this);
        if (ticketBackText == null)
            Debug.LogWarning("ChapterArrivalTicketSequence: ticketBackText is missing.", this);
    }
}
