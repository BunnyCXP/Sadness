using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

public class RazerHIDRumbleTester : MonoBehaviour
{
    [Header("Device Match")]
    public string deviceKeyword = "Razer Raiju";

    [Header("Test Strength")]
    [Range(0, 255)] public int lowMotor = 120;
    [Range(0, 255)] public int highMotor = 220;

    [Header("Debug")]
    [SerializeField] private string targetDeviceName;
    [SerializeField] private long lastCommandResult;
    [SerializeField] private int lastPattern;

    private InputDevice targetDevice;

    void Update()
    {
        FindDeviceIfNeeded();

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame) TestPattern(1);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) TestPattern(2);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) TestPattern(3);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) TestPattern(4);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) TestPattern(5);
        if (Keyboard.current.digit6Key.wasPressedThisFrame) TestPattern(6);
        if (Keyboard.current.digit7Key.wasPressedThisFrame) TestPattern(7);
        if (Keyboard.current.digit8Key.wasPressedThisFrame) TestPattern(8);

        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            StopAllPatterns();
        }
    }

    void FindDeviceIfNeeded()
    {
        if (targetDevice != null && IsDeviceStillConnected(targetDevice))
            return;

        targetDevice = null;
        targetDeviceName = "";

        string keyword = string.IsNullOrWhiteSpace(deviceKeyword)
            ? "razer"
            : deviceKeyword.ToLowerInvariant();

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
                targetDevice = device;
                targetDeviceName = device.displayName;
                Debug.Log("Razer HID rumble target found: " + targetDeviceName);
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

    void TestPattern(int pattern)
    {
        if (targetDevice == null)
        {
            Debug.LogWarning("No Razer HID device found.");
            return;
        }

        byte low = (byte)Mathf.Clamp(lowMotor, 0, 255);
        byte high = (byte)Mathf.Clamp(highMotor, 0, 255);

        HidOutput32Command command = HidOutput32Command.Create();

        ApplyPattern(ref command, pattern, low, high);

        lastPattern = pattern;
        lastCommandResult = targetDevice.ExecuteCommand(ref command);

        Debug.Log(
            $"HID rumble test pattern {pattern}, result = {lastCommandResult}. " +
            "如果手柄震了，记住这个 Pattern 编号。"
        );
    }

    void StopAllPatterns()
    {
        if (targetDevice == null)
            return;

        for (int pattern = 1; pattern <= 8; pattern++)
        {
            HidOutput32Command command = HidOutput32Command.Create();
            ApplyPattern(ref command, pattern, 0, 0);
            long result = targetDevice.ExecuteCommand(ref command);

            Debug.Log($"Stop pattern {pattern}, result = {result}");
        }
    }

    void ApplyPattern(ref HidOutput32Command command, int pattern, byte low, byte high)
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
        StopAllPatterns();
    }

    void OnDestroy()
    {
        StopAllPatterns();
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