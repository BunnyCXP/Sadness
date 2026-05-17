using UnityEngine;

public class RailPathAutoFlipByAngle : MonoBehaviour
{
    public enum ConditionType
    {
        Angle,
        Position
    }

    public enum Axis
    {
        X,
        Y,
        Z
    }

    public enum ValueSpace
    {
        Local,
        World
    }

    public enum PositionValueMode
    {
        AbsoluteCoordinate,
        OffsetFromInitial
    }

    public enum CompareMode
    {
        NearTarget,
        GreaterOrEqual,
        LessOrEqual,
        BetweenRange,
        OutsideRange
    }

    [Header("要翻转的 RailPath")]
    public RailPath railPath;

    [Header("条件来源")]
    [Tooltip("角度模式：拖旋转的 Root。位置模式：拖移动的 Root。")]
    public Transform conditionSource;

    public ConditionType conditionType = ConditionType.Angle;

    public ValueSpace valueSpace = ValueSpace.Local;

    public Axis axis = Axis.Y;

    [Header("位置模式")]
    [Tooltip("AbsoluteCoordinate = 直接读 local/world position；OffsetFromInitial = 读取相对初始位置的移动距离。")]
    public PositionValueMode positionValueMode = PositionValueMode.OffsetFromInitial;

    [Tooltip("如果开启，运行时会把当前高度/位置作为初始值。")]
    public bool captureInitialPositionOnAwake = true;

    [Tooltip("不自动捕获初始值时使用这个值。")]
    public float manualInitialPositionValue = 0f;

    [Header("比较方式")]
    public CompareMode compareMode = CompareMode.NearTarget;

    [Tooltip("NearTarget / GreaterOrEqual / LessOrEqual 用这个值。角度比如 90，高度比如 0.5。")]
    public float targetValue = 0f;

    [Tooltip("允许误差。角度可以 3，高度可以 0.02。")]
    public float tolerance = 3f;

    [Tooltip("BetweenRange / OutsideRange 使用。")]
    public float minValue = 0f;

    [Tooltip("BetweenRange / OutsideRange 使用。")]
    public float maxValue = 1f;

    [Header("翻转规则")]
    [Tooltip("勾选：条件成立时使用反向顺序。不勾：条件不成立时使用反向顺序。")]
    public bool useReversedWhenConditionIsTrue = false;

    [Header("火车同步")]
    [Tooltip("你的 TrainOnRails。用于火车正在这条轨道上时，翻转后保持当前位置。")]
    public TrainOnRails train;

    [Tooltip("火车正在这条 RailPath 上时也立刻翻转，并修正 distanceOnPath，避免跳位。")]
    public bool flipImmediatelyWhileTrainIsOnThisPath = true;

    [Tooltip("翻转时让火车保持同一个路径比例位置。建议勾选。")]
    public bool preserveTrainPositionOnFlip = true;

    [Header("Waypoints 设置")]
    [Tooltip("正常顺序，比如 WP, WP1, WP2。为空时会自动读取 RailPath 当前 Waypoints。")]
    public Transform[] normalWaypoints;

    [Tooltip("反向顺序。为空时会自动由 Normal Waypoints 反过来生成。")]
    public Transform[] reversedWaypoints;

    [Header("调试")]
    [SerializeField] private float debugCurrentValue;
    [SerializeField] private float debugInitialPositionValue;
    [SerializeField] private bool debugConditionTrue;
    [SerializeField] private bool debugShouldUseReversed;
    [SerializeField] private bool debugCurrentlyUsingReversed;
    [SerializeField] private bool debugTrainIsOnThisPath;

    private bool hasAppliedOnce = false;
    private bool currentReversedState = false;
    private float initialPositionValue = 0f;

    void Awake()
    {
        CacheWaypointsIfNeeded();
        CaptureInitialPositionValue();
        ApplyCurrentState(true);
    }

    void Update()
    {
        ApplyCurrentState(false);
    }

    void OnValidate()
    {
        CacheWaypointsIfNeeded();
        RefreshDebug();
    }

    void CacheWaypointsIfNeeded()
    {
        if (railPath == null)
            railPath = GetComponent<RailPath>();

        if (railPath == null)
            return;

        if ((normalWaypoints == null || normalWaypoints.Length == 0) &&
            railPath.waypoints != null &&
            railPath.waypoints.Length > 0)
        {
            normalWaypoints = new Transform[railPath.waypoints.Length];

            for (int i = 0; i < railPath.waypoints.Length; i++)
            {
                normalWaypoints[i] = railPath.waypoints[i];
            }
        }

        if ((reversedWaypoints == null || reversedWaypoints.Length == 0) &&
            normalWaypoints != null &&
            normalWaypoints.Length > 0)
        {
            reversedWaypoints = new Transform[normalWaypoints.Length];

            for (int i = 0; i < normalWaypoints.Length; i++)
            {
                reversedWaypoints[i] = normalWaypoints[normalWaypoints.Length - 1 - i];
            }
        }
    }

    void CaptureInitialPositionValue()
    {
        if (conditionSource == null)
        {
            initialPositionValue = manualInitialPositionValue;
            return;
        }

        if (captureInitialPositionOnAwake)
        {
            initialPositionValue = GetRawPositionAxisValue();
        }
        else
        {
            initialPositionValue = manualInitialPositionValue;
        }

        debugInitialPositionValue = initialPositionValue;
    }

    void ApplyCurrentState(bool force)
    {
        if (railPath == null || conditionSource == null)
            return;

        bool conditionTrue = IsConditionTrue();

        bool shouldUseReversed = useReversedWhenConditionIsTrue
            ? conditionTrue
            : !conditionTrue;

        if (!force && hasAppliedOnce && shouldUseReversed == currentReversedState)
        {
            RefreshDebug();
            return;
        }

        bool trainIsOnThisPath =
            train != null &&
            train.currentPath == railPath;

        if (trainIsOnThisPath && !flipImmediatelyWhileTrainIsOnThisPath)
        {
            RefreshDebug();
            return;
        }

        float oldLength = railPath.GetLength();
        float oldDistance = trainIsOnThisPath ? train.distanceOnPath : 0f;

        Transform[] sourceArray = shouldUseReversed ? reversedWaypoints : normalWaypoints;

        if (sourceArray == null || sourceArray.Length == 0)
            return;

        railPath.waypoints = new Transform[sourceArray.Length];

        for (int i = 0; i < sourceArray.Length; i++)
        {
            railPath.waypoints[i] = sourceArray[i];
        }

        if (trainIsOnThisPath && preserveTrainPositionOnFlip)
        {
            float newLength = railPath.GetLength();

            if (oldLength > 0.0001f && newLength > 0.0001f)
            {
                float normalizedDistance = Mathf.Clamp01(oldDistance / oldLength);
                float mirroredNormalizedDistance = 1f - normalizedDistance;

                train.distanceOnPath = mirroredNormalizedDistance * newLength;
            }
        }

        currentReversedState = shouldUseReversed;
        hasAppliedOnce = true;

        RefreshDebug();
    }

    bool IsConditionTrue()
    {
        float value = GetCurrentConditionValue();

        switch (compareMode)
        {
            case CompareMode.NearTarget:
                if (conditionType == ConditionType.Angle)
                    return Mathf.Abs(Mathf.DeltaAngle(value, targetValue)) <= tolerance;

                return Mathf.Abs(value - targetValue) <= tolerance;

            case CompareMode.GreaterOrEqual:
                return value >= targetValue - tolerance;

            case CompareMode.LessOrEqual:
                return value <= targetValue + tolerance;

            case CompareMode.BetweenRange:
                return value >= minValue - tolerance && value <= maxValue + tolerance;

            case CompareMode.OutsideRange:
                return value < minValue - tolerance || value > maxValue + tolerance;

            default:
                return false;
        }
    }

    float GetCurrentConditionValue()
    {
        if (conditionSource == null)
            return 0f;

        if (conditionType == ConditionType.Angle)
            return GetCurrentAngleValue();

        return GetCurrentPositionValue();
    }

    float GetCurrentAngleValue()
    {
        Vector3 euler = valueSpace == ValueSpace.Local
            ? conditionSource.localEulerAngles
            : conditionSource.eulerAngles;

        float angle = 0f;

        switch (axis)
        {
            case Axis.X:
                angle = euler.x;
                break;

            case Axis.Y:
                angle = euler.y;
                break;

            case Axis.Z:
                angle = euler.z;
                break;
        }

        return NormalizeAngle(angle);
    }

    float GetCurrentPositionValue()
    {
        float raw = GetRawPositionAxisValue();

        if (positionValueMode == PositionValueMode.OffsetFromInitial)
            return raw - initialPositionValue;

        return raw;
    }

    float GetRawPositionAxisValue()
    {
        if (conditionSource == null)
            return 0f;

        Vector3 pos = valueSpace == ValueSpace.Local
            ? conditionSource.localPosition
            : conditionSource.position;

        switch (axis)
        {
            case Axis.X:
                return pos.x;

            case Axis.Y:
                return pos.y;

            case Axis.Z:
                return pos.z;

            default:
                return pos.y;
        }
    }

    float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle < 0f)
            angle += 360f;

        return angle;
    }

    void RefreshDebug()
    {
        if (conditionSource == null)
        {
            debugCurrentValue = -1f;
            debugConditionTrue = false;
            debugShouldUseReversed = false;
            debugCurrentlyUsingReversed = currentReversedState;
            debugTrainIsOnThisPath = false;
            debugInitialPositionValue = initialPositionValue;
            return;
        }

        debugCurrentValue = GetCurrentConditionValue();
        debugConditionTrue = IsConditionTrue();

        debugShouldUseReversed = useReversedWhenConditionIsTrue
            ? debugConditionTrue
            : !debugConditionTrue;

        debugCurrentlyUsingReversed = currentReversedState;

        debugTrainIsOnThisPath =
            train != null &&
            train.currentPath == railPath;

        debugInitialPositionValue = initialPositionValue;
    }

    [ContextMenu("Apply Normal Order")]
    public void ApplyNormalOrder()
    {
        if (railPath == null || normalWaypoints == null)
            return;

        railPath.waypoints = new Transform[normalWaypoints.Length];

        for (int i = 0; i < normalWaypoints.Length; i++)
        {
            railPath.waypoints[i] = normalWaypoints[i];
        }

        currentReversedState = false;
        hasAppliedOnce = true;
        RefreshDebug();
    }

    [ContextMenu("Apply Reversed Order")]
    public void ApplyReversedOrder()
    {
        if (railPath == null || reversedWaypoints == null)
            return;

        railPath.waypoints = new Transform[reversedWaypoints.Length];

        for (int i = 0; i < reversedWaypoints.Length; i++)
        {
            railPath.waypoints[i] = reversedWaypoints[i];
        }

        currentReversedState = true;
        hasAppliedOnce = true;
        RefreshDebug();
    }

    [ContextMenu("Capture Current Position As Initial")]
    public void CaptureCurrentPositionAsInitial()
    {
        if (conditionSource == null)
            return;

        initialPositionValue = GetRawPositionAxisValue();
        manualInitialPositionValue = initialPositionValue;
        debugInitialPositionValue = initialPositionValue;
        RefreshDebug();
    }
}