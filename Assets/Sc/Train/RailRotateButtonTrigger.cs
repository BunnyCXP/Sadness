using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RailRotateButtonTrigger : MonoBehaviour
{
    public enum RotateAxis
    {
        X,
        Y,
        Z
    }

    [Header("触发设置")]
    [Tooltip("能触发机关的对象 Tag。留空 = 任意 Collider 进入都可以触发。")]
    public string requiredTag = "";

    [Tooltip("是否只能触发一次。")]
    public bool triggerOnce = true;

    [Header("要旋转的实体轨道方块")]
    [Tooltip("只拖你要旋转的那半截实体轨道方块，不要拖 RailPath，也不要拖 WP。")]
    public Transform targetBlock;

    public RotateAxis rotateAxis = RotateAxis.X;

    [Tooltip("你的初始 X=-90，想回到 0，就填 +90。")]
    public float rotateAngle = 90f;

    public float rotateDuration = 0.65f;
    public float overshootAngle = 3.5f;

    [Header("透明挡路 Collider")]
    [Tooltip("透明挡路 Collider。触发后只禁用这个 Collider。")]
    public Collider blockerCollider;

    [Header("真正拦住 RailPath 的 Gate")]
    [Tooltip("是否启用 RailPath 逻辑拦截。必须开启，否则 Collider 挡不住 WP 系统。")]
    public bool blockTrainOnRail = true;

    [Tooltip("你的火车 TrainOnRails。")]
    public TrainOnRails train;

    [Tooltip("要被半路拦住的那条 RailPath。")]
    public RailPath gatedPath;

    [Tooltip("把这个点放在你想拦住火车的位置。脚本会自动算它在 RailPath 上的距离。")]
    public Transform gatePoint;

    [Tooltip("如果不想自动算 GatePoint，可以关掉，然后手动填 Gate Distance。")]
    public bool calculateGateDistanceFromPoint = true;

    [Tooltip("火车在 RailPath 上超过这个距离就会被卡住。")]
    public float gateDistance = 0f;

    [Tooltip("防止火车刚好卡在临界点抖动，通常 0.01~0.03。")]
    public float gateBuffer = 0.015f;

    [Tooltip("打开后，火车可以继续通过。")]
    public bool gateOpened = false;

    [Header("交互范围补偿")]
    [Tooltip("即使 Trigger 没触发，只要火车离机关足够近，也允许按 R2。")]
    public bool allowDistanceBasedPress = true;

    [Tooltip("距离机关多少以内可以按 R2。")]
    public float pressDistance = 1.2f;

    [Header("抖动设置")]
    public float shakeDuration = 0.22f;
    public float shakeRotationAmount = 2.0f;
    public float shakePositionAmount = 0.01f;
    public float shakeFrequency = 36f;

    [Header("声音")]
    public AudioSource audioSource;
    public AudioClip activateClip;
    public AudioClip rotateLoopClip;
    public AudioClip lockClip;

    [Range(0f, 1f)] public float activateVolume = 0.85f;
    [Range(0f, 1f)] public float rotateLoopVolume = 0.55f;
    [Range(0f, 1f)] public float lockVolume = 0.95f;

    [Header("手柄震动")]
    public bool useGamepadRumble = true;

    public float startRumbleDuration = 0.12f;
    [Range(0f, 1f)] public float startRumbleLow = 0.25f;
    [Range(0f, 1f)] public float startRumbleHigh = 0.45f;

    [Range(0f, 1f)] public float rotateRumbleLow = 0.08f;
    [Range(0f, 1f)] public float rotateRumbleHigh = 0.18f;

    public float lockRumbleDuration = 0.16f;
    [Range(0f, 1f)] public float lockRumbleLow = 0.45f;
    [Range(0f, 1f)] public float lockRumbleHigh = 0.75f;

    [Header("输入")]
    [Tooltip("统一用 R2 触发。")]
    public bool useGamepadR2 = true;

    [Tooltip("调试用 Space。")]
    public bool allowKeyboardDebug = true;

    [Header("状态，只读")]
    [SerializeField] private bool canPressByTrigger;
    [SerializeField] private bool canPressByDistance;
    [SerializeField] private bool hasTriggered;
    [SerializeField] private bool isAnimating;

    private Quaternion startRotation;
    private Quaternion finalRotation;
    private Vector3 startLocalPosition;

    private Coroutine currentRoutine;
    private Coroutine rumbleRoutine;

    void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (targetBlock != null)
        {
            startRotation = targetBlock.localRotation;
            finalRotation = startRotation * Quaternion.Euler(GetAxisVector() * rotateAngle);
            startLocalPosition = targetBlock.localPosition;
        }

        if (calculateGateDistanceFromPoint && gatedPath != null && gatePoint != null)
        {
            gateDistance = FindClosestDistanceOnPath(gatedPath, gatePoint.position);
        }
    }

    void OnDisable()
    {
        StopRumble();
        StopRotateLoop();
    }

    void OnDestroy()
    {
        StopRumble();
    }

    void Update()
    {
        UpdateDistanceBasedPress();

        if (!CanPress())
            return;

        if (isAnimating)
            return;

        if (triggerOnce && hasTriggered)
            return;

        if (WasInteractPressedThisFrame())
        {
            TriggerMechanism();
        }
    }

    void LateUpdate()
    {
        ApplyRailGateBlock();
    }

    void UpdateDistanceBasedPress()
    {
        canPressByDistance = false;

        if (!allowDistanceBasedPress)
            return;

        if (train == null)
            return;

        float distance = Vector3.Distance(train.transform.position, transform.position);
        canPressByDistance = distance <= pressDistance;
    }

    bool CanPress()
    {
        return canPressByTrigger || canPressByDistance;
    }

    bool WasInteractPressedThisFrame()
    {
        return GameInputHub.R2PressedThisFrame;
    }

    public void TriggerMechanism()
    {
        if (targetBlock == null)
        {
            Debug.LogWarning("RailRotateButtonTrigger: Target Block 没有设置。");
            return;
        }

        if (triggerOnce && hasTriggered)
            return;

        hasTriggered = true;
        gateOpened = true;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(MechanismRoutine());
    }

    IEnumerator MechanismRoutine()
    {
        isAnimating = true;

        DisableBlockerCollider();

        PlayOneShot(activateClip, activateVolume);
        StartRumble(startRumbleLow, startRumbleHigh, startRumbleDuration);

        yield return StartCoroutine(ShakeRoutine());

        PlayRotateLoop();
        SetRumble(rotateRumbleLow, rotateRumbleHigh);

        yield return StartCoroutine(RotateRoutine());

        StopRotateLoop();

        targetBlock.localRotation = finalRotation;
        targetBlock.localPosition = startLocalPosition;

        PlayOneShot(lockClip, lockVolume);
        StartRumble(lockRumbleLow, lockRumbleHigh, lockRumbleDuration);

        isAnimating = false;
        currentRoutine = null;
    }

    void DisableBlockerCollider()
    {
        if (blockerCollider != null)
            blockerCollider.enabled = false;
    }

    void ApplyRailGateBlock()
    {
        if (!blockTrainOnRail)
            return;

        if (gateOpened)
            return;

        if (train == null || gatedPath == null)
            return;

        if (train.currentPath != gatedPath)
            return;

        float limit = Mathf.Max(0f, gateDistance - gateBuffer);

        if (train.distanceOnPath > limit)
        {
            train.distanceOnPath = limit;

            train.SendMessage("StopTrain", SendMessageOptions.DontRequireReceiver);

            Vector3 pos = gatedPath.GetPointAtDistance(limit);
            train.transform.position = pos;

            Vector3 tangent = gatedPath.GetTangentAtDistance(limit);

            if (tangent.sqrMagnitude > 0.0001f)
                train.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        }
    }

    float FindClosestDistanceOnPath(RailPath path, Vector3 worldPosition)
    {
        if (path == null || path.waypoints == null || path.waypoints.Length < 2)
            return 0f;

        float bestDistanceOnPath = 0f;
        float bestSqrDistance = float.MaxValue;

        float walkedDistance = 0f;

        for (int i = 0; i < path.waypoints.Length - 1; i++)
        {
            Transform aT = path.waypoints[i];
            Transform bT = path.waypoints[i + 1];

            if (aT == null || bT == null)
                continue;

            Vector3 a = aT.position;
            Vector3 b = bT.position;

            Vector3 ab = b - a;
            float segmentLength = ab.magnitude;

            if (segmentLength <= 0.0001f)
                continue;

            float t = Vector3.Dot(worldPosition - a, ab) / (segmentLength * segmentLength);
            t = Mathf.Clamp01(t);

            Vector3 closestPoint = Vector3.Lerp(a, b, t);
            float sqrDistance = (worldPosition - closestPoint).sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestDistanceOnPath = walkedDistance + segmentLength * t;
            }

            walkedDistance += segmentLength;
        }

        return bestDistanceOnPath;
    }

    IEnumerator ShakeRoutine()
    {
        Quaternion baseRotation = targetBlock.localRotation;
        Vector3 basePosition = targetBlock.localPosition;

        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / shakeDuration);
            float fade = 1f - t;

            float waveA = Mathf.Sin(elapsed * shakeFrequency);
            float waveB = Mathf.Sin(elapsed * shakeFrequency * 1.37f + 1.2f);

            Vector3 shakeEuler = new Vector3(
                waveA * shakeRotationAmount * fade,
                waveB * shakeRotationAmount * 0.55f * fade,
                -waveA * shakeRotationAmount * 0.35f * fade
            );

            Vector3 shakePos = new Vector3(
                waveB * shakePositionAmount * fade,
                waveA * shakePositionAmount * 0.45f * fade,
                0f
            );

            targetBlock.localRotation = baseRotation * Quaternion.Euler(shakeEuler);
            targetBlock.localPosition = basePosition + shakePos;

            yield return null;
        }

        targetBlock.localRotation = baseRotation;
        targetBlock.localPosition = basePosition;
    }

    IEnumerator RotateRoutine()
    {
        Quaternion fromRotation = targetBlock.localRotation;
        Quaternion overshootRotation = finalRotation;

        if (Mathf.Abs(overshootAngle) > 0.001f)
        {
            overshootRotation = finalRotation * Quaternion.Euler(GetAxisVector() * overshootAngle);
        }

        float mainDuration = rotateDuration * 0.78f;
        float settleDuration = rotateDuration * 0.22f;

        float elapsed = 0f;

        while (elapsed < mainDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / mainDuration);
            float smoothT = EaseOutCubic(t);

            targetBlock.localRotation = Quaternion.Slerp(fromRotation, overshootRotation, smoothT);
            targetBlock.localPosition = startLocalPosition;

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / settleDuration);
            float smoothT = EaseOutBackSmall(t);

            targetBlock.localRotation = Quaternion.Slerp(overshootRotation, finalRotation, smoothT);
            targetBlock.localPosition = startLocalPosition;

            yield return null;
        }
    }

    void PlayOneShot(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip, volume);
    }

    void PlayRotateLoop()
    {
        if (audioSource == null || rotateLoopClip == null)
            return;

        audioSource.clip = rotateLoopClip;
        audioSource.loop = true;
        audioSource.volume = rotateLoopVolume;
        audioSource.Play();
    }

    void StopRotateLoop()
    {
        if (audioSource == null)
            return;

        if (audioSource.clip == rotateLoopClip)
        {
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }
    }

    void StartRumble(float low, float high, float duration)
    {
        if (!useGamepadRumble)
            return;

        if (Gamepad.current == null)
            return;

        if (rumbleRoutine != null)
            StopCoroutine(rumbleRoutine);

        rumbleRoutine = StartCoroutine(RumbleRoutine(low, high, duration));
    }

    IEnumerator RumbleRoutine(float low, float high, float duration)
    {
        SetRumble(low, high);

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        StopRumble();
        rumbleRoutine = null;
    }

    void SetRumble(float low, float high)
    {
        if (!useGamepadRumble)
            return;

        GameInputHub.SetRumble(low, high);
    }

    void StopRumble()
    {
        GameInputHub.StopRumble();

        if (rumbleRoutine != null)
        {
            StopCoroutine(rumbleRoutine);
            rumbleRoutine = null;
        }
    }

    Vector3 GetAxisVector()
    {
        switch (rotateAxis)
        {
            case RotateAxis.X:
                return Vector3.right;

            case RotateAxis.Y:
                return Vector3.up;

            case RotateAxis.Z:
                return Vector3.forward;

            default:
                return Vector3.right;
        }
    }

    float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    float EaseOutBackSmall(float t)
    {
        t = Mathf.Clamp01(t);

        float c1 = 1.15f;
        float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsValidTriggerObject(other))
            return;

        canPressByTrigger = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsValidTriggerObject(other))
            return;

        canPressByTrigger = false;
    }

    bool IsValidTriggerObject(Collider other)
    {
        if (string.IsNullOrWhiteSpace(requiredTag))
            return true;

        return other.CompareTag(requiredTag);
    }
}