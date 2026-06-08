using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class VoidTrainFinaleSequence : MonoBehaviour
{
    [Header("Trigger")]
    public bool triggerOnce = true;
    public string requiredTag = "";
    public bool useDistanceFallback = true;
    public float fallbackTriggerDistance = 1.0f;
    public bool verboseDebug = true;

    [Header("Gameplay References")]
    public TrainOnRails train;
    public Transform trainRoot;
    public InteractionManager interactionManager;

    [Header("Finale Lead In")]
    [Tooltip("触发后先聚焦一小段时间，再让火车真正飞出去。可以减少突然切过去的感觉。")]
    public float leadInDuration = 0.6f;

    [Header("Flight Path")]
    [Tooltip("火车凭空飞行的路点。按顺序拖 Empty。脚本会自动把火车当前触发位置作为真正起点。")]
    public Transform[] flightPoints;

    [Tooltip("通常不要勾。勾上会把火车吸到第一个飞行点，可能产生突然切过去的感觉。")]
    public bool snapTrainToFirstPoint = false;

    public float flightDuration = 4.0f;
    public AnimationCurve flightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool rotateTrainAlongPath = true;
    public Vector3 extraTrainEulerOffset = Vector3.zero;

    [Header("Tail Effect")]
    [Tooltip("挂在火车尾部的星光/风痕粒子。")]
    public ParticleSystem[] tailParticles;

    public GameObject tailEffectRoot;
    public bool enableTailEffectObject = true;
    public float stopTailDelayAfterFlash = 0.2f;

    [Header("Letterbox")]
    public RectTransform topBar;
    public RectTransform bottomBar;
    public float letterboxHeight = 140f;
    public float letterboxEnterDuration = 0.8f;
    public AnimationCurve letterboxCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Camera Follow")]
    public Camera movieCamera;

    [Tooltip("勾上后，触发瞬间记录 MovieCamera 到火车的偏移，然后飞行时保持这个偏移跟随。")]
    public bool useCurrentCameraOffset = true;

    [Tooltip("不使用当前偏移时，使用这个世界坐标偏移跟随火车。")]
    public Vector3 cameraFollowOffset = new Vector3(0f, 4f, -6f);

    public bool lookAtTrain = true;
    public Transform cameraLookTarget;

    [Tooltip("相机跟随平滑。数值越大越紧。想慢一点就调低。")]
    public float cameraFollowSmooth = 2.2f;

    [Header("OP Style Focus")]
    [Tooltip("像 OpeningTrainCinematicSequence 一样，看向火车前方一点，而不是死盯火车中心。")]
    public bool useLookAheadFocus = true;

    [Tooltip("镜头看向火车前方的距离。数值越大，镜头越像在看火车要去的方向。")]
    public float lookAheadDistance = 0.8f;

    [Tooltip("相机旋转平滑。想慢一点就调低。")]
    public float cameraRotationSmooth = 3.5f;

    [Tooltip("相机缩放平滑。想让镜头推进慢一点就调低。")]
    public float cameraZoomSmooth = 1.2f;

    [Header("Camera Zoom")]
    public bool zoomDuringFinale = true;
    public float focusOrthographicSize = 4.2f;
    public float focusFieldOfView = 35f;

    [Header("White Flash")]
    public CanvasGroup whiteFlashCanvasGroup;

    [Tooltip("飞行进度到多少时开始白屏。0.62 表示火车还剩 38% 路程时就开始白屏。")]
    [Range(0.05f, 0.98f)]
    public float whiteFlashStartProgress = 0.62f;

    [Tooltip("勾上后，白屏会在火车飞行途中提前开始，而不是等火车飞完。")]
    public bool startWhiteFlashBeforeFlightEnds = true;

    public float whiteFlashDuration = 0.8f;
    public AnimationCurve whiteFlashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Finish")]
    public UnityEvent onFinaleFinished;

    private bool hasTriggered;
    private bool isPlaying;

    private bool whiteFlashStarted;
    private bool whiteFlashFinished;

    private Vector3 runtimeCameraOffset;
    private float originalOrthoSize;
    private float originalFieldOfView;

    private void Awake()
    {
        if (trainRoot == null && train != null)
            trainRoot = train.transform;

        if (movieCamera != null)
        {
            originalOrthoSize = movieCamera.orthographicSize;
            originalFieldOfView = movieCamera.fieldOfView;
        }

        SetLetterboxHeight(0f);

        if (whiteFlashCanvasGroup != null)
            whiteFlashCanvasGroup.alpha = 0f;

        if (tailEffectRoot != null && enableTailEffectObject)
            tailEffectRoot.SetActive(false);
    }

    private void Update()
    {
        if (!useDistanceFallback)
            return;

        if (hasTriggered || isPlaying)
            return;

        if (trainRoot == null)
            return;

        float distance = Vector3.Distance(trainRoot.position, transform.position);

        if (distance <= fallbackTriggerDistance)
            StartFinale("DistanceFallback");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || isPlaying)
            return;

        if (!IsValidTrigger(other))
            return;

        StartFinale("OnTriggerEnter");
    }

    private void OnTriggerStay(Collider other)
    {
        if (hasTriggered || isPlaying)
            return;

        if (!IsValidTrigger(other))
            return;

        StartFinale("OnTriggerStay");
    }

    private bool IsValidTrigger(Collider other)
    {
        if (other == null)
            return false;

        if (string.IsNullOrEmpty(requiredTag))
            return true;

        bool selfMatch = other.CompareTag(requiredTag);
        bool rootMatch = other.transform.root != null && other.transform.root.CompareTag(requiredTag);

        return selfMatch || rootMatch;
    }

    public void StartFinale(string source)
    {
        if (triggerOnce && hasTriggered)
            return;

        if (isPlaying)
            return;

        hasTriggered = true;
        StartCoroutine(PlayFinale(source));
    }

    [ContextMenu("Test Play Finale")]
    public void TestPlayFinale()
    {
        StartFinale("Manual Test");
    }

    private IEnumerator PlayFinale(string source)
    {
        isPlaying = true;

        whiteFlashStarted = false;
        whiteFlashFinished = false;

        if (whiteFlashCanvasGroup != null)
            whiteFlashCanvasGroup.alpha = 0f;

        if (verboseDebug)
            Debug.Log("VoidTrainFinale: triggered by " + source);

        if (train != null)
            train.DisableDrive();

        if (interactionManager != null)
            interactionManager.canInteract = false;

        // 先记录当前相机偏移，再决定是否 snap。
        // 默认不 snap，避免触发瞬间硬切。
        if (movieCamera != null && trainRoot != null)
        {
            runtimeCameraOffset = useCurrentCameraOffset
                ? movieCamera.transform.position - trainRoot.position
                : cameraFollowOffset;

            originalOrthoSize = movieCamera.orthographicSize;
            originalFieldOfView = movieCamera.fieldOfView;
        }

        if (snapTrainToFirstPoint)
            SnapTrainToFirstPoint();

        StartTailEffects();

        Coroutine letterboxRoutine = StartCoroutine(EnterLetterbox());

        if (leadInDuration > 0f)
        {
            if (verboseDebug)
                Debug.Log("VoidTrainFinale: lead in started");

            float elapsed = 0f;

            while (elapsed < leadInDuration)
            {
                elapsed += Time.deltaTime;

                Vector3 forward = trainRoot != null ? trainRoot.forward : Vector3.forward;
                UpdateCameraFollow(0f, forward);

                yield return null;
            }
        }

        if (verboseDebug)
            Debug.Log("VoidTrainFinale: flight started");

        yield return MoveTrainThroughVoid();

        if (letterboxRoutine != null)
            yield return letterboxRoutine;

        if (!whiteFlashStarted)
        {
            yield return WhiteFlashTracked();
        }
        else
        {
            while (!whiteFlashFinished)
                yield return null;
        }

        yield return new WaitForSeconds(stopTailDelayAfterFlash);

        StopTailEffects();

        if (verboseDebug)
            Debug.Log("VoidTrainFinale: finished");

        onFinaleFinished?.Invoke();

        isPlaying = false;
    }

    private void StartTailEffects()
    {
        if (tailEffectRoot != null && enableTailEffectObject)
            tailEffectRoot.SetActive(true);

        if (tailParticles == null)
            return;

        foreach (ParticleSystem ps in tailParticles)
        {
            if (ps == null)
                continue;

            ps.Clear(true);
            ps.Play(true);
        }
    }

    private void StopTailEffects()
    {
        if (tailParticles != null)
        {
            foreach (ParticleSystem ps in tailParticles)
            {
                if (ps == null)
                    continue;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (tailEffectRoot != null && enableTailEffectObject)
            tailEffectRoot.SetActive(false);
    }

    private IEnumerator EnterLetterbox()
    {
        float duration = Mathf.Max(0.0001f, letterboxEnterDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = letterboxCurve.Evaluate(rawT);

            SetLetterboxHeight(Mathf.Lerp(0f, letterboxHeight, t));

            yield return null;
        }

        SetLetterboxHeight(letterboxHeight);
    }

    private void SetLetterboxHeight(float height)
    {
        if (topBar != null)
        {
            Vector2 size = topBar.sizeDelta;
            size.y = height;
            topBar.sizeDelta = size;
        }

        if (bottomBar != null)
        {
            Vector2 size = bottomBar.sizeDelta;
            size.y = height;
            bottomBar.sizeDelta = size;
        }
    }

    private void SnapTrainToFirstPoint()
    {
        if (trainRoot == null)
            return;

        if (flightPoints == null || flightPoints.Length == 0 || flightPoints[0] == null)
            return;

        trainRoot.position = flightPoints[0].position;

        if (rotateTrainAlongPath && flightPoints.Length > 1 && flightPoints[1] != null)
        {
            Vector3 dir = flightPoints[1].position - flightPoints[0].position;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                trainRoot.rotation = lookRot * Quaternion.Euler(extraTrainEulerOffset);
            }
        }
    }

    private IEnumerator MoveTrainThroughVoid()
    {
        if (trainRoot == null)
            yield break;

        List<Vector3> points = BuildValidPointList();

        if (points.Count < 2)
        {
            if (verboseDebug)
                Debug.LogWarning("VoidTrainFinale: Need at least 2 flight points.");

            yield break;
        }

        float[] segmentLengths = new float[points.Count - 1];
        float totalLength = 0f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            segmentLengths[i] = Vector3.Distance(points[i], points[i + 1]);
            totalLength += segmentLengths[i];
        }

        if (totalLength <= 0.0001f)
            yield break;

        float duration = Mathf.Max(0.0001f, flightDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float rawT = Mathf.Clamp01(elapsed / duration);
            float moveT = flightCurve.Evaluate(rawT);

            float targetDistance = totalLength * moveT;
            EvaluatePath(points, segmentLengths, targetDistance, out Vector3 pos, out Vector3 forward);

            trainRoot.position = pos;

            if (rotateTrainAlongPath && forward.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
                trainRoot.rotation = lookRot * Quaternion.Euler(extraTrainEulerOffset);
            }

            UpdateCameraFollow(rawT, forward);
            TryStartWhiteFlashDuringFlight(rawT);

            yield return null;
        }

        Vector3 lastForward = points[points.Count - 1] - points[points.Count - 2];

        trainRoot.position = points[points.Count - 1];

        if (rotateTrainAlongPath && lastForward.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(lastForward.normalized, Vector3.up);
            trainRoot.rotation = lookRot * Quaternion.Euler(extraTrainEulerOffset);
        }

        UpdateCameraFollow(1f, lastForward);
    }

    private List<Vector3> BuildValidPointList()
    {
        List<Vector3> points = new List<Vector3>();

        // 关键改动：
        // 不再把 FlightPoint_01 当作硬起点。
        // 火车当前真实位置才是 finale 飞行的起点。
        if (trainRoot != null)
            points.Add(trainRoot.position);

        if (flightPoints == null)
            return points;

        foreach (Transform p in flightPoints)
        {
            if (p != null)
                points.Add(p.position);
        }

        return points;
    }

    private void EvaluatePath(
        List<Vector3> points,
        float[] segmentLengths,
        float distance,
        out Vector3 position,
        out Vector3 forward)
    {
        float remaining = distance;

        for (int i = 0; i < segmentLengths.Length; i++)
        {
            float length = segmentLengths[i];

            if (remaining <= length)
            {
                float t = length <= 0.0001f ? 0f : remaining / length;

                position = Vector3.Lerp(points[i], points[i + 1], t);
                forward = points[i + 1] - points[i];
                return;
            }

            remaining -= length;
        }

        position = points[points.Count - 1];
        forward = points[points.Count - 1] - points[points.Count - 2];
    }

    private void UpdateCameraFollow(float rawProgress, Vector3 pathForward)
    {
        if (movieCamera == null || trainRoot == null)
            return;

        Transform cam = movieCamera.transform;

        Vector3 targetPos = trainRoot.position + runtimeCameraOffset;

        cam.position = Vector3.Lerp(
            cam.position,
            targetPos,
            Mathf.Clamp01(Time.deltaTime * cameraFollowSmooth)
        );

        Vector3 lookPoint = GetFinaleCameraLookPoint(pathForward);

        if (lookAtTrain)
        {
            Vector3 dir = lookPoint - cam.position;

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);

                cam.rotation = Quaternion.Slerp(
                    cam.rotation,
                    targetRot,
                    Mathf.Clamp01(Time.deltaTime * cameraRotationSmooth)
                );
            }
        }

        if (zoomDuringFinale)
        {
            float zoomT = Mathf.Clamp01(Time.deltaTime * cameraZoomSmooth);

            if (movieCamera.orthographic)
            {
                movieCamera.orthographicSize = Mathf.Lerp(
                    movieCamera.orthographicSize,
                    focusOrthographicSize,
                    zoomT
                );
            }
            else
            {
                movieCamera.fieldOfView = Mathf.Lerp(
                    movieCamera.fieldOfView,
                    focusFieldOfView,
                    zoomT
                );
            }
        }
    }

    private Vector3 GetFinaleCameraLookPoint(Vector3 pathForward)
    {
        if (cameraLookTarget != null)
            return cameraLookTarget.position;

        Vector3 lookPoint = trainRoot.position;

        if (useLookAheadFocus && pathForward.sqrMagnitude > 0.0001f)
            lookPoint += pathForward.normalized * lookAheadDistance;

        return lookPoint;
    }

    private void TryStartWhiteFlashDuringFlight(float rawProgress)
    {
        if (!startWhiteFlashBeforeFlightEnds)
            return;

        if (whiteFlashStarted)
            return;

        if (rawProgress < whiteFlashStartProgress)
            return;

        StartCoroutine(WhiteFlashTracked());
    }

    private IEnumerator WhiteFlashTracked()
    {
        whiteFlashStarted = true;
        whiteFlashFinished = false;

        if (verboseDebug)
            Debug.Log("VoidTrainFinale: white flash");

        yield return WhiteFlash();

        whiteFlashFinished = true;
    }

    private IEnumerator WhiteFlash()
    {
        if (whiteFlashCanvasGroup == null)
            yield break;

        float duration = Mathf.Max(0.0001f, whiteFlashDuration);
        float elapsed = 0f;

        whiteFlashCanvasGroup.alpha = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = whiteFlashCurve.Evaluate(rawT);

            whiteFlashCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        whiteFlashCanvasGroup.alpha = 1f;
    }
}