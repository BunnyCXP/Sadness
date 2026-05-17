using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class TrainOnRails : MonoBehaviour
{
    [Header("当前轨道")]
    public RailPath currentPath;

    [Tooltip("初始在当前轨道上的距离")]
    public float distanceOnPath = 0f;

    [Header("驾驶开关")]
    public bool canDrive = false;

    [Header("倒车设置")]
    [Tooltip("是否允许在当前 RailPath 上倒车。")]
    public bool allowReverse = true;

    [Tooltip("是否允许倒车回到刚才的上一段 RailPath。")]
    public bool allowReverseAcrossPaths = true;

    [Tooltip("倒车跨回上一段 RailPath 时，是否要求原连接器仍然有效。")]
    public bool reverseRequiresConnectionStillValid = false;

    [Tooltip("倒车时车头是否转向移动方向。真实火车一般关闭，调试 cube 可以打开。")]
    public bool faceMoveDirectionWhenReversing = false;

    [Header("速度设置")]
    public float maxSpeed = 3f;
    public float acceleration = 5f;
    public float deceleration = 6f;

    [Tooltip("摇杆死区")]
    public float inputDeadZone = 0.15f;

    [Tooltip("轻推慢、推满快。数值越大，轻推越慢")]
    public float inputPower = 1.6f;

    [Header("朝向设置")]
    public bool rotateAlongPath = true;
    public float rotateSpeed = 8f;

    [Header("调试键盘")]
    public bool allowKeyboardDebug = true;

    [Header("事件")]
    public UnityEvent onBlockedAtEnd;
    public UnityEvent onBlockedAtStart;
    public UnityEvent onChangedPath;
    public UnityEvent onReachedFinalEnd;
    public UnityEvent onTeleportStarted;
    public UnityEvent onTeleportStep;
    public UnityEvent onTeleportFinished;

    private float currentSpeed = 0f;
    private bool blockedThisFrame = false;

    private struct PathReturnPoint
    {
        public RailPath path;
        public float distance;
        public RailConnector connector;

        public PathReturnPoint(RailPath path, float distance, RailConnector connector)
        {
            this.path = path;
            this.distance = distance;
            this.connector = connector;
        }
    }

    private readonly Stack<PathReturnPoint> reverseHistory = new Stack<PathReturnPoint>();

    private bool isTeleporting = false;
    private bool teleportIsReverse = false;

    private RailConnector activeTeleportConnector;
    private RailPath teleportDestinationPath;
    private float teleportDestinationDistance;

    private readonly List<Transform> activeTeleportPoints = new List<Transform>();
    private int activeTeleportIndex = 0;
    private float teleportTimer = 0f;

    void Start()
    {
        ApplyPoseToPath();
    }

    void Update()
    {
        if (isTeleporting)
        {
            UpdateStepTeleport();
            return;
        }

        if (currentPath == null)
            return;

        blockedThisFrame = false;

        float input = canDrive ? ReadDriveInput() : 0f;
        float targetSpeed = GetTargetSpeed(input);

        float speedChange =
            Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed)
                ? acceleration
                : deceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            speedChange * Time.deltaTime
        );

        MoveAlongPath(currentSpeed * Time.deltaTime);

        if (!isTeleporting)
            ApplyPoseToPath();
    }

    float ReadDriveInput()
    {
        return Mathf.Clamp(GameInputHub.LeftStick.y, -1f, 1f);
    }

    float GetTargetSpeed(float input)
    {
        if (Mathf.Abs(input) <= inputDeadZone)
            return 0f;

        if (input < 0f && !allowReverse)
            return 0f;

        float amount = Mathf.InverseLerp(inputDeadZone, 1f, Mathf.Abs(input));
        amount = Mathf.Pow(amount, inputPower);

        float sign = input >= 0f ? 1f : -1f;
        return sign * amount * maxSpeed;
    }

    void MoveAlongPath(float distanceDelta)
    {
        if (Mathf.Abs(distanceDelta) <= 0.0001f)
            return;

        if (distanceDelta > 0f)
        {
            MoveForward(distanceDelta);
        }
        else
        {
            MoveBackward(distanceDelta);
        }
    }

    void MoveForward(float distanceDelta)
    {
        float remainingDelta = distanceDelta;
        int safety = 0;

        while (remainingDelta > 0f && safety < 12)
        {
            safety++;

            float pathLength = currentPath.GetLength();

            if (pathLength <= 0.0001f)
            {
                currentSpeed = 0f;
                return;
            }

            distanceOnPath = Mathf.Clamp(distanceOnPath, 0f, pathLength);

            float distanceToEnd = pathLength - distanceOnPath;

            if (remainingDelta <= distanceToEnd)
            {
                distanceOnPath += remainingDelta;
                return;
            }

            remainingDelta -= distanceToEnd;
            distanceOnPath = pathLength;

            bool movedToNextPath = TryEnterNextPath();

            if (isTeleporting)
                return;

            if (!movedToNextPath)
            {
                currentSpeed = 0f;

                if (!blockedThisFrame)
                {
                    blockedThisFrame = true;
                    onBlockedAtEnd?.Invoke();
                }

                return;
            }
        }
    }

    void MoveBackward(float distanceDelta)
    {
        if (!allowReverse)
        {
            currentSpeed = 0f;
            return;
        }

        float remainingDelta = distanceDelta;
        int safety = 0;

        while (remainingDelta < 0f && safety < 12)
        {
            safety++;

            float pathLength = currentPath.GetLength();

            if (pathLength <= 0.0001f)
            {
                currentSpeed = 0f;
                return;
            }

            distanceOnPath = Mathf.Clamp(distanceOnPath, 0f, pathLength);

            float moveBackAmount = -remainingDelta;

            if (moveBackAmount <= distanceOnPath)
            {
                distanceOnPath -= moveBackAmount;
                return;
            }

            remainingDelta += distanceOnPath;
            distanceOnPath = 0f;

            bool returnedToPreviousPath = TryReturnToPreviousPath();

            if (isTeleporting)
                return;

            if (!returnedToPreviousPath)
            {
                currentSpeed = 0f;

                if (!blockedThisFrame)
                {
                    blockedThisFrame = true;
                    onBlockedAtStart?.Invoke();
                }

                return;
            }
        }
    }

    bool TryEnterNextPath()
    {
        if (currentPath == null)
            return false;

        RailConnector connector = currentPath.exitConnector;

        if (connector == null)
        {
            onReachedFinalEnd?.Invoke();
            return false;
        }

        if (!connector.CanEnterNextPath())
            return false;

        RailPath nextPath = connector.nextPath;

        if (nextPath == null)
            return false;

        float currentPathEndDistance = currentPath.GetLength();

        reverseHistory.Push(
            new PathReturnPoint(
                currentPath,
                currentPathEndDistance,
                connector
            )
        );

        if (connector.HasTransitionPoints())
        {
            BeginStepTeleport(
                connector,
                nextPath,
                0f,
                false
            );

            return true;
        }

        currentPath = nextPath;
        distanceOnPath = 0f;

        onChangedPath?.Invoke();

        return true;
    }

    bool TryReturnToPreviousPath()
    {
        if (!allowReverseAcrossPaths)
            return false;

        while (reverseHistory.Count > 0)
        {
            PathReturnPoint returnPoint = reverseHistory.Peek();

            if (returnPoint.path == null)
            {
                reverseHistory.Pop();
                continue;
            }

            if (reverseRequiresConnectionStillValid)
            {
                if (returnPoint.connector == null)
                    return false;

                if (!returnPoint.connector.CanEnterNextPath())
                    return false;
            }

            reverseHistory.Pop();

            if (returnPoint.connector != null &&
                returnPoint.connector.HasTransitionPoints())
            {
                BeginStepTeleport(
                    returnPoint.connector,
                    returnPoint.path,
                    returnPoint.distance,
                    true
                );

                return true;
            }

            currentPath = returnPoint.path;

            float pathLength = currentPath.GetLength();
            distanceOnPath = Mathf.Clamp(returnPoint.distance, 0f, pathLength);

            onChangedPath?.Invoke();

            return true;
        }

        return false;
    }

    void BeginStepTeleport(
        RailConnector connector,
        RailPath destinationPath,
        float destinationDistance,
        bool reverse
    )
    {
        if (connector == null || destinationPath == null)
            return;

        activeTeleportConnector = connector;
        teleportDestinationPath = destinationPath;
        teleportDestinationDistance = destinationDistance;
        teleportIsReverse = reverse;

        activeTeleportPoints.Clear();

        if (connector.transitionPoints != null)
        {
            if (!reverse)
            {
                for (int i = 0; i < connector.transitionPoints.Length; i++)
                {
                    if (connector.transitionPoints[i] != null)
                        activeTeleportPoints.Add(connector.transitionPoints[i]);
                }
            }
            else
            {
                for (int i = connector.transitionPoints.Length - 1; i >= 0; i--)
                {
                    if (connector.transitionPoints[i] != null)
                        activeTeleportPoints.Add(connector.transitionPoints[i]);
                }
            }
        }

        activeTeleportIndex = 0;
        teleportTimer = 0f;

        isTeleporting = true;
        currentSpeed = 0f;

        onTeleportStarted?.Invoke();

        if (activeTeleportPoints.Count > 0)
        {
            SnapToCurrentTeleportPoint();
        }
        else
        {
            CompleteStepTeleport();
        }
    }

    void UpdateStepTeleport()
    {
        if (activeTeleportConnector == null || teleportDestinationPath == null)
        {
            CancelStepTeleport();
            return;
        }

        float interval = Mathf.Max(0.02f, activeTeleportConnector.teleportStepInterval);

        teleportTimer += Time.deltaTime;

        if (teleportTimer < interval)
            return;

        teleportTimer = 0f;
        activeTeleportIndex++;

        if (activeTeleportIndex < activeTeleportPoints.Count)
        {
            SnapToCurrentTeleportPoint();
        }
        else
        {
            CompleteStepTeleport();
        }
    }

    void SnapToCurrentTeleportPoint()
    {
        if (activeTeleportIndex < 0 || activeTeleportIndex >= activeTeleportPoints.Count)
            return;

        Transform point = activeTeleportPoints[activeTeleportIndex];

        if (point == null)
            return;

        transform.position = point.position;

        if (activeTeleportConnector != null && activeTeleportConnector.rotateDuringTeleport)
        {
            RotateTowardNextTeleportTarget();
        }

        onTeleportStep?.Invoke();
    }

    void RotateTowardNextTeleportTarget()
    {
        Vector3 targetPosition;

        int nextIndex = activeTeleportIndex + 1;

        if (nextIndex < activeTeleportPoints.Count && activeTeleportPoints[nextIndex] != null)
        {
            targetPosition = activeTeleportPoints[nextIndex].position;
        }
        else
        {
            targetPosition = GetFinalTeleportTargetPosition();
        }

        Vector3 dir = targetPosition - transform.position;

        if (dir.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    Vector3 GetFinalTeleportTargetPosition()
    {
        if (activeTeleportConnector == null)
            return transform.position;

        if (!teleportIsReverse)
        {
            return activeTeleportConnector.GetForwardFinalTargetPosition();
        }

        return activeTeleportConnector.GetReverseFinalTargetPosition(
            teleportDestinationPath,
            teleportDestinationDistance
        );
    }

    void CompleteStepTeleport()
    {
        currentPath = teleportDestinationPath;

        float pathLength = currentPath.GetLength();
        distanceOnPath = Mathf.Clamp(teleportDestinationDistance, 0f, pathLength);

        isTeleporting = false;
        teleportIsReverse = false;

        activeTeleportConnector = null;
        teleportDestinationPath = null;
        activeTeleportPoints.Clear();
        activeTeleportIndex = 0;
        teleportTimer = 0f;

        ApplyPoseToPath();

        onChangedPath?.Invoke();
        onTeleportFinished?.Invoke();
    }

    void CancelStepTeleport()
    {
        isTeleporting = false;
        teleportIsReverse = false;

        activeTeleportConnector = null;
        teleportDestinationPath = null;
        activeTeleportPoints.Clear();
        activeTeleportIndex = 0;
        teleportTimer = 0f;

        currentSpeed = 0f;
    }

    void ApplyPoseToPath()
    {
        if (currentPath == null)
            return;

        float pathLength = currentPath.GetLength();
        distanceOnPath = Mathf.Clamp(distanceOnPath, 0f, pathLength);

        Vector3 targetPosition = currentPath.GetPointAtDistance(distanceOnPath);
        transform.position = targetPosition;

        if (rotateAlongPath)
        {
            Vector3 tangent = currentPath.GetTangentAtDistance(distanceOnPath);

            if (currentSpeed < -0.001f && faceMoveDirectionWhenReversing)
                tangent = -tangent;

            if (tangent.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(tangent, Vector3.up);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    Time.deltaTime * rotateSpeed
                );
            }
        }
    }

    public void EnableDrive()
    {
        canDrive = true;
    }

    public void DisableDrive()
    {
        canDrive = false;
        currentSpeed = 0f;
    }

    public void StopTrain()
    {
        currentSpeed = 0f;
    }

    public void ClearReverseHistory()
    {
        reverseHistory.Clear();
    }

    public void SetPath(RailPath newPath, float newDistance = 0f)
    {
        currentPath = newPath;
        distanceOnPath = newDistance;
        currentSpeed = 0f;
        reverseHistory.Clear();

        CancelStepTeleport();
        ApplyPoseToPath();
    }
}