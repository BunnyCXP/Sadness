using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

[DefaultExecutionOrder(-1000)]
public class GameInputHub : MonoBehaviour
{
    public static GameInputHub Instance { get; private set; }

    public static Vector2 LeftStick => Instance != null ? Instance.leftStick : Vector2.zero;
    public static Vector2 RightStick => Instance != null ? Instance.rightStick : Vector2.zero;

    public static float R2Value => Instance != null ? Instance.r2Value : 0f;
    public static bool R2Held => Instance != null && Instance.r2Held;
    public static bool R2PressedThisFrame => Instance != null && Instance.r2PressedThisFrame;
    public static bool R2ReleasedThisFrame => Instance != null && Instance.r2ReleasedThisFrame;

    public static bool AHeld => Instance != null && Instance.aHeld;
    public static bool APressedThisFrame => Instance != null && Instance.aPressedThisFrame;

    public static bool TouchpadHeld => Instance != null && Instance.touchpadHeld;
    public static bool TouchpadPressedThisFrame => Instance != null && Instance.touchpadPressedThisFrame;

    [Header("Input Sources")]
    public bool useStandardGamepad = true;
    public bool useRazerRaijuHID = true;
    public bool useKeyboardDebug = true;

    [Header("Razer HID Device Match")]
    public string razerDeviceKeyword = "Razer Raiju";

    [Header("Razer HID Mapping")]
    public string razerR2Control = "button8";
    public string razerLeftStickControl = "stick";

    [Tooltip("右摇杆 X。你目前先用 z。")]
    public string razerRightStickXControl = "z";

    [Tooltip("右摇杆 Y。如果还没找到正确轴，可以先留空。")]
    public string razerRightStickYControl = "";

    [Tooltip("× / Confirm")]
    public string razerAControl = "button2";

    [Tooltip("Touchpad Press")]
    public string razerTouchpadControl = "button14";

    [Header("Razer Stick Invert")]
    public bool invertRazerLeftStickX = false;
    public bool invertRazerLeftStickY = true;

    public bool invertRazerRightStickX = false;
    public bool invertRazerRightStickY = true;

    [Header("Razer Right Stick Auto Calibration")]
    [Tooltip("开启后，启动时会把右摇杆当前原始值当成中心值，防止静止时输出 -1。")]
    public bool autoCalibrateRazerRightStick = true;

    [Tooltip("运行开始后多少秒内采样中心值。期间不要碰右摇杆。")]
    public float calibrationDuration = 0.5f;

    [Tooltip("右摇杆原始输入的最大范围。HID 常见是 1。")]
    public float razerRightStickRawRange = 1f;

    [Tooltip("运行时按这个键重新校准右摇杆。")]
    public Key recalibrateKey = Key.F9;

    [Header("Dead Zones")]
    public float leftStickDeadZone = 0.12f;
    public float rightStickDeadZone = 0.25f;
    public float triggerThreshold = 0.5f;

    [Header("Keyboard Debug")]
    public Key debugR2Key = Key.Space;
    public Key debugAKey = Key.Enter;
    public Key debugTouchpadKey = Key.Tab;

    [Header("Standard Gamepad Rumble")]
    public bool useStandardGamepadRumble = true;
    public bool rumbleAllStandardGamepads = true;

    [Header("Razer HID Rumble")]
    public bool useRazerHIDRumble = true;

    [Tooltip("你测出来 1~5 都能震。先用 1；如果手感不好再换 2/3/4/5。")]
    [Range(1, 8)]
    public int razerHIDRumblePattern = 1;

    [Header("Debug Read Only")]
    [SerializeField] private string currentRazerDeviceName;
    [SerializeField] private bool hasStandardGamepad;
    [SerializeField] private bool hasRazerDevice;
    [SerializeField] private bool canUseStandardRumble;
    [SerializeField] private long lastRazerHIDRumbleResult;

    [SerializeField] private Vector2 leftStick;
    [SerializeField] private Vector2 rightStick;
    [SerializeField] private float r2Value;
    [SerializeField] private bool r2Held;
    [SerializeField] private bool r2PressedThisFrame;
    [SerializeField] private bool r2ReleasedThisFrame;
    [SerializeField] private bool aHeld;
    [SerializeField] private bool aPressedThisFrame;
    [SerializeField] private bool touchpadHeld;
    [SerializeField] private bool touchpadPressedThisFrame;

    [Header("Razer Calibration Debug")]
    [SerializeField] private bool calibrationFinished;
    [SerializeField] private float calibrationTimer;
    [SerializeField] private float razerRightStickXCenter;
    [SerializeField] private float razerRightStickYCenter;
    [SerializeField] private float rawRazerRightStickX;
    [SerializeField] private float rawRazerRightStickY;

    private bool previousR2Held;
    private bool previousAHeld;
    private bool previousTouchpadHeld;

    private InputDevice razerDevice;
    private readonly Dictionary<string, InputControl> razerControlCache = new Dictionary<string, InputControl>();

    private float calibrationXSum;
    private float calibrationYSum;
    private int calibrationSampleCount;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        BeginRightStickCalibration();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[recalibrateKey].wasPressedThisFrame)
        {
            BeginRightStickCalibration();
        }

        previousR2Held = r2Held;
        previousAHeld = aHeld;
        previousTouchpadHeld = touchpadHeld;

        Vector2 combinedLeftStick = Vector2.zero;
        Vector2 combinedRightStick = Vector2.zero;
        float combinedR2Value = 0f;

        bool combinedR2Held = false;
        bool combinedAHeld = false;
        bool combinedTouchpadHeld = false;

        hasStandardGamepad = Gamepad.all.Count > 0;
        canUseStandardRumble = hasStandardGamepad && useStandardGamepadRumble;

        if (useStandardGamepad)
        {
            ReadStandardGamepads(
                ref combinedLeftStick,
                ref combinedRightStick,
                ref combinedR2Value,
                ref combinedR2Held,
                ref combinedAHeld
            );
        }

        if (useRazerRaijuHID)
        {
            ReadRazerRaijuHID(
                ref combinedLeftStick,
                ref combinedRightStick,
                ref combinedR2Value,
                ref combinedR2Held,
                ref combinedAHeld,
                ref combinedTouchpadHeld
            );
        }

        if (useKeyboardDebug)
        {
            ReadKeyboardDebug(
                ref combinedLeftStick,
                ref combinedRightStick,
                ref combinedR2Value,
                ref combinedR2Held,
                ref combinedAHeld,
                ref combinedTouchpadHeld
            );
        }

        leftStick = ApplyDeadZone(combinedLeftStick, leftStickDeadZone);
        rightStick = ApplyDeadZone(combinedRightStick, rightStickDeadZone);

        r2Value = Mathf.Clamp01(combinedR2Value);
        r2Held = combinedR2Held || r2Value >= triggerThreshold;
        aHeld = combinedAHeld;
        touchpadHeld = combinedTouchpadHeld;

        r2PressedThisFrame = r2Held && !previousR2Held;
        r2ReleasedThisFrame = !r2Held && previousR2Held;

        aPressedThisFrame = aHeld && !previousAHeld;
        touchpadPressedThisFrame = touchpadHeld && !previousTouchpadHeld;
    }

    void BeginRightStickCalibration()
    {
        calibrationFinished = false;
        calibrationTimer = 0f;

        calibrationXSum = 0f;
        calibrationYSum = 0f;
        calibrationSampleCount = 0;
    }

    void ReadStandardGamepads(
        ref Vector2 combinedLeftStick,
        ref Vector2 combinedRightStick,
        ref float combinedR2Value,
        ref bool combinedR2Held,
        ref bool combinedAHeld
    )
    {
        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad pad = Gamepad.all[i];

            if (pad == null)
                continue;

            Vector2 ls = pad.leftStick.ReadValue();
            Vector2 rs = pad.rightStick.ReadValue();

            if (ls.sqrMagnitude > combinedLeftStick.sqrMagnitude)
                combinedLeftStick = ls;

            if (rs.sqrMagnitude > combinedRightStick.sqrMagnitude)
                combinedRightStick = rs;

            float r2 = pad.rightTrigger.ReadValue();

            if (r2 > combinedR2Value)
                combinedR2Value = r2;

            combinedR2Held |= pad.rightTrigger.isPressed;
            combinedAHeld |= pad.buttonSouth.isPressed;
        }
    }

    void ReadRazerRaijuHID(
        ref Vector2 combinedLeftStick,
        ref Vector2 combinedRightStick,
        ref float combinedR2Value,
        ref bool combinedR2Held,
        ref bool combinedAHeld,
        ref bool combinedTouchpadHeld
    )
    {
        FindRazerDeviceIfNeeded();

        if (razerDevice == null)
            return;

        Vector2 rawLeftStick = ReadRazerVector2(razerLeftStickControl);

        if (invertRazerLeftStickX)
            rawLeftStick.x *= -1f;

        if (invertRazerLeftStickY)
            rawLeftStick.y *= -1f;

        if (rawLeftStick.sqrMagnitude > combinedLeftStick.sqrMagnitude)
            combinedLeftStick = rawLeftStick;

        rawRazerRightStickX = ReadRazerRawAxis(razerRightStickXControl);
        rawRazerRightStickY = ReadRazerRawAxis(razerRightStickYControl);

        UpdateRightStickCalibration(rawRazerRightStickX, rawRazerRightStickY);

        float rightX = CalibrateRawAxis(rawRazerRightStickX, razerRightStickXCenter);
        float rightY = CalibrateRawAxis(rawRazerRightStickY, razerRightStickYCenter);

        if (invertRazerRightStickX)
            rightX *= -1f;

        if (invertRazerRightStickY)
            rightY *= -1f;

        Vector2 rawRightStick = new Vector2(rightX, rightY);

        if (rawRightStick.sqrMagnitude > combinedRightStick.sqrMagnitude)
            combinedRightStick = rawRightStick;

        float r2 = ReadRazerButtonOrAxisValue(razerR2Control);

        if (r2 > combinedR2Value)
            combinedR2Value = r2;

        combinedR2Held |= r2 >= triggerThreshold;

        combinedAHeld |= ReadRazerButton(razerAControl);
        combinedTouchpadHeld |= ReadRazerButton(razerTouchpadControl);

        hasRazerDevice = true;
    }

    void UpdateRightStickCalibration(float rawX, float rawY)
    {
        if (!autoCalibrateRazerRightStick)
        {
            calibrationFinished = true;
            return;
        }

        if (calibrationFinished)
            return;

        calibrationTimer += Time.deltaTime;

        calibrationXSum += rawX;
        calibrationYSum += rawY;
        calibrationSampleCount++;

        if (calibrationTimer >= calibrationDuration)
        {
            if (calibrationSampleCount > 0)
            {
                razerRightStickXCenter = calibrationXSum / calibrationSampleCount;
                razerRightStickYCenter = calibrationYSum / calibrationSampleCount;
            }

            calibrationFinished = true;
        }
    }

    float CalibrateRawAxis(float rawValue, float center)
    {
        if (!calibrationFinished && autoCalibrateRazerRightStick)
            return 0f;

        float range = Mathf.Max(0.0001f, razerRightStickRawRange);
        float value = (rawValue - center) / range;

        return Mathf.Clamp(value, -1f, 1f);
    }

    void ReadKeyboardDebug(
        ref Vector2 combinedLeftStick,
        ref Vector2 combinedRightStick,
        ref float combinedR2Value,
        ref bool combinedR2Held,
        ref bool combinedAHeld,
        ref bool combinedTouchpadHeld
    )
    {
        if (Keyboard.current == null)
            return;

        Vector2 keyboardLeft = Vector2.zero;
        Vector2 keyboardRight = Vector2.zero;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            keyboardLeft.x -= 1f;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            keyboardLeft.x += 1f;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            keyboardLeft.y += 1f;

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            keyboardLeft.y -= 1f;

        if (Keyboard.current.jKey.isPressed)
            keyboardRight.x -= 1f;

        if (Keyboard.current.lKey.isPressed)
            keyboardRight.x += 1f;

        if (Keyboard.current.iKey.isPressed)
            keyboardRight.y += 1f;

        if (Keyboard.current.kKey.isPressed)
            keyboardRight.y -= 1f;

        if (keyboardLeft.sqrMagnitude > combinedLeftStick.sqrMagnitude)
            combinedLeftStick = keyboardLeft.normalized;

        if (keyboardRight.sqrMagnitude > combinedRightStick.sqrMagnitude)
            combinedRightStick = keyboardRight.normalized;

        if (Keyboard.current[debugR2Key].isPressed)
        {
            combinedR2Value = 1f;
            combinedR2Held = true;
        }

        combinedAHeld |= Keyboard.current[debugAKey].isPressed;
        combinedTouchpadHeld |= Keyboard.current[debugTouchpadKey].isPressed;
    }

    void FindRazerDeviceIfNeeded()
    {
        if (razerDevice != null && IsDeviceStillConnected(razerDevice))
            return;

        razerDevice = null;
        currentRazerDeviceName = "";
        hasRazerDevice = false;
        razerControlCache.Clear();

        string keyword = string.IsNullOrWhiteSpace(razerDeviceKeyword)
            ? "razer"
            : razerDeviceKeyword.ToLowerInvariant();

        for (int i = 0; i < InputSystem.devices.Count; i++)
        {
            InputDevice device = InputSystem.devices[i];

            string name = (
                device.displayName + " " +
                device.name + " " +
                device.layout + " " +
                device.description.product
            ).ToLowerInvariant();

            if (name.Contains(keyword))
            {
                razerDevice = device;
                currentRazerDeviceName = device.displayName;
                hasRazerDevice = true;
                BeginRightStickCalibration();
                return;
            }
        }
    }

    bool IsDeviceStillConnected(InputDevice deviceToCheck)
    {
        if (deviceToCheck == null)
            return false;

        for (int i = 0; i < InputSystem.devices.Count; i++)
        {
            if (InputSystem.devices[i] == deviceToCheck)
                return true;
        }

        return false;
    }

    Vector2 ReadRazerVector2(string controlName)
    {
        InputControl control = GetRazerControl(controlName);

        if (control is Vector2Control vector2)
            return vector2.ReadValue();

        return Vector2.zero;
    }

    float ReadRazerRawAxis(string controlName)
    {
        InputControl control = GetRazerControl(controlName);

        if (control is AxisControl axis)
            return axis.ReadValue();

        return 0f;
    }

    bool ReadRazerButton(string controlName)
    {
        InputControl control = GetRazerControl(controlName);

        if (control is ButtonControl button)
            return button.isPressed;

        if (control is AxisControl axis)
            return axis.ReadValue() >= triggerThreshold;

        return false;
    }

    float ReadRazerButtonOrAxisValue(string controlName)
    {
        InputControl control = GetRazerControl(controlName);

        if (control is ButtonControl button)
            return button.isPressed ? 1f : 0f;

        if (control is AxisControl axis)
            return Mathf.Clamp01(axis.ReadValue());

        return 0f;
    }

    InputControl GetRazerControl(string controlName)
    {
        if (razerDevice == null)
            return null;

        if (string.IsNullOrWhiteSpace(controlName))
            return null;

        if (razerControlCache.TryGetValue(controlName, out InputControl cachedControl))
        {
            if (cachedControl != null && cachedControl.device == razerDevice)
                return cachedControl;
        }

        for (int i = 0; i < razerDevice.allControls.Count; i++)
        {
            InputControl control = razerDevice.allControls[i];

            if (control.name == controlName || control.path.EndsWith("/" + controlName))
            {
                razerControlCache[controlName] = control;
                return control;
            }
        }

        return null;
    }

    Vector2 ApplyDeadZone(Vector2 value, float deadZone)
    {
        if (value.magnitude < deadZone)
            return Vector2.zero;

        return Vector2.ClampMagnitude(value, 1f);
    }

    public static void SetRumble(float low, float high)
    {
        if (Instance == null)
            return;

        low = Mathf.Clamp01(low);
        high = Mathf.Clamp01(high);

        if (Instance.useRazerHIDRumble)
        {
            Instance.SendRazerHIDRumble(low, high);
        }

        if (Instance.useStandardGamepadRumble)
        {
            if (Instance.rumbleAllStandardGamepads)
            {
                for (int i = 0; i < Gamepad.all.Count; i++)
                {
                    Gamepad pad = Gamepad.all[i];

                    if (pad != null)
                        pad.SetMotorSpeeds(low, high);
                }
            }
            else if (Gamepad.current != null)
            {
                Gamepad.current.SetMotorSpeeds(low, high);
            }
        }
    }

    public static void StopRumble()
    {
        if (Instance == null)
            return;

        if (Instance.useRazerHIDRumble)
        {
            Instance.SendRazerHIDRumble(0f, 0f);
        }

        for (int i = 0; i < Gamepad.all.Count; i++)
        {
            Gamepad pad = Gamepad.all[i];

            if (pad != null)
                pad.SetMotorSpeeds(0f, 0f);
        }
    }

    void SendRazerHIDRumble(float low, float high)
    {
        FindRazerDeviceIfNeeded();

        if (razerDevice == null)
            return;

        byte lowByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(low) * 255f);
        byte highByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(high) * 255f);

        HidOutput32Command command = HidOutput32Command.Create();

        ApplyRazerHIDRumblePattern(
            ref command,
            razerHIDRumblePattern,
            lowByte,
            highByte
        );

        lastRazerHIDRumbleResult = razerDevice.ExecuteCommand(ref command);
    }

    void ApplyRazerHIDRumblePattern(
        ref HidOutput32Command command,
        int pattern,
        byte low,
        byte high
    )
    {
        command.ClearData();

        switch (pattern)
        {
            case 1:
                command.b00 = 0x05;
                command.b01 = 0xFF;
                command.b02 = 0x04;
                command.b03 = 0x00;
                command.b04 = low;
                command.b05 = high;
                break;

            case 2:
                command.b00 = 0x05;
                command.b01 = 0xFF;
                command.b02 = 0x04;
                command.b03 = 0x00;
                command.b04 = high;
                command.b05 = low;
                break;

            case 3:
                command.b00 = 0x05;
                command.b01 = 0x01;
                command.b02 = 0x00;
                command.b03 = low;
                command.b04 = high;
                break;

            case 4:
                command.b00 = 0x05;
                command.b01 = 0x01;
                command.b02 = 0x04;
                command.b03 = 0x00;
                command.b04 = low;
                command.b05 = high;
                break;

            case 5:
                command.b00 = 0x05;
                command.b01 = 0xFF;
                command.b02 = 0x04;
                command.b03 = 0x00;
                command.b05 = low;
                command.b06 = high;
                break;

            case 6:
                command.b00 = 0x05;
                command.b01 = low;
                command.b02 = high;
                break;

            case 7:
                command.b00 = 0x05;
                command.b01 = 0x00;
                command.b02 = 0x00;
                command.b03 = 0x00;
                command.b04 = 0x00;
                command.b05 = low;
                command.b06 = high;
                break;

            case 8:
                command.b00 = 0x05;
                command.b01 = 0x00;
                command.b02 = low;
                command.b03 = high;
                command.b04 = 0x00;
                command.b05 = 0x00;
                break;
        }
    }

    void OnDisable()
    {
        StopRumble();
    }

    void OnDestroy()
    {
        StopRumble();
    }

    [StructLayout(LayoutKind.Explicit, Size = CommandSize)]
    public struct HidOutput32Command : IInputDeviceCommandInfo
    {
        public const int BaseCommandSize = 8;
        public const int DataSize = 32;
        public const int CommandSize = BaseCommandSize + DataSize;

        public static FourCC Type => new FourCC('H', 'I', 'D', 'O');

        [FieldOffset(0)]
        public InputDeviceCommand baseCommand;

        [FieldOffset(BaseCommandSize + 0)] public byte b00;
        [FieldOffset(BaseCommandSize + 1)] public byte b01;
        [FieldOffset(BaseCommandSize + 2)] public byte b02;
        [FieldOffset(BaseCommandSize + 3)] public byte b03;
        [FieldOffset(BaseCommandSize + 4)] public byte b04;
        [FieldOffset(BaseCommandSize + 5)] public byte b05;
        [FieldOffset(BaseCommandSize + 6)] public byte b06;
        [FieldOffset(BaseCommandSize + 7)] public byte b07;
        [FieldOffset(BaseCommandSize + 8)] public byte b08;
        [FieldOffset(BaseCommandSize + 9)] public byte b09;
        [FieldOffset(BaseCommandSize + 10)] public byte b10;
        [FieldOffset(BaseCommandSize + 11)] public byte b11;
        [FieldOffset(BaseCommandSize + 12)] public byte b12;
        [FieldOffset(BaseCommandSize + 13)] public byte b13;
        [FieldOffset(BaseCommandSize + 14)] public byte b14;
        [FieldOffset(BaseCommandSize + 15)] public byte b15;
        [FieldOffset(BaseCommandSize + 16)] public byte b16;
        [FieldOffset(BaseCommandSize + 17)] public byte b17;
        [FieldOffset(BaseCommandSize + 18)] public byte b18;
        [FieldOffset(BaseCommandSize + 19)] public byte b19;
        [FieldOffset(BaseCommandSize + 20)] public byte b20;
        [FieldOffset(BaseCommandSize + 21)] public byte b21;
        [FieldOffset(BaseCommandSize + 22)] public byte b22;
        [FieldOffset(BaseCommandSize + 23)] public byte b23;
        [FieldOffset(BaseCommandSize + 24)] public byte b24;
        [FieldOffset(BaseCommandSize + 25)] public byte b25;
        [FieldOffset(BaseCommandSize + 26)] public byte b26;
        [FieldOffset(BaseCommandSize + 27)] public byte b27;
        [FieldOffset(BaseCommandSize + 28)] public byte b28;
        [FieldOffset(BaseCommandSize + 29)] public byte b29;
        [FieldOffset(BaseCommandSize + 30)] public byte b30;
        [FieldOffset(BaseCommandSize + 31)] public byte b31;

        public FourCC typeStatic => Type;

        public static HidOutput32Command Create()
        {
            HidOutput32Command command = new HidOutput32Command();
            command.baseCommand = new InputDeviceCommand(Type, CommandSize);
            command.ClearData();
            return command;
        }

        public void ClearData()
        {
            b00 = b01 = b02 = b03 = b04 = b05 = b06 = b07 = 0;
            b08 = b09 = b10 = b11 = b12 = b13 = b14 = b15 = 0;
            b16 = b17 = b18 = b19 = b20 = b21 = b22 = b23 = 0;
            b24 = b25 = b26 = b27 = b28 = b29 = b30 = b31 = 0;
        }
    }
}