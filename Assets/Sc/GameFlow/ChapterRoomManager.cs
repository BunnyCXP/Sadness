using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns chapter/room state and room-input permission. It does not move the HandCursor, animate the
/// remote, run countdowns, or perform the room-to-movie camera transition.
/// </summary>
public class ChapterRoomManager : MonoBehaviour
{
    [Header("Chapter")]
    public int chapterIndex = 1;
    public int filmIndexInThisChapter = 1;

    [Header("Roots")]
    public GameObject roomRoot;
    public GameObject filmRoot;

    [Header("Cameras")]
    public Camera roomCamera;
    public Camera movieCamera;

    [Header("Existing Systems")]
    public RoomInteraction roomInteraction;
    public CameraSwitch cameraSwitch;
    public InteractionManager interactionManager;
    public GameInputHub inputHub;

    [Header("Optional Room State Objects")]
    public GameObject rachelObject;
    public GameObject discObject;
    public GameObject nextClueObject;
    public GameObject lightDoorObject;

    [Header("Behaviour")]
    public bool thisChapterHasOpeningRoomSequence = false;
    public bool disableRoomInteractionOnReturnedChapters = true;
    public bool enableRoomInputAfterReveal = true;
    public bool roomInputEnabled;
    public bool allowR2ToEnterFilm = true;
    public bool forceEnableDirectR2InNormalRoom = false;
    public bool useCameraSwitchForEnterFilm = false;
    public bool verboseDebug = true;

    [Header("Direct Play Debug")]
    public bool debugEnableRoomInputWhenPlayedDirectly = false;
    public float debugDirectPlayDelay = 0.2f;

    [Header("Events")]
    public UnityEvent onRoomRevealed;
    public UnityEvent onEnterFilmRequested;
    public UnityEvent onEndingReady;

    private void Start()
    {
        ApplyChapterStateFromGameFlow();

        if (debugEnableRoomInputWhenPlayedDirectly)
            StartCoroutine(EnableRoomInputForDirectPlayRoutine());
    }

    private void Update()
    {
        if (!roomInputEnabled || !allowR2ToEnterFilm)
            return;

        if (GameInputHub.R2PressedThisFrame)
            EnterFilm();
    }

    public void ApplyChapterStateFromGameFlow()
    {
        if (verboseDebug)
            Debug.Log("ChapterRoomManager: ApplyChapterStateFromGameFlow");

        if (GameFlowManager.Instance == null)
        {
            if (verboseDebug)
                Debug.Log("ChapterRoomManager: GameFlowManager not ready; keeping scene defaults.");

            return;
        }

        if (chapterIndex == 1 &&
            thisChapterHasOpeningRoomSequence &&
            GameFlowManager.Instance.lastFinishedFilmIndex == 0)
        {
            roomInputEnabled = false;
            return;
        }

        if (GameFlowManager.Instance.lastFinishedFilmIndex == 3 && chapterIndex == 3)
        {
            ApplyEndingReadyRoom();
            return;
        }

        ApplyNormalChapterRoom();
    }

    public void ApplyNormalChapterRoom()
    {
        EnterRoomMode();

        if (lightDoorObject != null)
            lightDoorObject.SetActive(false);

        if (forceEnableDirectR2InNormalRoom)
            allowR2ToEnterFilm = true;

        if (verboseDebug)
            Debug.Log("ChapterRoomManager: applied normal room state for chapter " + chapterIndex);
    }

    public void EnterRoomMode()
    {
        if (roomRoot != null)
            roomRoot.SetActive(true);

        if (roomCamera != null)
        {
            roomCamera.gameObject.SetActive(true);
            roomCamera.enabled = true;
        }

        if (movieCamera != null)
        {
            movieCamera.enabled = false;
            movieCamera.gameObject.SetActive(false);
        }

        if (interactionManager != null)
            interactionManager.canInteract = false;

        roomInputEnabled = false;

        if (!thisChapterHasOpeningRoomSequence &&
            disableRoomInteractionOnReturnedChapters &&
            roomInteraction != null)
        {
            roomInteraction.enabled = false;
        }

        if (cameraSwitch != null)
        {
            SetBarHeight(cameraSwitch.topBar, 0f);
            SetBarHeight(cameraSwitch.bottomBar, 0f);

            if (cameraSwitch.whiteFlashGroup != null)
                cameraSwitch.whiteFlashGroup.alpha = 0f;
        }
    }

    public void ApplyEndingReadyRoom()
    {
        EnterRoomMode();

        if (lightDoorObject != null)
            lightDoorObject.SetActive(true);

        if (discObject != null)
            discObject.SetActive(false);

        if (rachelObject != null)
            rachelObject.SetActive(false);

        allowR2ToEnterFilm = false;
        onEndingReady?.Invoke();

        if (verboseDebug)
            Debug.Log("ChapterRoomManager: EndingReady room state applied.");
    }

    public void EnterFilm()
    {
        if (chapterIndex == 1 && thisChapterHasOpeningRoomSequence)
        {
            if (verboseDebug)
                Debug.Log("ChapterRoomManager: L1 entry remains controlled by RoomInteraction and CameraSwitch.");

            return;
        }

        roomInputEnabled = false;

        if (useCameraSwitchForEnterFilm && cameraSwitch != null)
            cameraSwitch.StartCinematicFromRemote();

        onEnterFilmRequested?.Invoke();

        if (verboseDebug)
            Debug.Log("ChapterRoomManager: EnterFilm requested for chapter " + chapterIndex);
    }

    public void GoToEnding()
    {
        if (GameFlowManager.Instance != null)
            GameFlowManager.Instance.GoToEnding();
        else
            Debug.LogWarning("ChapterRoomManager: GameFlowManager.Instance is missing.");
    }

    public void OnRoomRevealComplete()
    {
        if (!(chapterIndex == 1 && thisChapterHasOpeningRoomSequence))
            roomInputEnabled = enableRoomInputAfterReveal;

        if (verboseDebug)
        {
            Debug.Log("ChapterRoomManager: OnRoomRevealComplete");

            if (roomInputEnabled)
                Debug.Log("ChapterRoomManager: room input enabled");
            else
                Debug.Log("ChapterRoomManager: room input remains disabled for chapter " + chapterIndex);
        }

        onRoomRevealed?.Invoke();
    }

    public void NotifyRoomRevealed()
    {
        OnRoomRevealComplete();
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        if (roomRoot == null)
            Debug.LogWarning("ChapterRoomManager: roomRoot is missing.", this);
        if (roomCamera == null)
            Debug.LogWarning("ChapterRoomManager: roomCamera is missing.", this);
        if (chapterIndex > 1 && roomInteraction != null && roomInteraction.enabled)
            Debug.LogWarning("ChapterRoomManager: returned chapters should not run the L1 RoomInteraction.", this);
        if (chapterIndex > 1 && cameraSwitch != null && cameraSwitch.enabled)
            Debug.LogWarning("ChapterRoomManager: returned chapters should not run the L1 CameraSwitch.", this);
    }

    [ContextMenu("Debug Start From Room")]
    public void DebugStartFromRoom()
    {
        ApplyNormalChapterRoom();
        OnRoomRevealComplete();
    }

    private IEnumerator EnableRoomInputForDirectPlayRoutine()
    {
        if (debugDirectPlayDelay > 0f)
            yield return new WaitForSeconds(debugDirectPlayDelay);
        else
            yield return null;

        if (!debugEnableRoomInputWhenPlayedDirectly)
            yield break;

        if (!string.Equals(SceneManager.GetActiveScene().name, "L2", System.StringComparison.OrdinalIgnoreCase))
            yield break;

        if (GameFlowManager.Instance != null && GameFlowManager.Instance.lastFinishedFilmIndex != 0)
            yield break;

        ApplyNormalChapterRoom();
        OnRoomRevealComplete();

        if (verboseDebug)
            Debug.Log("ChapterRoomManager: direct L2 play debug room input enabled.");
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
