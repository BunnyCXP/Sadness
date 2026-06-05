using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class OpeningTrainCinematicSequence : MonoBehaviour
{
    [System.Serializable]
    public class Cue
    {
        public float triggerDistance;
        [TextArea(1, 3)] public string text;
        public Transform anchor;
        public float duration = 2.5f;
        public bool triggered;
    }

    [Header("References")]
    public TrainOnRails train;
    public RailPath introPath;
    public Camera movieCamera;
    public InteractionManager interactionManager;

    [Header("Camera Focus During White Flash")]
    public bool focusCameraDuringWhiteFlash = true;
    public bool lockCameraPosition = true;
    public bool rotateCameraToTrain = true;
    public bool zoomCameraDuringIntro = true;
    public bool restoreCameraAfterIntro = true;

    public Transform cameraLookTarget;
    public float introOrthographicSize = 3.5f;
    public float introFieldOfView = 35f;
    public float cameraRotationSmooth = 6f;
    public float cameraZoomSmooth = 4f;
    public float lookAheadDistance = 0.4f;

    [Header("Train Motion")]
    public float startDistance = 0f;
    public float endDistance = 10f;
    public float autoDriveSpeed = 0.7f;
    public bool usePathEndAsEndDistance = true;

    [Header("Intro Train Motion")]
    public AnimationCurve driveSpeedCurve = AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1f);
    public float minDriveSpeedMultiplier = 0.35f;

    [Header("Outro Timing")]
    public float postTrainHoldDuration = 0.45f;

    [Header("Outro Camera Restore")]
    public float cameraRestoreDuration = 2.4f;
    public AnimationCurve cameraRestoreCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.3f, 0.08f),
        new Keyframe(0.75f, 0.78f),
        new Keyframe(1f, 1f)
    );

    [Header("Cue Markers")]
    [Tooltip("这里只保留距离 cue 的 Debug 触发。真实字幕请用独立 Trigger 字幕脚本处理。")]
    public Cue[] cues;

    [Header("Events")]
    public UnityEvent onIntroStarted;
    public UnityEvent onIntroFinished;

    [Header("Debug")]
    [SerializeField] private bool isPlaying;

    private bool isPrepared;
    private bool canPlayPreparedSequence;
    private float preparedPathLength;
    private float preparedEndDistance;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private float originalOrthographicSize;
    private float originalFieldOfView;

    public void PrepareForWhiteReveal()
    {
        isPrepared = false;
        canPlayPreparedSequence = false;

        if (movieCamera == null)
            Debug.LogWarning("OpeningSequence: movieCamera is missing. Camera focus may not work.");

        CacheCameraState();

        if (interactionManager != null)
            interactionManager.canInteract = false;

        if (train == null || introPath == null)
        {
            Debug.LogWarning("OpeningSequence: missing Train or Intro Path. Intro sequence skipped.");
            isPrepared = true;
            return;
        }

        train.DisableDrive();
        train.currentPath = introPath;
        train.distanceOnPath = startDistance;
        train.ClearReverseHistory();

        ResetCues();

        preparedPathLength = introPath.GetLength();

        if (usePathEndAsEndDistance)
            endDistance = preparedPathLength;

        preparedEndDistance = Mathf.Clamp(endDistance, 0f, preparedPathLength);

        Debug.Log(
            "OpeningSequence: pathLength=" + preparedPathLength +
            ", startDistance=" + startDistance +
            ", endDistance=" + preparedEndDistance +
            ", speed=" + autoDriveSpeed
        );

        train.distanceOnPath = Mathf.Clamp(startDistance, 0f, preparedEndDistance);
        ApplyTrainPose(train.distanceOnPath);
        ApplyCameraFocus(0f, 1f, true);

        isPrepared = true;
        canPlayPreparedSequence = preparedPathLength > 0f && autoDriveSpeed > 0f;

        if (preparedPathLength <= 0f)
            Debug.LogWarning("OpeningSequence: Intro Path length is 0. Intro sequence skipped.");

        if (autoDriveSpeed <= 0f)
            Debug.LogWarning("OpeningSequence: autoDriveSpeed must be greater than 0. Intro sequence skipped.");

        Debug.Log("OpeningSequence: prepared during white flash");
    }

    public IEnumerator PlayAfterWhiteReveal()
    {
        if (!isPrepared)
            PrepareForWhiteReveal();

        if (!isPrepared)
            yield break;

        Debug.Log("OpeningSequence: started after white reveal");
        isPlaying = true;
        onIntroStarted?.Invoke();

        if (!canPlayPreparedSequence)
        {
            FinishTrainMotion(preparedEndDistance);
            yield break;
        }

        while (train.distanceOnPath < preparedEndDistance)
        {
            float progress = preparedEndDistance > 0.0001f
                ? Mathf.InverseLerp(startDistance, preparedEndDistance, train.distanceOnPath)
                : 1f;

            float speedMultiplier = Mathf.Max(minDriveSpeedMultiplier, EvaluateDriveSpeedCurve(progress));
            float nextDistance = train.distanceOnPath + autoDriveSpeed * speedMultiplier * Time.deltaTime;

            train.distanceOnPath = Mathf.Min(nextDistance, preparedEndDistance);

            ApplyTrainPose(train.distanceOnPath);

            progress = preparedEndDistance > 0.0001f
                ? Mathf.InverseLerp(startDistance, preparedEndDistance, train.distanceOnPath)
                : 1f;

            ApplyCameraFocus(Time.deltaTime, progress, false);
            TriggerCues(train.distanceOnPath);

            yield return null;
        }

        FinishTrainMotion(preparedEndDistance);

        if (postTrainHoldDuration > 0f)
            yield return new WaitForSeconds(postTrainHoldDuration);
    }

    public IEnumerator Play()
    {
        PrepareForWhiteReveal();
        yield return PlayAfterWhiteReveal();
    }

    void CacheCameraState()
    {
        if (movieCamera == null)
            return;

        originalCameraPosition = movieCamera.transform.position;
        originalCameraRotation = movieCamera.transform.rotation;
        originalOrthographicSize = movieCamera.orthographicSize;
        originalFieldOfView = movieCamera.fieldOfView;
    }

    void ResetCues()
    {
        if (cues == null)
            return;

        for (int i = 0; i < cues.Length; i++)
        {
            if (cues[i] != null)
                cues[i].triggered = false;
        }
    }

    void TriggerCues(float currentDistance)
    {
        if (cues == null)
            return;

        for (int i = 0; i < cues.Length; i++)
        {
            Cue cue = cues[i];

            if (cue == null || cue.triggered)
                continue;

            if (currentDistance >= cue.triggerDistance)
            {
                cue.triggered = true;
                Debug.Log("OpeningSequence: cue triggered: " + cue.text);
            }
        }
    }

    void ApplyTrainPose(float distance)
    {
        if (train == null || introPath == null)
            return;

        train.transform.position = introPath.GetPointAtDistance(distance);

        Vector3 tangent = introPath.GetTangentAtDistance(distance);

        if (tangent.sqrMagnitude > 0.0001f)
            train.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
    }

    void ApplyCameraFocus(float deltaTime, float progress, bool instant)
    {
        if (!focusCameraDuringWhiteFlash || movieCamera == null)
            return;

        if (lockCameraPosition)
            movieCamera.transform.position = originalCameraPosition;

        if (rotateCameraToTrain)
            RotateCameraToTrain(deltaTime, instant);

        if (zoomCameraDuringIntro)
            ZoomCamera(deltaTime, progress, instant);
    }

    void RotateCameraToTrain(float deltaTime, bool instant)
    {
        Vector3 lookPoint = GetCameraLookPoint();
        Vector3 direction = lookPoint - originalCameraPosition;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        float rotationT = instant ? 1f : Mathf.Clamp01(deltaTime * cameraRotationSmooth);

        movieCamera.transform.rotation = Quaternion.Slerp(
            movieCamera.transform.rotation,
            targetRotation,
            rotationT
        );
    }

    Vector3 GetCameraLookPoint()
    {
        if (cameraLookTarget != null)
            return cameraLookTarget.position;

        Vector3 lookPoint = train.transform.position;

        if (introPath != null)
        {
            Vector3 tangent = introPath.GetTangentAtDistance(train.distanceOnPath);

            if (tangent.sqrMagnitude > 0.0001f)
                lookPoint += tangent.normalized * lookAheadDistance;
        }

        return lookPoint;
    }

    void ZoomCamera(float deltaTime, float progress, bool instant)
    {
        float zoomT = instant ? 1f : Mathf.Clamp01(deltaTime * cameraZoomSmooth);

        if (movieCamera.orthographic)
        {
            movieCamera.orthographicSize = Mathf.Lerp(
                movieCamera.orthographicSize,
                introOrthographicSize,
                zoomT
            );
        }
        else
        {
            movieCamera.fieldOfView = Mathf.Lerp(
                movieCamera.fieldOfView,
                introFieldOfView,
                zoomT
            );
        }
    }

    void FinishTrainMotion(float finalDistance)
    {
        if (train != null && introPath != null)
        {
            train.currentPath = introPath;
            train.distanceOnPath = finalDistance;
            ApplyTrainPose(train.distanceOnPath);
        }

        if (movieCamera != null && lockCameraPosition)
            movieCamera.transform.position = originalCameraPosition;

        isPlaying = false;

        Debug.Log("OpeningSequence: train motion finished, waiting for outro");
        onIntroFinished?.Invoke();
    }

    public IEnumerator RestoreCameraAfterIntroSmooth(float duration)
    {
        if (movieCamera == null)
            yield break;

        Debug.Log("OpeningSequence: camera restore started");

        if (duration <= 0f)
        {
            RestoreCameraState();
            Debug.Log("OpeningSequence: camera restore finished");
            yield break;
        }

        Vector3 startPosition = movieCamera.transform.position;
        Quaternion startRotation = movieCamera.transform.rotation;
        float startOrthographicSize = movieCamera.orthographicSize;
        float startFieldOfView = movieCamera.fieldOfView;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = cameraRestoreCurve != null && cameraRestoreCurve.length > 0
                ? Mathf.Clamp01(cameraRestoreCurve.Evaluate(t))
                : Mathf.SmoothStep(0f, 1f, t);

            movieCamera.transform.position = lockCameraPosition
                ? originalCameraPosition
                : Vector3.Lerp(startPosition, originalCameraPosition, smoothT);

            movieCamera.transform.rotation = Quaternion.Slerp(
                startRotation,
                originalCameraRotation,
                smoothT
            );

            if (movieCamera.orthographic)
            {
                movieCamera.orthographicSize = Mathf.Lerp(
                    startOrthographicSize,
                    originalOrthographicSize,
                    smoothT
                );
            }
            else
            {
                movieCamera.fieldOfView = Mathf.Lerp(
                    startFieldOfView,
                    originalFieldOfView,
                    smoothT
                );
            }

            yield return null;
        }

        RestoreCameraState();
        Debug.Log("OpeningSequence: camera restore finished");
    }

    public void RestorePlayerControl()
    {
        if (train != null)
            train.EnableDrive();

        if (interactionManager != null)
            interactionManager.canInteract = true;

        isPrepared = false;

        Debug.Log("OpeningSequence: player control restored");
    }

    void RestoreCameraState()
    {
        if (movieCamera == null)
            return;

        movieCamera.transform.position = originalCameraPosition;
        movieCamera.transform.rotation = originalCameraRotation;
        movieCamera.orthographicSize = originalOrthographicSize;
        movieCamera.fieldOfView = originalFieldOfView;
    }

    float EvaluateDriveSpeedCurve(float progress)
    {
        if (driveSpeedCurve == null || driveSpeedCurve.length == 0)
            return 1f;

        return driveSpeedCurve.Evaluate(Mathf.Clamp01(progress));
    }
}