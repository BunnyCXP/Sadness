using System.Collections;
using UnityEngine;

public class RailMoveUnlockGate : MonoBehaviour
{
    public enum MoveAxis
    {
        X,
        Y,
        Z
    }

    [Header("要限制移动的机关 Root")]
    [Tooltip("拖可以上下移动的那个 Root。")]
    public Transform movingRoot;

    [Tooltip("通常上下移动用 Y。")]
    public MoveAxis moveAxis = MoveAxis.Y;

    [Header("未解锁前的移动限制")]
    [Tooltip("未解锁前，movingRoot 在该轴上最多只能到这个 localPosition 值。比如只能升到 0.5，就填 0.5。")]
    public float lockedMaxLocalValue = 0.5f;

    [Tooltip("是否限制最大值。")]
    public bool clampMaxBeforeUnlocked = true;

    [Tooltip("是否限制最小值。")]
    public bool clampMinBeforeUnlocked = false;

    [Tooltip("未解锁前，movingRoot 在该轴上最低只能到这个 localPosition 值。")]
    public float lockedMinLocalValue = 0f;

    [Header("解锁条件：火车到达某个点")]
    public TrainOnRails train;

    [Tooltip("火车必须在这条 RailPath 上才会检测。")]
    public RailPath requiredPath;

    [Tooltip("把这个 Empty 放在轨道上，火车到达这里后解锁。")]
    public Transform unlockPoint;

    [Tooltip("如果火车距离 unlockPoint 在这个范围内，就算到达。")]
    public float unlockDistanceTolerance = 0.08f;

    [Tooltip("如果勾选，火车经过 unlockPoint 后也算解锁，不一定要精确停在点上。")]
    public bool unlockWhenPassedPoint = true;

    [Header("上方挡住的物体")]
    [Tooltip("上方挡路 Collider。解锁后会关闭。")]
    public Collider topBlockerCollider;

    [Tooltip("如果上方物体要移开，拖它的 Transform。")]
    public Transform topObjectToMove;

    [Tooltip("是否让上方物体移动开。")]
    public bool moveTopObject = true;

    [Tooltip("上方物体移开的本地偏移量。比如往上移就填 (0, 1, 0)，往旁边移就填 (1, 0, 0)。")]
    public Vector3 topObjectMoveLocalOffset = new Vector3(0f, 1f, 0f);

    public float topObjectMoveDuration = 0.45f;

    [Header("上方挡路物体动画")]
    public bool animateTopObjectOnUnlock = true;
    public bool disableTopBlockerColliderOnUnlock = true;

    [Header("上方物体位置")]
    public bool moveTopObjectToTarget = true;
    public bool useTopObjectLocalPosition = true;
    public bool useTopObjectPositionOffset = false;
    public Vector3 topObjectTargetLocalPosition;
    public Vector3 topObjectTargetWorldPosition;
    public Vector3 topObjectPositionOffset;

    [Header("上方物体旋转")]
    public bool rotateTopObjectToTarget = true;
    public bool useTopObjectLocalRotation = true;
    public bool useTopObjectRotationOffset = false;
    public Vector3 topObjectTargetLocalEuler;
    public Vector3 topObjectTargetWorldEuler;
    public Vector3 topObjectRotationOffsetEuler;

    [Header("上方物体动画曲线")]
    public float topObjectMotionDuration = 0.8f;
    public AnimationCurve topObjectMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Extra Optional Motion")]
    [Tooltip("Extra optional target motion. For the top blocker object, prefer the fields above.")]
    public bool playUnlockTargetMotion = false;
    public Transform unlockMotionTarget;

    [Header("解锁后位置动画")]
    public bool animatePosition = true;
    public bool useLocalPosition = true;
    public bool usePositionOffset = true;
    public Vector3 targetPositionOrOffset;

    [Header("解锁后旋转动画")]
    public bool animateRotation = false;
    public bool useLocalRotation = true;
    public bool useRotationOffset = true;
    public Vector3 targetEulerOrOffset;

    public float unlockMotionDuration = 0.65f;
    public AnimationCurve unlockMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Header("状态，只读")]
    [SerializeField] private bool unlocked;
    [SerializeField] private float unlockDistanceOnPath;
    [SerializeField] private float currentTrainDistance;
    [SerializeField] private float currentDistanceToUnlockPoint;

    private Vector3 topObjectStartLocalPosition;
    private Coroutine moveTopRoutine;
    private Coroutine unlockMotionRoutine;

    void Start()
    {
        if (topObjectToMove != null)
            topObjectStartLocalPosition = topObjectToMove.localPosition;

        CalculateUnlockDistanceOnPath();
    }

    void Update()
    {
        if (unlocked)
            return;

        CheckUnlockCondition();
    }

    void LateUpdate()
    {
        if (unlocked)
            return;

        ClampMovingRoot();
    }

    void CalculateUnlockDistanceOnPath()
    {
        if (requiredPath == null || unlockPoint == null)
        {
            unlockDistanceOnPath = 0f;
            return;
        }

        unlockDistanceOnPath = FindClosestDistanceOnPath(requiredPath, unlockPoint.position);
    }

    void CheckUnlockCondition()
    {
        if (train == null || requiredPath == null || unlockPoint == null)
            return;

        if (train.currentPath != requiredPath)
            return;

        currentTrainDistance = train.distanceOnPath;

        Vector3 trainPos = requiredPath.GetPointAtDistance(train.distanceOnPath);
        currentDistanceToUnlockPoint = Vector3.Distance(trainPos, unlockPoint.position);

        bool reachedByDistance = currentDistanceToUnlockPoint <= unlockDistanceTolerance;
        bool reachedByPassedPoint = unlockWhenPassedPoint && train.distanceOnPath >= unlockDistanceOnPath;

        if (reachedByDistance || reachedByPassedPoint)
        {
            UnlockGate();
        }
    }

    void ClampMovingRoot()
    {
        if (movingRoot == null)
            return;

        Vector3 localPos = movingRoot.localPosition;
        float value = GetAxisValue(localPos);

        if (clampMaxBeforeUnlocked && value > lockedMaxLocalValue)
            value = lockedMaxLocalValue;

        if (clampMinBeforeUnlocked && value < lockedMinLocalValue)
            value = lockedMinLocalValue;

        SetAxisValue(ref localPos, value);
        movingRoot.localPosition = localPos;
    }

    public void UnlockGate()
    {
        if (unlocked)
            return;

        unlocked = true;

        if (disableTopBlockerColliderOnUnlock && topBlockerCollider != null)
            topBlockerCollider.enabled = false;

        if (topObjectToMove != null)
        {
            if (moveTopRoutine != null)
                StopCoroutine(moveTopRoutine);

            if (animateTopObjectOnUnlock)
                moveTopRoutine = StartCoroutine(AnimateTopObjectRoutine());
            else if (moveTopObject)
                moveTopRoutine = StartCoroutine(MoveTopObjectRoutine());
        }

        if (playUnlockTargetMotion && unlockMotionTarget != null)
        {
            if (unlockMotionRoutine != null)
                StopCoroutine(unlockMotionRoutine);

            unlockMotionRoutine = StartCoroutine(UnlockTargetMotionRoutine());
        }
    }

    IEnumerator AnimateTopObjectRoutine()
    {
        Vector3 startLocalPosition = topObjectToMove.localPosition;
        Vector3 startWorldPosition = topObjectToMove.position;
        Quaternion startLocalRotation = topObjectToMove.localRotation;
        Quaternion startWorldRotation = topObjectToMove.rotation;

        Vector3 targetLocalPosition = startLocalPosition;
        Vector3 targetWorldPosition = startWorldPosition;

        if (moveTopObjectToTarget)
        {
            if (useTopObjectLocalPosition)
                targetLocalPosition = useTopObjectPositionOffset ? startLocalPosition + topObjectPositionOffset : topObjectTargetLocalPosition;
            else
                targetWorldPosition = useTopObjectPositionOffset ? startWorldPosition + topObjectPositionOffset : topObjectTargetWorldPosition;
        }

        Quaternion targetLocalRotation = startLocalRotation;
        Quaternion targetWorldRotation = startWorldRotation;

        if (rotateTopObjectToTarget)
        {
            Quaternion offsetRotation = Quaternion.Euler(topObjectRotationOffsetEuler);

            if (useTopObjectLocalRotation)
                targetLocalRotation = useTopObjectRotationOffset ? startLocalRotation * offsetRotation : Quaternion.Euler(topObjectTargetLocalEuler);
            else
                targetWorldRotation = useTopObjectRotationOffset ? startWorldRotation * offsetRotation : Quaternion.Euler(topObjectTargetWorldEuler);
        }

        float duration = Mathf.Max(0.0001f, topObjectMotionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = EvaluateTopObjectMotionCurve(t);

            if (moveTopObjectToTarget)
            {
                if (useTopObjectLocalPosition)
                    topObjectToMove.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, curvedT);
                else
                    topObjectToMove.position = Vector3.Lerp(startWorldPosition, targetWorldPosition, curvedT);
            }

            if (rotateTopObjectToTarget)
            {
                if (useTopObjectLocalRotation)
                    topObjectToMove.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, curvedT);
                else
                    topObjectToMove.rotation = Quaternion.Slerp(startWorldRotation, targetWorldRotation, curvedT);
            }

            yield return null;
        }

        if (moveTopObjectToTarget)
        {
            if (useTopObjectLocalPosition)
                topObjectToMove.localPosition = targetLocalPosition;
            else
                topObjectToMove.position = targetWorldPosition;
        }

        if (rotateTopObjectToTarget)
        {
            if (useTopObjectLocalRotation)
                topObjectToMove.localRotation = targetLocalRotation;
            else
                topObjectToMove.rotation = targetWorldRotation;
        }

        moveTopRoutine = null;
    }

    IEnumerator MoveTopObjectRoutine()
    {
        Vector3 from = topObjectToMove.localPosition;
        Vector3 to = topObjectStartLocalPosition + topObjectMoveLocalOffset;

        float elapsed = 0f;

        while (elapsed < topObjectMoveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / topObjectMoveDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            topObjectToMove.localPosition = Vector3.Lerp(from, to, smoothT);

            yield return null;
        }

        topObjectToMove.localPosition = to;
        moveTopRoutine = null;
    }

    IEnumerator UnlockTargetMotionRoutine()
    {
        Vector3 startLocalPosition = unlockMotionTarget.localPosition;
        Vector3 startWorldPosition = unlockMotionTarget.position;
        Quaternion startLocalRotation = unlockMotionTarget.localRotation;
        Quaternion startWorldRotation = unlockMotionTarget.rotation;

        Vector3 targetLocalPosition = startLocalPosition;
        Vector3 targetWorldPosition = startWorldPosition;

        if (animatePosition)
        {
            if (useLocalPosition)
                targetLocalPosition = usePositionOffset ? startLocalPosition + targetPositionOrOffset : targetPositionOrOffset;
            else
                targetWorldPosition = usePositionOffset ? startWorldPosition + targetPositionOrOffset : targetPositionOrOffset;
        }

        Quaternion targetLocalRotation = startLocalRotation;
        Quaternion targetWorldRotation = startWorldRotation;

        if (animateRotation)
        {
            Quaternion rotationValue = Quaternion.Euler(targetEulerOrOffset);

            if (useLocalRotation)
                targetLocalRotation = useRotationOffset ? startLocalRotation * rotationValue : rotationValue;
            else
                targetWorldRotation = useRotationOffset ? startWorldRotation * rotationValue : rotationValue;
        }

        float duration = Mathf.Max(0.0001f, unlockMotionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = EvaluateUnlockMotionCurve(t);

            if (animatePosition)
            {
                if (useLocalPosition)
                    unlockMotionTarget.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, curvedT);
                else
                    unlockMotionTarget.position = Vector3.Lerp(startWorldPosition, targetWorldPosition, curvedT);
            }

            if (animateRotation)
            {
                if (useLocalRotation)
                    unlockMotionTarget.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, curvedT);
                else
                    unlockMotionTarget.rotation = Quaternion.Slerp(startWorldRotation, targetWorldRotation, curvedT);
            }

            yield return null;
        }

        if (animatePosition)
        {
            if (useLocalPosition)
                unlockMotionTarget.localPosition = targetLocalPosition;
            else
                unlockMotionTarget.position = targetWorldPosition;
        }

        if (animateRotation)
        {
            if (useLocalRotation)
                unlockMotionTarget.localRotation = targetLocalRotation;
            else
                unlockMotionTarget.rotation = targetWorldRotation;
        }

        unlockMotionRoutine = null;
    }

    float EvaluateUnlockMotionCurve(float t)
    {
        if (unlockMotionCurve == null || unlockMotionCurve.length == 0)
            return Mathf.SmoothStep(0f, 1f, t);

        return Mathf.Clamp01(unlockMotionCurve.Evaluate(t));
    }

    float EvaluateTopObjectMotionCurve(float t)
    {
        if (topObjectMotionCurve == null || topObjectMotionCurve.length == 0)
            return Mathf.SmoothStep(0f, 1f, t);

        return Mathf.Clamp01(topObjectMotionCurve.Evaluate(t));
    }
    float GetAxisValue(Vector3 v)
    {
        switch (moveAxis)
        {
            case MoveAxis.X:
                return v.x;

            case MoveAxis.Y:
                return v.y;

            case MoveAxis.Z:
                return v.z;

            default:
                return v.y;
        }
    }

    void SetAxisValue(ref Vector3 v, float value)
    {
        switch (moveAxis)
        {
            case MoveAxis.X:
                v.x = value;
                break;

            case MoveAxis.Y:
                v.y = value;
                break;

            case MoveAxis.Z:
                v.z = value;
                break;
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

    void OnDrawGizmosSelected()
    {
        if (unlockPoint != null)
        {
            Gizmos.color = unlocked ? Color.green : Color.red;
            Gizmos.DrawSphere(unlockPoint.position, 0.08f);
        }

        if (movingRoot != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(movingRoot.position, 0.12f);
        }
    }
}


