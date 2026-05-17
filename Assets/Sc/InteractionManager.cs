using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InteractionManager : MonoBehaviour
{
    [Header("核心设置")]
    public Camera movieCamera;
    public LayerMask interactableLayer;
    [HideInInspector] public bool canInteract = false;

    [Header("准星 UI (手柄虚拟光标)")]
    public RectTransform reticleRoot;
    public Image solidDiamond;
    public Image hollowDiamond;

    [Header("动画与操控参数")]
    public float normalSize = 40f;
    public float hoverSize = 25f;
    public float transitionSpeed = 15f;
    [Tooltip("手柄光标的移动速度")]
    public float cursorSpeed = 1000f;
    [Tooltip("鼠标光标的灵敏度")]
    public float mouseSensitivity = 1.0f;

    private Mechanism currentDraggingMechanism;
    private Vector2 virtualCursorPos;

    // 新增：状态机，记录当前正在使用什么设备
    private bool isUsingMouse = true;

    void Start()
    {
        // 隐藏真实鼠标
        Cursor.visible = false;
        // 强制解锁鼠标，防止被第一人称残留代码锁死
        Cursor.lockState = CursorLockMode.None;

        if (solidDiamond != null) solidDiamond.color = new Color(1, 1, 1, 1);
        if (hollowDiamond != null) hollowDiamond.color = new Color(1, 1, 1, 0);
        if (reticleRoot != null) reticleRoot.gameObject.SetActive(false);

        // 初始化在屏幕正中央
        virtualCursorPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
    }

    void Update()
    {
        if (!canInteract || !movieCamera.gameObject.activeInHierarchy)
        {
            if (reticleRoot != null && reticleRoot.gameObject.activeSelf) reticleRoot.gameObject.SetActive(false);
            return;
        }

        if (reticleRoot != null && !reticleRoot.gameObject.activeSelf) reticleRoot.gameObject.SetActive(true);

        // ==========================================
        // 1. 智能状态切换 (防打架系统)
        // ==========================================
        if (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f || Mouse.current.leftButton.wasPressedThisFrame))
        {
            isUsingMouse = true; // 只要鼠标动了一下，立刻切换到鼠标模式
        }
        else if (GameInputHub.RightStick.sqrMagnitude > 0.1f || GameInputHub.R2PressedThisFrame)
        {
            isUsingMouse = false;
        }

        // ==========================================
        // 2. 处理设备输入
        // ==========================================
        Vector2 inputDelta = Vector2.zero;
        bool isGrabPressed = false;
        bool wasGrabPressedThisFrame = false;
        bool wasGrabReleasedThisFrame = false;

        if (isUsingMouse && Mouse.current != null)
        {
            // --- 鼠标模式 ---
            // 核心修复：用 Delta (相对移动) 代替 Position，打破死锁！
            Vector2 mouseMove = Mouse.current.delta.ReadValue();
            virtualCursorPos += mouseMove * mouseSensitivity;

            inputDelta = mouseMove * 0.5f;

            isGrabPressed = Mouse.current.leftButton.isPressed;
            wasGrabPressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
            wasGrabReleasedThisFrame = Mouse.current.leftButton.wasReleasedThisFrame;
        }
        else if (!isUsingMouse)
        {
            Vector2 stickValue = GameInputHub.RightStick;
            virtualCursorPos += stickValue * cursorSpeed * Time.deltaTime;

            inputDelta = stickValue * 10f;

            isGrabPressed = GameInputHub.R2Held;
            wasGrabPressedThisFrame = GameInputHub.R2PressedThisFrame;
            wasGrabReleasedThisFrame = GameInputHub.R2ReleasedThisFrame;
        }
        // 限制虚拟光标不出屏幕边缘
        virtualCursorPos.x = Mathf.Clamp(virtualCursorPos.x, 0, Screen.width);
        virtualCursorPos.y = Mathf.Clamp(virtualCursorPos.y, 0, Screen.height);

        UpdateReticle(virtualCursorPos);

        // ==========================================
        // 3. 射线与拖拽交互逻辑 (完全复用)
        // ==========================================
        if (wasGrabPressedThisFrame)
        {
            Ray ray = movieCamera.ScreenPointToRay(virtualCursorPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, interactableLayer))
            {
                currentDraggingMechanism = hit.collider.GetComponent<Mechanism>();
            }
        }

        if (isGrabPressed && currentDraggingMechanism != null)
        {
            currentDraggingMechanism.ProcessDrag(inputDelta);
        }

        if (wasGrabReleasedThisFrame && currentDraggingMechanism != null)
        {
            currentDraggingMechanism.EndDrag();
            currentDraggingMechanism = null;
        }
    }

    void UpdateReticle(Vector2 pos)
    {
        if (reticleRoot == null || solidDiamond == null || hollowDiamond == null) return;

        reticleRoot.position = pos;

        Ray ray = movieCamera.ScreenPointToRay(pos);
        bool isHovering = Physics.Raycast(ray, out RaycastHit hit, 1000f, interactableLayer);

        bool isActive = isHovering || currentDraggingMechanism != null;

        float targetSize = isActive ? hoverSize : normalSize;
        reticleRoot.sizeDelta = Vector2.Lerp(reticleRoot.sizeDelta, new Vector2(targetSize, targetSize), Time.deltaTime * transitionSpeed);

        float targetSolidAlpha = isActive ? 0f : 1f;
        float targetHollowAlpha = isActive ? 1f : 0f;

        Color sColor = solidDiamond.color;
        sColor.a = Mathf.Lerp(sColor.a, targetSolidAlpha, Time.deltaTime * transitionSpeed);
        solidDiamond.color = sColor;

        Color hColor = hollowDiamond.color;
        hColor.a = Mathf.Lerp(hColor.a, targetHollowAlpha, Time.deltaTime * transitionSpeed);
        hollowDiamond.color = hColor;
    }
}