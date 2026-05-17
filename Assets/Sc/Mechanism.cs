using UnityEngine;

public class Mechanism : MonoBehaviour
{
    public enum MechanismType
    {
        Rotate,
        Slide
    }

    public enum InputMode
    {
        HorizontalOnly,   // 只吃左右输入
        VerticalOnly,     // 只吃上下输入
        Combined          // X + Y 合并
    }

    public enum RotateAxis
    {
        X,
        Y,
        Z
    }

    public enum SlideDirection
    {
        UpDown,        // Y 轴
        LeftRight,     // X 轴
        ForwardBack,   // Z 轴
        Custom         // 自定义轴
    }

    [Header("机关类型")]
    public MechanismType type = MechanismType.Rotate;

    [Tooltip("你想控制的父物体，比如整个高塔、一段桥、一段轨道")]
    public Transform targetObject;

    [Header("--- 通用设置 ---")]
    [Tooltip("锁住后不能再被拖动")]
    public bool isLocked = false;

    [Tooltip("机关移动 / 旋转的丝滑速度")]
    public float smoothSpeed = 10f;

    [Header("--- 旋转设置 Rotate ---")]
    [Tooltip("旋转轴。高塔一般用 Y，翻板可以用 X 或 Z")]
    public RotateAxis rotateAxis = RotateAxis.Y;

    [Tooltip("旋转输入方式。左右拖动转就选 HorizontalOnly，上下拖动转就选 VerticalOnly")]
    public InputMode rotateInputMode = InputMode.HorizontalOnly;

    [Tooltip("旋转灵敏度")]
    public float rotationSensitivity = 0.5f;

    [Tooltip("反转旋转方向")]
    public bool invertRotationDirection = false;

    [Tooltip("旋转吸附角度。例如 90 = 自动吸附到 0 / 90 / 180 / 270")]
    public float snapAngle = 90f;

    private float currentRotationAmount;
    private float targetRotationAmount;
    private Vector3 initialLocalEuler;

    [Header("--- 滑动设置 Slide ---")]
    [Tooltip("选择滑动方向：上下 / 左右 / 前后 / 自定义")]
    public SlideDirection slideDirection = SlideDirection.UpDown;

    [Tooltip("只有 Slide Direction = Custom 时才使用这个方向")]
    public Vector3 customSlideAxis = new Vector3(0f, 1f, 0f);

    [Tooltip("选择用左右拖动还是上下拖动来控制滑动")]
    public InputMode slideInputMode = InputMode.VerticalOnly;

    [Tooltip("滑动灵敏度")]
    public float slideSensitivity = 0.05f;

    [Tooltip("反转滑动方向")]
    public bool invertSlideDirection = false;

    [Header("--- 滑动吸附设置 Slide Snap ---")]
    [Tooltip("是否启用滑动吸附")]
    public bool useSlideSnap = true;

    [Tooltip("每一格的距离。例如 1 = 吸附到 0 / 1 / 2；0.5 = 吸附到 0 / 0.5 / 1 / 1.5")]
    public float slideSnapDistance = 1f;

    [Tooltip("滑动最小距离。单位是沿滑动方向的距离")]
    public float minPos = 0f;

    [Tooltip("滑动最大距离。单位是沿滑动方向的距离")]
    public float maxPos = 5f;

    private float currentSlideAmount;
    private float targetSlideAmount;
    private Vector3 initialLocalPosition;

    void Start()
    {
        if (targetObject == null)
            targetObject = transform.parent;

        if (targetObject == null)
        {
            Debug.LogError("Mechanism: targetObject 没有设置，并且当前物体也没有父物体。");
            enabled = false;
            return;
        }

        initialLocalEuler = targetObject.localEulerAngles;
        initialLocalPosition = targetObject.localPosition;

        if (type == MechanismType.Rotate)
        {
            currentRotationAmount = GetInitialAxisAngle();
            targetRotationAmount = currentRotationAmount;
        }
        else if (type == MechanismType.Slide)
        {
            currentSlideAmount = 0f;
            targetSlideAmount = 0f;
        }
    }

    void OnValidate()
    {
        smoothSpeed = Mathf.Max(0.01f, smoothSpeed);

        snapAngle = Mathf.Max(0.0001f, snapAngle);
        slideSnapDistance = Mathf.Max(0.0001f, slideSnapDistance);

        if (maxPos < minPos)
            maxPos = minPos;
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
    }

    public void ProcessDrag(Vector2 inputDelta)
    {
        if (isLocked)
            return;

        if (targetObject == null)
            return;

        if (type == MechanismType.Rotate)
        {
            ProcessRotate(inputDelta);
        }
        else if (type == MechanismType.Slide)
        {
            ProcessSlide(inputDelta);
        }
    }

    void ProcessRotate(Vector2 inputDelta)
    {
        float inputAmount = GetInputAmount(inputDelta, rotateInputMode);
        float direction = invertRotationDirection ? -1f : 1f;

        targetRotationAmount += inputAmount * rotationSensitivity * direction;
    }

    void ProcessSlide(Vector2 inputDelta)
    {
        float inputAmount = GetInputAmount(inputDelta, slideInputMode);
        float direction = invertSlideDirection ? -1f : 1f;

        targetSlideAmount += inputAmount * slideSensitivity * direction;
        targetSlideAmount = Mathf.Clamp(targetSlideAmount, minPos, maxPos);
    }

    public void EndDrag()
    {
        if (isLocked)
            return;

        if (type == MechanismType.Rotate)
        {
            SnapRotation();
        }
        else if (type == MechanismType.Slide)
        {
            SnapSlide();
        }
    }

    void SnapRotation()
    {
        if (snapAngle <= 0.0001f)
            return;

        targetRotationAmount =
            Mathf.Round(targetRotationAmount / snapAngle) * snapAngle;
    }

    void SnapSlide()
    {
        if (!useSlideSnap)
            return;

        if (slideSnapDistance <= 0.0001f)
            return;

        targetSlideAmount =
            Mathf.Round(targetSlideAmount / slideSnapDistance) * slideSnapDistance;

        targetSlideAmount = Mathf.Clamp(targetSlideAmount, minPos, maxPos);
    }

    void Update()
    {
        if (targetObject == null)
            return;

        if (type == MechanismType.Rotate)
        {
            UpdateRotate();
        }
        else if (type == MechanismType.Slide)
        {
            UpdateSlide();
        }
    }

    void UpdateRotate()
    {
        currentRotationAmount = Mathf.LerpAngle(
            currentRotationAmount,
            targetRotationAmount,
            Time.deltaTime * smoothSpeed
        );

        ApplyRotation();
    }

    void UpdateSlide()
    {
        currentSlideAmount = Mathf.Lerp(
            currentSlideAmount,
            targetSlideAmount,
            Time.deltaTime * smoothSpeed
        );

        Vector3 axis = GetSlideAxis();

        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.up;

        targetObject.localPosition =
            initialLocalPosition + axis.normalized * currentSlideAmount;
    }

    float GetInputAmount(Vector2 inputDelta, InputMode mode)
    {
        switch (mode)
        {
            case InputMode.HorizontalOnly:
                return inputDelta.x;

            case InputMode.VerticalOnly:
                return inputDelta.y;

            case InputMode.Combined:
                return inputDelta.x + inputDelta.y;

            default:
                return inputDelta.y;
        }
    }

    Vector3 GetSlideAxis()
    {
        switch (slideDirection)
        {
            case SlideDirection.UpDown:
                return Vector3.up;

            case SlideDirection.LeftRight:
                return Vector3.right;

            case SlideDirection.ForwardBack:
                return Vector3.forward;

            case SlideDirection.Custom:
                return customSlideAxis;

            default:
                return Vector3.up;
        }
    }

    float GetInitialAxisAngle()
    {
        Vector3 euler = targetObject.localEulerAngles;

        switch (rotateAxis)
        {
            case RotateAxis.X:
                return euler.x;

            case RotateAxis.Y:
                return euler.y;

            case RotateAxis.Z:
                return euler.z;

            default:
                return euler.y;
        }
    }

    void ApplyRotation()
    {
        Vector3 euler = initialLocalEuler;

        switch (rotateAxis)
        {
            case RotateAxis.X:
                euler.x = currentRotationAmount;
                break;

            case RotateAxis.Y:
                euler.y = currentRotationAmount;
                break;

            case RotateAxis.Z:
                euler.z = currentRotationAmount;
                break;
        }

        targetObject.localEulerAngles = euler;
    }

    public float GetCurrentSlideAmount()
    {
        return currentSlideAmount;
    }

    public float GetTargetSlideAmount()
    {
        return targetSlideAmount;
    }

    public float GetCurrentRotationAmount()
    {
        return currentRotationAmount;
    }

    public float GetTargetRotationAmount()
    {
        return targetRotationAmount;
    }
}