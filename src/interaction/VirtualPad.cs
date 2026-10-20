using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using IS = UnityEngine.InputSystem.InputSystem;

namespace VampireHuntersVR.Interaction;

// D-093: a virtual Xbox pad (Unity Input System "Gamepad" layout) fed from the controllers. The game's own action map
// (Move=leftStick, Jump=buttonSouth, Start=start, UI Navigate/Submit) then runs in its Gamepad scheme: analog 360-degree
// movement instead of WASD. No OS keyboard/mouse/XInput driver is touched; the device exists only inside the game process.
internal sealed class VirtualPad
{
    private const int EventSize = 24 + 28; // InputEvent base (20) + stateFormat (4) + GamepadState (28)
    private static int Four(char a, char b, char c, char d) => (a << 24) | (b << 16) | (c << 8) | d;
    private IntPtr buffer;
    private float lx = float.NaN, ly, rx, ry, lt, rt;
    private uint buttons = uint.MaxValue;
    public InputDevice? Device;
    public string? Error;
    public int Sent;
    public string Layout = "";

    public bool Create()
    {
        try
        {
            try { IS.settings.backgroundBehavior = (InputSettings.BackgroundBehavior)2; } catch (System.Exception e) { Error = "bg:" + e.GetType().Name; }
            try { IS.runInBackground = true; } catch (System.Exception) { }
            Device = IS.AddDevice("Gamepad", "VHVR Virtual Pad", null);
            if (Device == null) { Error = "AddDevice returned null"; return false; }
            Layout = "Gamepad id=" + Device.deviceId;
            buffer = Marshal.AllocHGlobal(EventSize);
            return true;
        }
        catch (System.Exception e) { Error = e.GetType().Name + ": " + e.Message; return false; }
    }

    // buttons use GamepadButton bit indices (South=6, Start=12, ...).
    public void Send(float leftX, float leftY, float rightX, float rightY, float leftTrigger, float rightTrigger, uint buttonBits)
    {
        if (Device == null || buffer == IntPtr.Zero) return;
        if (leftX == lx && leftY == ly && rightX == rx && rightY == ry && leftTrigger == lt && rightTrigger == rt && buttonBits == buttons) return;
        lx = leftX; ly = leftY; rx = rightX; ry = rightY; lt = leftTrigger; rt = rightTrigger; buttons = buttonBits;
        try
        {
            IntPtr b = buffer;
            Marshal.WriteInt32(b, 0, Four('S', 'T', 'A', 'T'));
            Marshal.WriteInt16(b, 4, (short)EventSize);
            Marshal.WriteInt16(b, 6, (short)Device.deviceId);
            Marshal.WriteInt64(b, 8, 0);
            Marshal.WriteInt32(b, 16, 0);
            Marshal.WriteInt32(b, 20, Four('G', 'P', 'A', 'D'));
            Marshal.WriteInt32(b, 24, unchecked((int)buttonBits));
            WriteFloat(b, 28, leftX); WriteFloat(b, 32, leftY);
            WriteFloat(b, 36, rightX); WriteFloat(b, 40, rightY);
            WriteFloat(b, 44, leftTrigger); WriteFloat(b, 48, rightTrigger);
            InputEventPtr ptr = default; System.Runtime.CompilerServices.Unsafe.As<InputEventPtr, IntPtr>(ref ptr) = b; IS.QueueEvent(ptr);
            Sent++;
        }
        catch (System.Exception e) { Error = e.GetType().Name + ": " + e.Message; }
    }

    private static void WriteFloat(IntPtr b, int offset, float v) => Marshal.WriteInt32(b, offset, BitConverter.SingleToInt32Bits(v));

    public void Remove()
    {
        try { Send(0, 0, 0, 0, 0, 0, 0); } catch (System.Exception) { }
        try { if (Device != null) IS.RemoveDevice(Device); } catch (System.Exception) { }
        Device = null;
        if (buffer != IntPtr.Zero) { Marshal.FreeHGlobal(buffer); buffer = IntPtr.Zero; }
    }
}