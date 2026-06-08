using UnityEngine;

public class RailConnector : MonoBehaviour
{
    public enum ConnectionMode
    {
        AlwaysConnected,
        DistanceAndAngle,
        Manual,
        ManualAndAngle,
        ManualAndCustomAngle
    }

    public enum CustomAngleAxis
    {
        X,
        Y,
        Z
    }

    [System.Serializable]
    public class RequiredDistanceCheck
    {
        [Header("额外接口检查")]
        public bool enabled = true;

        [Tooltip("第一个接口点，比如 RA 的出口点")]
        public Transform pointA;

        [Tooltip("第二个接口点，比如 Transition 的入口点")]
        public Transform pointB;

        [Tooltip("两个点距离小于这个值，才算接上")]
        public float maxDistance = 0.2f;

        [Tooltip("是否额外检查方向")]
        public bool useAngleCheck = false;

        [Tooltip("允许的最大角度差")]
        public float maxAngle = 15f;

        [Tooltip("如果两个接口 forward 是反方向，也允许通过")]
        public bool allowOppositeForward = false;

        public bool IsConnected()
        {
            if (!enabled)
                return true;

            if (pointA == null || pointB == null)
                return false;

            float distance = Vector3.Distance(pointA.position, pointB.position);

            if (distance > maxDistance)
                return false;

            if (!useAngleCheck)
                return true;

            float angle = Vector3.Angle(pointA.forward, pointB.forward);

            if (allowOppositeForward)
            {
                float oppositeAngle = Vector3.Angle(pointA.forward, -pointB.forward);
                angle = Mathf.Min(angle, oppositeAngle);
            }

            return angle <= maxAngle;
        }

        public float GetCurrentDistance()
        {
            if (pointA == null || pointB == null)
                return -1f;

            return Vector3.Distance(pointA.position, pointB.position);
        }

        public float GetCurrentAngle()
        {
            if (pointA == null || pointB == null)
                return -1f;

            float angle = Vector3.Angle(pointA.forward, pointB.forward);

            if (allowOppositeForward)
            {
                float oppositeAngle = Vector3.Angle(pointA.forward, -pointB.forward);
                angle = Mathf.Min(angle, oppositeAngle);
            }

            return angle;
        }
    }

    [Header("连接模式")]
    public ConnectionMode connectionMode = ConnectionMode.DistanceAndAngle;

    [Header("下一段轨道")]
    public RailPath nextPath;

    [Header("连接点")]
    public Transform thisEnd;
    public Transform otherEnd;

    [Header("距离 / 两端方向判定")]
    public float maxDistance = 0.15f;
    public bool useAngleCheck = false;
    public float maxAngle = 15f;
    public bool allowOppositeForward = false;

    [Header("手动连接")]
    public bool manualConnected = false;

    [Header("Manual + 两端方向")]
    public bool manualAngleRequiresDistance = false;
    public bool manualAngleRequiresAngle = true;

    [Header("Manual + 自定义目标角度")]
    [Tooltip("拖真正会旋转的 Root。比如 Rail_Up_Root，不一定是 Rail_Up 本身。")]
    public Transform customAngleSource;

    [Tooltip("勾选=读 localEulerAngles；不勾=读世界 eulerAngles。")]
    public bool useLocalAngle = true;

    public CustomAngleAxis customAngleAxis = CustomAngleAxis.Y;

    [Tooltip("目标角度。比如希望 Rail_Up_Root 的 Y = 90 时接通，就填 90。")]
    public float targetAngle = 90f;

    [Tooltip("允许误差。比如 5 代表 85~95 都能通过。")]
    public float customAngleTolerance = 5f;

    [Tooltip("视错觉闪现连接一般不勾。勾了以后还会检查 thisEnd 和 otherEnd 的距离。")]
    public bool customAngleRequiresDistance = false;

    [Header("额外接通条件")]
    [Tooltip("开启后，除了当前 Connector 自己的条件，还必须满足下面所有额外检查。")]
    public bool useExtraRequiredChecks = false;

    [Tooltip("比如 Transition -> RB 时，可以额外要求 RA_ExitPoint 和 Transition_EntryPoint 仍然对齐。")]
    public RequiredDistanceCheck[] extraRequiredChecks;

    [Header("分段闪现过渡")]
    [Tooltip("连接成功后会依次闪现到这些点，再进入 Next Path。不要把这些点放进 RailPath 的 Waypoints。")]
    public Transform[] transitionPoints;

    [Tooltip("每个闪现点停留多久。太小会看起来像直接跳过。")]
    public float teleportStepInterval = 0.08f;

    [Tooltip("闪到过渡点时是否立刻朝向下一个目标。")]
    public bool rotateDuringTeleport = true;

    [Header("调试显示，只读")]
    [SerializeField] private float debugCurrentCustomAngle;
    [SerializeField] private float debugTargetAngle;
    [SerializeField] private float debugAngleDelta;
    [SerializeField] private bool debugHasNextPath;
    [SerializeField] private bool debugManualPass;
    [SerializeField] private bool debugAnglePass;
    [SerializeField] private bool debugDistancePass;
    [SerializeField] private bool debugExtraRequiredPass;
    [SerializeField] private bool debugFinalCanEnter;

    void Update()
    {
        RefreshDebugValues();
    }

    void OnValidate()
    {
        RefreshDebugValues();
    }

    public bool CanEnterNextPath()
    {
        bool result = EvaluateCanEnter();
        RefreshDebugValues();
        return result;
    }

    bool EvaluateCanEnter()
    {
        if (nextPath == null)
            return false;

        bool basePass = false;

        switch (connectionMode)
        {
            case ConnectionMode.AlwaysConnected:
                basePass = true;
                break;

            case ConnectionMode.Manual:
                basePass = manualConnected;
                break;

            case ConnectionMode.DistanceAndAngle:
                basePass = CheckDistanceAndAngle();
                break;

            case ConnectionMode.ManualAndAngle:
                basePass = CheckManualAndAngle();
                break;

            case ConnectionMode.ManualAndCustomAngle:
                basePass = CheckManualAndCustomAngle();
                break;

            default:
                basePass = false;
                break;
        }

        if (!basePass)
            return false;

        if (useExtraRequiredChecks && !CheckExtraRequiredConditions())
            return false;

        return true;
    }

    bool CheckDistanceAndAngle()
    {
        if (!CheckDistanceOnly())
            return false;

        if (useAngleCheck && !CheckForwardAngleOnly())
            return false;

        return true;
    }

    bool CheckManualAndAngle()
    {
        if (!manualConnected)
            return false;

        if (manualAngleRequiresDistance && !CheckDistanceOnly())
            return false;

        if (manualAngleRequiresAngle && !CheckForwardAngleOnly())
            return false;

        return true;
    }

    bool CheckManualAndCustomAngle()
    {
        if (!manualConnected)
            return false;

        if (customAngleRequiresDistance && !CheckDistanceOnly())
            return false;

        if (!CheckCustomAngleOnly())
            return false;

        return true;
    }

    bool CheckDistanceOnly()
    {
        if (thisEnd == null || otherEnd == null)
            return false;

        float distance = Vector3.Distance(thisEnd.position, otherEnd.position);
        return distance <= maxDistance;
    }

    bool CheckForwardAngleOnly()
    {
        if (thisEnd == null || otherEnd == null)
            return false;

        float angle = Vector3.Angle(thisEnd.forward, otherEnd.forward);

        if (allowOppositeForward)
        {
            float oppositeAngle = Vector3.Angle(thisEnd.forward, -otherEnd.forward);
            angle = Mathf.Min(angle, oppositeAngle);
        }

        return angle <= maxAngle;
    }

    bool CheckCustomAngleOnly()
    {
        if (customAngleSource == null)
            return false;

        float currentAngle = GetCurrentCustomAngle();
        float delta = Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle));

        return delta <= customAngleTolerance;
    }

    bool CheckExtraRequiredConditions()
    {
        if (extraRequiredChecks == null || extraRequiredChecks.Length == 0)
            return true;

        for (int i = 0; i < extraRequiredChecks.Length; i++)
        {
            RequiredDistanceCheck check = extraRequiredChecks[i];

            if (check == null)
                return false;

            if (!check.IsConnected())
                return false;
        }

        return true;
    }

    public float GetCurrentCustomAngle()
    {
        if (customAngleSource == null)
            return 0f;

        Vector3 euler = useLocalAngle
            ? customAngleSource.localEulerAngles
            : customAngleSource.eulerAngles;

        float angle;

        switch (customAngleAxis)
        {
            case CustomAngleAxis.X:
                angle = euler.x;
                break;

            case CustomAngleAxis.Y:
                angle = euler.y;
                break;

            case CustomAngleAxis.Z:
                angle = euler.z;
                break;

            default:
                angle = euler.y;
                break;
        }

        return NormalizeAngle(angle);
    }

    float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle < 0f)
            angle += 360f;

        return angle;
    }

    public float GetCurrentDistance()
    {
        if (thisEnd == null || otherEnd == null)
            return -1f;

        return Vector3.Distance(thisEnd.position, otherEnd.position);
    }

    public float GetCurrentForwardAngle()
    {
        if (thisEnd == null || otherEnd == null)
            return -1f;

        float angle = Vector3.Angle(thisEnd.forward, otherEnd.forward);

        if (allowOppositeForward)
        {
            float oppositeAngle = Vector3.Angle(thisEnd.forward, -otherEnd.forward);
            angle = Mathf.Min(angle, oppositeAngle);
        }

        return angle;
    }

    public float GetCurrentCustomAngleDelta()
    {
        if (customAngleSource == null)
            return -1f;

        return Mathf.Abs(Mathf.DeltaAngle(GetCurrentCustomAngle(), targetAngle));
    }

    public void SetManualConnected(bool connected)
    {
        manualConnected = connected;
        RefreshDebugValues();
    }

    public bool HasTransitionPoints()
    {
        if (transitionPoints == null)
            return false;

        for (int i = 0; i < transitionPoints.Length; i++)
        {
            if (transitionPoints[i] != null)
                return true;
        }

        return false;
    }

    public Vector3 GetForwardFinalTargetPosition()
    {
        if (nextPath != null)
            return nextPath.GetPointAtDistance(0f);

        if (otherEnd != null)
            return otherEnd.position;

        return transform.position;
    }

    public Vector3 GetReverseFinalTargetPosition(RailPath previousPath, float previousDistance)
    {
        if (previousPath != null)
            return previousPath.GetPointAtDistance(previousDistance);

        if (thisEnd != null)
            return thisEnd.position;

        return transform.position;
    }

    void RefreshDebugValues()
    {
        debugHasNextPath = nextPath != null;

        debugCurrentCustomAngle = customAngleSource != null ? GetCurrentCustomAngle() : -1f;
        debugTargetAngle = targetAngle;
        debugAngleDelta = customAngleSource != null ? GetCurrentCustomAngleDelta() : -1f;

        debugManualPass = manualConnected;

        debugAnglePass =
            customAngleSource != null &&
            debugAngleDelta <= customAngleTolerance;

        debugDistancePass = true;

        if (connectionMode == ConnectionMode.DistanceAndAngle)
        {
            debugDistancePass = CheckDistanceOnly();
        }
        else if (connectionMode == ConnectionMode.ManualAndAngle && manualAngleRequiresDistance)
        {
            debugDistancePass = CheckDistanceOnly();
        }
        else if (connectionMode == ConnectionMode.ManualAndCustomAngle && customAngleRequiresDistance)
        {
            debugDistancePass = CheckDistanceOnly();
        }

        debugExtraRequiredPass =
            !useExtraRequiredChecks || CheckExtraRequiredConditions();

        debugFinalCanEnter = EvaluateCanEnter();
    }

    void OnDrawGizmosSelected()
    {
        RefreshDebugValues();

        if (thisEnd != null && otherEnd != null)
        {
            Gizmos.color = debugFinalCanEnter ? Color.green : Color.red;
            Gizmos.DrawLine(thisEnd.position, otherEnd.position);

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(thisEnd.position, thisEnd.forward * 0.35f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(otherEnd.position, otherEnd.forward * 0.35f);
        }

        DrawExtraRequiredGizmos();
        DrawTransitionGizmos();
    }

    void DrawExtraRequiredGizmos()
    {
        if (!useExtraRequiredChecks)
            return;

        if (extraRequiredChecks == null)
            return;

        for (int i = 0; i < extraRequiredChecks.Length; i++)
        {
            RequiredDistanceCheck check = extraRequiredChecks[i];

            if (check == null || !check.enabled)
                continue;

            if (check.pointA == null || check.pointB == null)
                continue;

            bool connected = check.IsConnected();

            Gizmos.color = connected ? Color.green : Color.red;
            Gizmos.DrawLine(check.pointA.position, check.pointB.position);

            Gizmos.DrawSphere(check.pointA.position, 0.045f);
            Gizmos.DrawSphere(check.pointB.position, 0.045f);

            if (check.useAngleCheck)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(check.pointA.position, check.pointA.forward * 0.25f);

                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(check.pointB.position, check.pointB.forward * 0.25f);
            }
        }
    }

    void DrawTransitionGizmos()
    {
        Vector3 lastPoint;
        bool hasLast = false;

        if (thisEnd != null)
        {
            lastPoint = thisEnd.position;
            hasLast = true;
        }
        else
        {
            lastPoint = transform.position;
            hasLast = true;
        }

        Gizmos.color = new Color(1f, 0.55f, 0f, 1f);

        if (transitionPoints != null)
        {
            for (int i = 0; i < transitionPoints.Length; i++)
            {
                if (transitionPoints[i] == null)
                    continue;

                if (hasLast)
                    Gizmos.DrawLine(lastPoint, transitionPoints[i].position);

                Gizmos.DrawSphere(transitionPoints[i].position, 0.06f);

                lastPoint = transitionPoints[i].position;
                hasLast = true;
            }
        }

        Vector3 finalPoint = GetForwardFinalTargetPosition();

        if (hasLast)
            Gizmos.DrawLine(lastPoint, finalPoint);

        Gizmos.DrawSphere(finalPoint, 0.08f);
    }
}