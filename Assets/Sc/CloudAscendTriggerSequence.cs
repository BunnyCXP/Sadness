using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CloudAscendTriggerSequence : MonoBehaviour
{
    public enum CloudLayoutPreset
    {
        FiveSquare1920,
        SixSquare1920,
        EightSquare1920
    }

    [System.Serializable]
    public class CloudPiece
    {
        public RectTransform rect;
        public CanvasGroup canvasGroup;

        [Header("Placement")]
        public float xOffset;
        public float startY = 1400f;
        public float coverY = 0f;
        public float exitY = -1400f;

        [Header("Timing")]
        public float enterDelay = 0f;
        public float exitDelay = 0f;

        [Header("Look")]
        [Range(0f, 1f)]
        [Tooltip("Target visible alpha during the cover phase. Usually 1.")]
        public float alpha = 1f;

        public Vector3 scale = Vector3.one;

        [Tooltip("Optional. If Apply Size is enabled, this sets the UI Image size.")]
        public Vector2 size = new Vector2(1400f, 1400f);

        public bool applySize = true;

        [Header("Motion")]
        public float wobbleAmplitude = 10f;
        public float wobbleFrequency = 1f;
    }

    [Header("Trigger")]
    public bool triggerOnce = true;
    public string requiredTag = "";
    public bool useLayerMask = false;
    public LayerMask requiredLayerMask = ~0;

    [Header("Trigger Reliability")]
    public bool useTriggerEnter = true;
    public bool useTriggerStay = true;
    public bool useTrainDistanceFallback = true;
    public float fallbackTriggerDistance = 0.8f;
    public bool ensureKinematicRigidbodyOnTrigger = true;
    public bool verboseTriggerDebug = true;

    [Header("Trigger Guards")]
    public bool requirePlayerControlEnabled = true;
    public bool requireMovieCameraActive = true;
    public bool requireExitBeforeRetrigger = true;
    public float retriggerCooldown = 0.75f;
    public float distanceFallbackExitDistance = 1.4f;

    [Header("References")]
    public TrainOnRails train;
    public InteractionManager interactionManager;
    public Camera movieCamera;
    public Transform cameraMoveRoot;
    public Transform platformRoot;

    [Header("Camera Motion")]
    public Vector3 cameraWorldOffset = new Vector3(0f, 5f, 0f);

    [Header("Platform Motion")]
    public bool movePlatformByOffset = true;
    public Vector3 platformTargetPositionOrOffset = new Vector3(0f, 5f, 0f);
    public bool alsoMoveTrain = false;
    public Transform trainRootToMove;

    [Header("Lift Motion")]
    public float liftDuration = 2.6f;
    public AnimationCurve liftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("External Controllers")]
    public Behaviour[] behavioursToDisableDuringSequence;
    public bool disableBehavioursDuringSequence = true;

    [Header("Cloud Canvas")]
    [Tooltip("CloudCoverCanvas. If empty, the script will find a parent Canvas from CloudPiecesRoot or the first cloud piece.")]
    public Canvas cloudCanvas;

    [Tooltip("Parent that contains CloudPiece UI Image children.")]
    public RectTransform cloudPiecesRoot;

    public bool forceCloudCanvasOnTop = true;
    public int cloudCanvasSortingOrder = 32767;
    public bool forceSetAsLastSibling = true;
    public bool forceParentCanvasGroupsVisible = true;
    public bool forceImageAlphaToOneOnPrepare = true;

    [Header("Cloud Pieces")]
    public bool useCloudPieces = true;
    public List<CloudPiece> cloudPieces = new List<CloudPiece>();

    [Header("Cloud Piece Auto Setup")]
    public bool autoFindCloudPiecesIfListEmpty = true;
    public bool autoApplyLayoutIfListEmpty = false;
    public CloudLayoutPreset autoLayoutPreset = CloudLayoutPreset.FiveSquare1920;
    public Vector2 autoPieceSize = new Vector2(1400f, 1400f);
    public bool autoFixZeroAlphaToOne = true;

    [Header("Cloud Piece Timing")]
    public float cloudEnterDuration = 1.0f;
    public float cloudExitDuration = 1.0f;
    public AnimationCurve cloudPieceMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Visible Lift During Cloud Exit")]
    [Tooltip("When enabled, clouds start exiting before the lift finishes, so the platform/camera are still moving while the clouds slide down.")]
    public bool continueLiftDuringCloudExit = true;

    [Range(0.05f, 0.98f)]
    [Tooltip("Lift progress at which cloud pieces begin exiting. Lower = more remaining platform movement while clouds go down. Try 0.65-0.8.")]
    public float liftProgressWhenCloudExitStarts = 0.72f;

    [Tooltip("If true, player control waits until the lift finishes even if the clouds have already exited.")]
    public bool waitForLiftAfterCloudExit = true;

    [Header("Cloud Cover Hold")]
    public bool snapCoveredStateAfterEnter = true;

    [Header("Cloud Piece Visibility")]
    public bool hideCloudPiecesOnStart = true;

    [Header("Debug")]
    public bool logCloudPieceState = true;
    [SerializeField] private bool hasTriggered;
    [SerializeField] private bool isRunning;
    [SerializeField] private bool hasExitedAfterTrigger = true;
    [SerializeField] private bool distanceFallbackCanTrigger = true;
    [SerializeField] private float currentLiftProgress;

    private bool[] disabledBehaviourOriginalStates;
    private float lastTriggerTime = -999f;

    void OnValidate()
    {
        if (!autoFixZeroAlphaToOne || cloudPieces == null)
            return;

        for (int i = 0; i < cloudPieces.Count; i++)
        {
            CloudPiece piece = cloudPieces[i];
            if (piece != null && piece.alpha <= 0f)
                piece.alpha = 1f;
        }
    }

    void Awake()
    {
        ResolveCloudCanvas();
        ForceCloudCanvasOnTop();
    }

    void Start()
    {
        SetupTriggerColliderAndRigidbody();

        if (autoFindCloudPiecesIfListEmpty && (cloudPieces == null || cloudPieces.Count == 0))
        {
            AutoFindCloudPiecesFromRoot();

            if (autoApplyLayoutIfListEmpty)
                ApplyPresetLayout(autoLayoutPreset);
        }

        EnsureAllCloudPieceReferences();
        ForceCloudCanvasOnTop();

        if (hideCloudPiecesOnStart)
            HideCloudPiecesImmediate();
    }

    void Update()
    {
        if (!useTrainDistanceFallback || train == null)
            return;

        if (isRunning || (triggerOnce && hasTriggered))
            return;

        float distance = Vector3.Distance(train.transform.position, transform.position);

        if (distance > distanceFallbackExitDistance)
        {
            distanceFallbackCanTrigger = true;
            hasExitedAfterTrigger = true;
        }

        if (distanceFallbackCanTrigger && distance <= fallbackTriggerDistance)
        {
            distanceFallbackCanTrigger = false;
            StartSequence("DistanceFallback");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!useTriggerEnter)
            return;

        TryStartFromCollider(other, "OnTriggerEnter");
    }

    void OnTriggerStay(Collider other)
    {
        if (!useTriggerStay)
            return;

        TryStartFromCollider(other, "OnTriggerStay");
    }

    void OnTriggerExit(Collider other)
    {
        if (IsValidTrigger(other, "OnTriggerExit"))
        {
            hasExitedAfterTrigger = true;
            if (verboseTriggerDebug)
                Debug.Log("CloudAscend: valid target exited trigger, retrigger allowed.");
        }
    }

    [ContextMenu("Trigger Now")]
    public void TriggerNow()
    {
        StartSequence("Manual");
    }

    [ContextMenu("Auto Setup / 5 Square Clouds From Root")]
    public void AutoSetupFiveSquareClouds()
    {
        AutoFindCloudPiecesFromRoot();
        ApplyPresetLayout(CloudLayoutPreset.FiveSquare1920);
        HideCloudPiecesImmediate();
    }

    [ContextMenu("Auto Setup / 6 Square Clouds From Root")]
    public void AutoSetupSixSquareClouds()
    {
        AutoFindCloudPiecesFromRoot();
        ApplyPresetLayout(CloudLayoutPreset.SixSquare1920);
        HideCloudPiecesImmediate();
    }

    [ContextMenu("Auto Setup / 8 Square Clouds From Root")]
    public void AutoSetupEightSquareClouds()
    {
        AutoFindCloudPiecesFromRoot();
        ApplyPresetLayout(CloudLayoutPreset.EightSquare1920);
        HideCloudPiecesImmediate();
    }

    [ContextMenu("Auto Find Cloud Pieces From Root")]
    public void AutoFindCloudPiecesFromRoot()
    {
        RectTransform searchRoot = cloudPiecesRoot;

        if (searchRoot == null && cloudCanvas != null)
            searchRoot = cloudCanvas.transform as RectTransform;

        if (searchRoot == null)
            searchRoot = transform as RectTransform;

        if (searchRoot == null)
        {
            Debug.LogWarning("CloudAscend: Cannot auto-find cloud pieces because no RectTransform root was found.");
            return;
        }

        Image[] images = searchRoot.GetComponentsInChildren<Image>(true);
        cloudPieces = new List<CloudPiece>();

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];

            if (image == null)
                continue;

            RectTransform rect = image.rectTransform;

            if (rect == searchRoot)
                continue;

            CloudPiece piece = new CloudPiece();
            piece.rect = rect;
            piece.canvasGroup = rect.GetComponent<CanvasGroup>();
            if (piece.canvasGroup == null)
                piece.canvasGroup = rect.gameObject.AddComponent<CanvasGroup>();

            piece.alpha = 1f;
            piece.scale = Vector3.one;
            piece.size = autoPieceSize;
            piece.applySize = true;
            piece.wobbleAmplitude = 10f;
            piece.wobbleFrequency = 0.8f + i * 0.13f;

            cloudPieces.Add(piece);
        }

        Debug.Log("CloudAscend: auto-found " + cloudPieces.Count + " cloud pieces under " + searchRoot.name + ".");
    }

    [ContextMenu("Test Show All Cloud Pieces")]
    public void TestShowAllCloudPieces()
    {
        EnsureAllCloudPieceReferences();
        ForceCloudCanvasOnTop();

        if (cloudPieces == null)
            return;

        for (int i = 0; i < cloudPieces.Count; i++)
        {
            CloudPiece piece = cloudPieces[i];
            if (piece == null || piece.rect == null)
                continue;

            EnsureCloudPieceCanvasGroup(piece);
            PrepareCloudPieceVisual(piece);

            piece.rect.gameObject.SetActive(true);
            piece.rect.anchoredPosition = new Vector2(piece.xOffset, piece.coverY);
            piece.rect.localScale = piece.scale;

            if (piece.canvasGroup != null)
                piece.canvasGroup.alpha = GetSafePieceAlpha(piece);
        }

        Debug.Log("CloudAscend: test showed all cloud pieces.");
    }

    [ContextMenu("Hide All Cloud Pieces")]
    public void HideAllCloudPiecesContext()
    {
        HideCloudPiecesImmediate();
    }

    [ContextMenu("Debug Canvas Stack")]
    public void DebugCanvasStack()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            Debug.Log(
                "Canvas: " + canvas.name +
                ", renderMode=" + canvas.renderMode +
                ", override=" + canvas.overrideSorting +
                ", order=" + canvas.sortingOrder +
                ", active=" + canvas.gameObject.activeInHierarchy
            );
        }
    }

    void TryStartFromCollider(Collider other, string source)
    {
        if (isRunning || (triggerOnce && hasTriggered))
            return;

        if (requireExitBeforeRetrigger && !hasExitedAfterTrigger)
        {
            if (verboseTriggerDebug)
                Debug.Log("CloudAscend: rejected " + source + " because trigger exit is required before retrigger.");

            return;
        }

        if (verboseTriggerDebug && other != null)
        {
            Debug.Log("CloudAscend: " + source + " by " + other.name + ", tag=" + other.tag + ", layer=" + LayerMask.LayerToName(other.gameObject.layer));
        }

        if (!IsValidTrigger(other, source))
            return;

        StartSequence(source);
    }

    bool IsValidTrigger(Collider other, string source)
    {
        if (other == null)
            return false;

        if (!string.IsNullOrEmpty(requiredTag))
        {
            Transform root = other.transform.root;
            bool tagMatches = other.CompareTag(requiredTag) || (root != null && root.CompareTag(requiredTag));

            if (!tagMatches)
            {
                if (verboseTriggerDebug)
                {
                    string rootTag = root != null ? root.tag : "none";
                    Debug.Log("CloudAscend: rejected " + source + " because tag did not match requiredTag=" + requiredTag + ". otherTag=" + other.tag + ", rootTag=" + rootTag);
                }

                return false;
            }
        }

        if (useLayerMask)
        {
            Transform root = other.transform.root;
            bool selfLayerMatches = (requiredLayerMask.value & (1 << other.gameObject.layer)) != 0;
            bool rootLayerMatches = root != null && (requiredLayerMask.value & (1 << root.gameObject.layer)) != 0;

            if (!selfLayerMatches && !rootLayerMatches)
            {
                if (verboseTriggerDebug)
                {
                    Debug.Log("CloudAscend: rejected " + source + " because layer did not match. otherLayer=" + LayerMask.LayerToName(other.gameObject.layer));
                }

                return false;
            }
        }

        return true;
    }

    void StartSequence(string source)
    {
        if (isRunning)
            return;

        if (triggerOnce && hasTriggered)
            return;

        if (Time.time < lastTriggerTime + retriggerCooldown)
        {
            if (verboseTriggerDebug)
                Debug.Log("CloudAscend: rejected " + source + " because retrigger cooldown is active.");

            return;
        }

        if (requireExitBeforeRetrigger && !hasExitedAfterTrigger)
        {
            if (verboseTriggerDebug)
                Debug.Log("CloudAscend: rejected " + source + " because trigger exit is required before retrigger.");

            return;
        }

        if (requireMovieCameraActive && movieCamera != null && !movieCamera.gameObject.activeInHierarchy)
        {
            if (verboseTriggerDebug)
                Debug.Log("CloudAscend: rejected " + source + " because MovieCamera is not active.");

            return;
        }

        if (requirePlayerControlEnabled && interactionManager != null && !interactionManager.canInteract)
        {
            if (verboseTriggerDebug)
                Debug.Log("CloudAscend: rejected " + source + " because player interaction is not enabled.");

            return;
        }

        Debug.Log("CloudAscend: triggered by " + source);

        hasExitedAfterTrigger = false;
        hasTriggered = true;
        isRunning = true;
        lastTriggerTime = Time.time;

        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        Debug.Log("CloudAscend: started");

        if (interactionManager != null)
            interactionManager.canInteract = false;

        if (train != null)
            train.DisableDrive();

        DisableExternalBehaviours();
        ForceCloudCanvasOnTop();
        PrepareCloudPieces();

        Transform cameraTarget = GetCameraMoveTarget();
        Vector3 cameraStart = cameraTarget != null ? cameraTarget.position : Vector3.zero;
        Vector3 cameraEnd = cameraStart + cameraWorldOffset;

        Vector3 platformStart = platformRoot != null ? platformRoot.position : Vector3.zero;
        Vector3 platformEnd = platformStart;
        Vector3 platformDelta = Vector3.zero;

        if (platformRoot != null)
        {
            platformEnd = movePlatformByOffset ? platformStart + platformTargetPositionOrOffset : platformTargetPositionOrOffset;
            platformDelta = platformEnd - platformStart;
        }

        bool moveTrainRoot = alsoMoveTrain && trainRootToMove != null;
        Vector3 trainStart = moveTrainRoot ? trainRootToMove.position : Vector3.zero;
        Vector3 trainEnd = moveTrainRoot ? trainStart + platformDelta : Vector3.zero;

        bool liftFinished = false;
        StartCoroutine(LiftRoutine(cameraTarget, cameraStart, cameraEnd, platformStart, platformEnd, moveTrainRoot, trainStart, trainEnd, () => liftFinished = true));

        Debug.Log("CloudAscend: cloud pieces enter started");
        yield return PlayCloudEnter();

        if (snapCoveredStateAfterEnter)
            HoldCloudCovered();

        Debug.Log("CloudAscend: cloud pieces fully covered screen");

        float exitStartProgress = continueLiftDuringCloudExit ? Mathf.Clamp01(liftProgressWhenCloudExitStarts) : 1f;

        while (!liftFinished && currentLiftProgress < exitStartProgress)
        {
            ForceCloudCanvasOnTop();

            if (snapCoveredStateAfterEnter)
                HoldCloudCovered();

            yield return null;
        }

        if (liftFinished)
            Debug.Log("CloudAscend: lift reached target position before cloud exit");
        else
            Debug.Log("CloudAscend: cloud pieces exit started while lift is still moving. liftProgress=" + currentLiftProgress.ToString("0.00"));

        Debug.Log("CloudAscend: cloud pieces exit started");

        yield return PlayCloudExit();

        if (waitForLiftAfterCloudExit)
        {
            while (!liftFinished)
            {
                ForceCloudCanvasOnTop();
                yield return null;
            }
        }

        if (liftFinished)
            Debug.Log("CloudAscend: lift reached target position");

        HideCloudPiecesImmediate();
        RestoreExternalBehaviours();

        if (train != null)
            train.EnableDrive();

        if (interactionManager != null)
            interactionManager.canInteract = true;

        isRunning = false;
        Debug.Log("CloudAscend: finished, player control restored");
    }

    IEnumerator LiftRoutine(
        Transform cameraTarget,
        Vector3 cameraStart,
        Vector3 cameraEnd,
        Vector3 platformStart,
        Vector3 platformEnd,
        bool moveTrainRoot,
        Vector3 trainStart,
        Vector3 trainEnd,
        System.Action onComplete)
    {
        float duration = Mathf.Max(0.0001f, liftDuration);
        float elapsed = 0f;

        currentLiftProgress = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            currentLiftProgress = t;

            ApplyLiftPose(t, cameraTarget, cameraStart, cameraEnd, platformStart, platformEnd, moveTrainRoot, trainStart, trainEnd);

            yield return null;
        }

        currentLiftProgress = 1f;
        ApplyLiftPose(1f, cameraTarget, cameraStart, cameraEnd, platformStart, platformEnd, moveTrainRoot, trainStart, trainEnd);
        onComplete?.Invoke();
    }

    void PrepareCloudPieces()
    {
        EnsureAllCloudPieceReferences();
        ForceCloudCanvasOnTop();

        if (cloudPieces == null)
            return;

        for (int i = 0; i < cloudPieces.Count; i++)
        {
            CloudPiece piece = cloudPieces[i];

            if (piece == null || piece.rect == null)
                continue;

            EnsureCloudPieceCanvasGroup(piece);
            PrepareCloudPieceVisual(piece);

            piece.rect.gameObject.SetActive(true);
            piece.rect.localScale = piece.scale;
            piece.rect.anchoredPosition = GetCloudPiecePosition(piece, piece.startY, Time.time);

            if (piece.canvasGroup != null)
                piece.canvasGroup.alpha = 0f;

            if (logCloudPieceState)
                LogCloudPieceState(piece, "Prepare");
        }
    }

    IEnumerator PlayCloudEnter()
    {
        float duration = Mathf.Max(0.0001f, cloudEnterDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ForceCloudCanvasOnTop();

            elapsed += Time.deltaTime;

            if (cloudPieces != null)
            {
                for (int i = 0; i < cloudPieces.Count; i++)
                {
                    CloudPiece piece = cloudPieces[i];

                    if (piece == null || piece.rect == null)
                        continue;

                    EnsureCloudPieceCanvasGroup(piece);

                    float availableDuration = Mathf.Max(0.0001f, duration - Mathf.Max(0f, piece.enterDelay));
                    float localT = Mathf.Clamp01((elapsed - piece.enterDelay) / availableDuration);
                    float y = Mathf.LerpUnclamped(piece.startY, piece.coverY, EvaluateCloudPieceMoveCurve(localT));

                    piece.rect.localScale = piece.scale;
                    piece.rect.anchoredPosition = GetCloudPiecePosition(piece, y, Time.time);

                    if (piece.canvasGroup != null)
                        piece.canvasGroup.alpha = Mathf.Lerp(0f, GetSafePieceAlpha(piece), localT);
                }
            }

            yield return null;
        }

        HoldCloudCovered();
    }

    void HoldCloudCovered()
    {
        if (cloudPieces == null)
            return;

        for (int i = 0; i < cloudPieces.Count; i++)
        {
            CloudPiece piece = cloudPieces[i];

            if (piece == null || piece.rect == null)
                continue;

            EnsureCloudPieceCanvasGroup(piece);
            PrepareCloudPieceVisual(piece);

            piece.rect.gameObject.SetActive(true);
            piece.rect.localScale = piece.scale;
            piece.rect.anchoredPosition = GetCloudPiecePosition(piece, piece.coverY, Time.time);

            if (piece.canvasGroup != null)
                piece.canvasGroup.alpha = GetSafePieceAlpha(piece);
        }
    }

    IEnumerator PlayCloudExit()
    {
        float duration = Mathf.Max(0.0001f, cloudExitDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            ForceCloudCanvasOnTop();

            elapsed += Time.deltaTime;

            if (cloudPieces != null)
            {
                for (int i = 0; i < cloudPieces.Count; i++)
                {
                    CloudPiece piece = cloudPieces[i];

                    if (piece == null || piece.rect == null)
                        continue;

                    EnsureCloudPieceCanvasGroup(piece);

                    float availableDuration = Mathf.Max(0.0001f, duration - Mathf.Max(0f, piece.exitDelay));
                    float localT = Mathf.Clamp01((elapsed - piece.exitDelay) / availableDuration);
                    float y = Mathf.LerpUnclamped(piece.coverY, piece.exitY, EvaluateCloudPieceMoveCurve(localT));

                    piece.rect.localScale = piece.scale;
                    piece.rect.anchoredPosition = GetCloudPiecePosition(piece, y, Time.time);

                    if (piece.canvasGroup != null)
                        piece.canvasGroup.alpha = Mathf.Lerp(GetSafePieceAlpha(piece), 0f, localT);
                }
            }

            yield return null;
        }

        HideCloudPiecesImmediate();
    }

    void HideCloudPiecesImmediate()
    {
        if (cloudPieces == null)
            return;

        for (int i = 0; i < cloudPieces.Count; i++)
        {
            CloudPiece piece = cloudPieces[i];

            if (piece == null || piece.rect == null)
                continue;

            EnsureCloudPieceCanvasGroup(piece);

            if (piece.canvasGroup != null)
                piece.canvasGroup.alpha = 0f;

            piece.rect.gameObject.SetActive(false);
        }
    }

    void EnsureAllCloudPieceReferences()
    {
        if (cloudPieces == null)
            return;

        for (int i = 0; i < cloudPieces.Count; i++)
        {
            CloudPiece piece = cloudPieces[i];

            if (piece == null || piece.rect == null)
                continue;

            EnsureCloudPieceCanvasGroup(piece);
            PrepareCloudPieceVisual(piece);
        }
    }

    void EnsureCloudPieceCanvasGroup(CloudPiece piece)
    {
        if (piece == null || piece.rect == null || piece.canvasGroup != null)
            return;

        piece.canvasGroup = piece.rect.GetComponent<CanvasGroup>();

        if (piece.canvasGroup == null)
            piece.canvasGroup = piece.rect.gameObject.AddComponent<CanvasGroup>();
    }

    void PrepareCloudPieceVisual(CloudPiece piece)
    {
        if (piece == null || piece.rect == null)
            return;

        if (piece.applySize)
            piece.rect.sizeDelta = piece.size;

        Image image = piece.rect.GetComponent<Image>();

        if (image != null && forceImageAlphaToOneOnPrepare)
        {
            Color c = image.color;
            c.a = 1f;
            image.color = c;
        }

        if (piece.alpha <= 0.001f && autoFixZeroAlphaToOne)
            piece.alpha = 1f;
    }

    float GetSafePieceAlpha(CloudPiece piece)
    {
        if (piece == null)
            return 1f;

        if (piece.alpha <= 0.001f && autoFixZeroAlphaToOne)
            return 1f;

        return Mathf.Clamp01(piece.alpha);
    }

    Vector2 GetCloudPiecePosition(CloudPiece piece, float y, float time)
    {
        float wobble = Mathf.Sin(time * piece.wobbleFrequency) * piece.wobbleAmplitude;
        return new Vector2(piece.xOffset + wobble, y);
    }

    void ApplyLiftPose(
        float rawLiftT,
        Transform cameraTarget,
        Vector3 cameraStart,
        Vector3 cameraEnd,
        Vector3 platformStart,
        Vector3 platformEnd,
        bool moveTrainRoot,
        Vector3 trainStart,
        Vector3 trainEnd)
    {
        float liftT = EvaluateLiftCurve(Mathf.Clamp01(rawLiftT));

        if (cameraTarget != null)
            cameraTarget.position = Vector3.LerpUnclamped(cameraStart, cameraEnd, liftT);

        if (platformRoot != null)
            platformRoot.position = Vector3.LerpUnclamped(platformStart, platformEnd, liftT);

        if (moveTrainRoot && trainRootToMove != null)
            trainRootToMove.position = Vector3.LerpUnclamped(trainStart, trainEnd, liftT);
    }

    void DisableExternalBehaviours()
    {
        if (!disableBehavioursDuringSequence || behavioursToDisableDuringSequence == null)
            return;

        disabledBehaviourOriginalStates = new bool[behavioursToDisableDuringSequence.Length];

        for (int i = 0; i < behavioursToDisableDuringSequence.Length; i++)
        {
            Behaviour behaviour = behavioursToDisableDuringSequence[i];

            if (behaviour == null)
                continue;

            disabledBehaviourOriginalStates[i] = behaviour.enabled;

            if (behaviour.enabled)
            {
                behaviour.enabled = false;
                Debug.Log("CloudAscend: disabled external behaviour: " + behaviour.name);
            }
        }
    }

    void RestoreExternalBehaviours()
    {
        if (!disableBehavioursDuringSequence || behavioursToDisableDuringSequence == null || disabledBehaviourOriginalStates == null)
            return;

        for (int i = 0; i < behavioursToDisableDuringSequence.Length; i++)
        {
            Behaviour behaviour = behavioursToDisableDuringSequence[i];

            if (behaviour == null)
                continue;

            behaviour.enabled = disabledBehaviourOriginalStates[i];
            Debug.Log("CloudAscend: restored external behaviour: " + behaviour.name);
        }
    }

    void SetupTriggerColliderAndRigidbody()
    {
        Collider triggerCollider = GetComponent<Collider>();

        if (triggerCollider == null)
        {
            Debug.LogWarning("CloudAscend: trigger object has no Collider.");
        }
        else if (!triggerCollider.isTrigger)
        {
            Debug.LogWarning("CloudAscend: Collider is not marked as Is Trigger.");
        }

        if (ensureKinematicRigidbodyOnTrigger)
        {
            Rigidbody rb = GetComponent<Rigidbody>();

            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void ResolveCloudCanvas()
    {
        if (cloudCanvas != null)
            return;

        if (cloudPiecesRoot != null)
            cloudCanvas = cloudPiecesRoot.GetComponentInParent<Canvas>();

        if (cloudCanvas == null && cloudPieces != null)
        {
            for (int i = 0; i < cloudPieces.Count; i++)
            {
                CloudPiece piece = cloudPieces[i];

                if (piece != null && piece.rect != null)
                {
                    cloudCanvas = piece.rect.GetComponentInParent<Canvas>();

                    if (cloudCanvas != null)
                        break;
                }
            }
        }
    }

    void ForceCloudCanvasOnTop()
    {
        if (!forceCloudCanvasOnTop)
            return;

        ResolveCloudCanvas();

        if (cloudCanvas == null)
            return;

        cloudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cloudCanvas.overrideSorting = true;
        cloudCanvas.sortingOrder = cloudCanvasSortingOrder;

        if (forceSetAsLastSibling)
            cloudCanvas.transform.SetAsLastSibling();

        if (forceParentCanvasGroupsVisible)
        {
            CanvasGroup[] groups = cloudCanvas.GetComponentsInParent<CanvasGroup>(true);

            for (int i = 0; i < groups.Length; i++)
                groups[i].alpha = 1f;
        }
    }

    void ApplyPresetLayout(CloudLayoutPreset preset)
    {
        if (cloudPieces == null || cloudPieces.Count == 0)
        {
            Debug.LogWarning("CloudAscend: no cloud pieces to layout.");
            return;
        }

        switch (preset)
        {
            case CloudLayoutPreset.FiveSquare1920:
                ApplyFiveSquareLayout();
                break;

            case CloudLayoutPreset.SixSquare1920:
                ApplySixSquareLayout();
                break;

            case CloudLayoutPreset.EightSquare1920:
                ApplyEightSquareLayout();
                break;
        }

        Debug.Log("CloudAscend: applied " + preset + " layout to " + cloudPieces.Count + " pieces.");
    }

    void ApplyFiveSquareLayout()
    {
        float[,] values =
        {
            { -650f, 1500f, 420f, -1500f, 0f, 0.05f, 1.00f, 1.00f },
            {  650f, 1550f, 420f, -1550f, 0.08f, 0.12f, 1.00f, 1.00f },
            {    0f, 1450f,   0f, -1450f, 0.14f, 0.00f, 1.15f, 1.15f },
            { -600f, 1600f,-420f, -1600f, 0.20f, 0.16f, 1.00f, 1.00f },
            {  600f, 1520f,-420f, -1520f, 0.26f, 0.08f, 1.00f, 1.00f },
        };

        ApplyLayoutValues(values);
    }

    void ApplySixSquareLayout()
    {
        float[,] values =
        {
            { -650f, 1500f, 420f, -1500f, 0f, 0.05f, 1.00f, 1.00f },
            {  650f, 1550f, 420f, -1550f, 0.08f, 0.12f, 1.00f, 1.00f },
            {    0f, 1450f,   0f, -1450f, 0.14f, 0.00f, 1.15f, 1.15f },
            { -600f, 1600f,-420f, -1600f, 0.20f, 0.16f, 1.00f, 1.00f },
            {  600f, 1520f,-420f, -1520f, 0.26f, 0.08f, 1.00f, 1.00f },
            {    0f, 1650f,-650f, -1650f, 0.18f, 0.20f, 1.00f, 1.00f },
        };

        ApplyLayoutValues(values);
    }

    void ApplyEightSquareLayout()
    {
        float[,] values =
        {
            { -800f, 1500f, 420f, -1500f, 0f, 0.05f, 1.25f, 1.25f },
            {    0f, 1600f, 380f, -1600f, 0.06f, 0.00f, 1.35f, 1.35f },
            {  820f, 1520f, 430f, -1520f, 0.12f, 0.10f, 1.20f, 1.20f },
            { -600f, 1450f,  80f, -1450f, 0.10f, 0.18f, 1.35f, 1.35f },
            {  280f, 1480f,  20f, -1480f, 0.16f, 0.12f, 1.45f, 1.45f },
            {  900f, 1420f, -60f, -1420f, 0.22f, 0.20f, 1.25f, 1.25f },
            { -350f, 1550f,-390f, -1550f, 0.20f, 0.08f, 1.45f, 1.45f },
            {  560f, 1580f,-420f, -1580f, 0.28f, 0.15f, 1.35f, 1.35f },
        };

        ApplyLayoutValues(values);
    }

    void ApplyLayoutValues(float[,] values)
    {
        int count = Mathf.Min(cloudPieces.Count, values.GetLength(0));

        for (int i = 0; i < count; i++)
        {
            CloudPiece piece = cloudPieces[i];

            if (piece == null)
                continue;

            piece.xOffset = values[i, 0];
            piece.startY = values[i, 1];
            piece.coverY = values[i, 2];
            piece.exitY = values[i, 3];
            piece.enterDelay = values[i, 4];
            piece.exitDelay = values[i, 5];
            piece.scale = new Vector3(values[i, 6], values[i, 7], 1f);
            piece.alpha = 1f;
            piece.size = autoPieceSize;
            piece.applySize = true;
            piece.wobbleAmplitude = 10f + i * 1.5f;
            piece.wobbleFrequency = 0.6f + i * 0.1f;
        }
    }

    void LogCloudPieceState(CloudPiece piece, string phase)
    {
        if (piece == null || piece.rect == null)
            return;

        Image image = piece.rect.GetComponent<Image>();

        string spriteName = image != null && image.sprite != null ? image.sprite.name : "null";
        float imageAlpha = image != null ? image.color.a : -1f;
        float groupAlpha = piece.canvasGroup != null ? piece.canvasGroup.alpha : -1f;

        Debug.Log(
            "CloudAscend " + phase + ": " + piece.rect.name +
            ", active=" + piece.rect.gameObject.activeInHierarchy +
            ", pos=" + piece.rect.anchoredPosition +
            ", size=" + piece.rect.sizeDelta +
            ", scale=" + piece.rect.localScale +
            ", sprite=" + spriteName +
            ", imageAlpha=" + imageAlpha +
            ", groupAlpha=" + groupAlpha +
            ", targetAlpha=" + piece.alpha
        );
    }

    Transform GetCameraMoveTarget()
    {
        if (cameraMoveRoot != null)
            return cameraMoveRoot;

        if (movieCamera != null)
            return movieCamera.transform;

        Debug.LogWarning("CloudAscend: no movieCamera or cameraMoveRoot assigned; camera will not move.");
        return null;
    }

    float EvaluateLiftCurve(float t)
    {
        if (liftCurve == null || liftCurve.length == 0)
            return Mathf.SmoothStep(0f, 1f, t);

        return liftCurve.Evaluate(t);
    }

    float EvaluateCloudPieceMoveCurve(float t)
    {
        if (cloudPieceMoveCurve == null || cloudPieceMoveCurve.length == 0)
            return Mathf.SmoothStep(0f, 1f, t);

        return cloudPieceMoveCurve.Evaluate(t);
    }
}
