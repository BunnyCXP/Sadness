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
    public GameObject ticketFrontObject;
    public GameObject ticketBackObject;
    public TMP_Text ticketBackText;
    public string ticketBackMessage = "let's look forward";

    [Header("Timing")]
    public float lookDownDuration = 0.8f;
    public float ticketRaiseDuration = 0.8f;
    public float ticketHoldDuration = 0.6f;
    public float ticketFlipDuration = 0.7f;
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
    private bool listenerRegistered;

    void Awake()
    {
        ResolveReferences();
        ResetTicketVisuals();
    }

    void OnEnable()
    {
        ResolveReferences();
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

        if (ticketBackText != null)
            ticketBackText.text = ticketBackMessage;

        SetTicketSides(true);

        if (heldTicketObject != null)
        {
            heldTicketObject.SetActive(true);

            Transform ticket = heldTicketObject.transform;
            Vector3 startPosition = ticketStartPoint != null ? ticketStartPoint.position : ticket.position;
            Quaternion startRotation = ticketStartPoint != null ? ticketStartPoint.rotation : ticket.rotation;
            Vector3 viewPosition = ticketViewPoint != null ? ticketViewPoint.position : startPosition;
            Quaternion viewRotation = ticketViewPoint != null ? ticketViewPoint.rotation : startRotation;
            ticket.SetPositionAndRotation(startPosition, startRotation);

            yield return LookAtTicketRoutine();
            yield return MoveTransformRoutine(ticket, startPosition, startRotation, viewPosition, viewRotation, ticketRaiseDuration);

            if (ticketHoldDuration > 0f)
                yield return new WaitForSeconds(ticketHoldDuration);

            Quaternion flippedRotation = viewRotation * Quaternion.Euler(0f, 180f, 0f);
            yield return MoveTransformRoutine(ticket, viewPosition, viewRotation, viewPosition, flippedRotation, ticketFlipDuration);
            SetTicketSides(false);
            onTicketBackRevealed?.Invoke();

            if (subtitlePlayer != null && ticketRevealLines != null && ticketRevealLines.Length > 0)
                yield return subtitlePlayer.PlayLinesRoutine(ticketRevealLines);

            if (afterFlipHoldDuration > 0f)
                yield return new WaitForSeconds(afterFlipHoldDuration);

            if (subtitlePlayer != null && afterTicketLines != null && afterTicketLines.Length > 0)
                yield return subtitlePlayer.PlayLinesRoutine(afterTicketLines);

            heldTicketObject.SetActive(false);
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

    private IEnumerator MoveTransformRoutine(
        Transform target,
        Vector3 fromPosition,
        Quaternion fromRotation,
        Vector3 toPosition,
        Quaternion toRotation,
        float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)) : 1f;
            target.SetPositionAndRotation(
                Vector3.Lerp(fromPosition, toPosition, t),
                Quaternion.Slerp(fromRotation, toRotation, t)
            );
            yield return null;
        }

        target.SetPositionAndRotation(toPosition, toRotation);
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

        SetTicketSides(true);
    }

    private void SetTicketSides(bool showFront)
    {
        if (ticketFrontObject != null)
            ticketFrontObject.SetActive(showFront);
        if (ticketBackObject != null)
            ticketBackObject.SetActive(!showFront);
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        ResolveReferences();

        if (roomManager == null || roomLookController == null || roomCamera == null)
            Debug.LogWarning("ChapterArrivalTicketSequence: room references are incomplete.", this);
        if (heldTicketObject == null || ticketStartPoint == null || ticketViewPoint == null)
            Debug.LogWarning("ChapterArrivalTicketSequence: ticket presentation references are incomplete.", this);
    }
}
