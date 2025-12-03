using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MonitorControl;

/// <summary>
/// Controls monitors via DDC/CI (Display Data Channel Command Interface).
/// Works with any monitor that supports the VESA MCCS standard.
/// </summary>
public class DdcCiController
{
    #region P/Invoke Declarations

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(
        IntPtr hMonitor, uint dwPhysicalMonitorArraySize,
        [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetVCPFeature(IntPtr hMonitor, byte bVCPCode, uint dwNewValue);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(
        IntPtr hMonitor, byte bVCPCode, out uint pvct,
        out uint pdwCurrentValue, out uint pdwMaximumValue);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor,
        ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    #endregion

    #region VCP Codes (VESA MCCS Standard)

    // Standard VCP codes
    private const byte VCP_INPUT_SOURCE = 0x60;
    private const byte VCP_BRIGHTNESS = 0x10;
    private const byte VCP_CONTRAST = 0x12;
    //private const byte VCP_COLOR_TEMPERATURE = 0x14;
    private const byte VCP_POWER_MODE = 0xD6;
    // private const byte VCP_AUDIO_VOLUME = 0x62;
    // private const byte VCP_AUDIO_MUTE = 0x8D;

    // Common manufacturer-specific USB/KVM codes (0xE0-0xFF reserved for manufacturers)
    private const byte VCP_USB_SWITCH_DELL = 0xE0;
    private const byte VCP_KVM_SWITCH_DELL_ALT = 0xE1;
    private const byte VCP_USB_SWITCH_HP = 0xE2;
    private const byte VCP_USB_SWITCH_LG = 0xE1;

    #endregion

    #region Enums

    /// <summary>
    /// Standard MCCS input source values (VCP code 0x60)
    /// </summary>
    public enum InputSource : uint
    {
        Analog1 = 0x01,      // VGA
        Analog2 = 0x02,
        DVI1 = 0x03,
        DVI2 = 0x04,
        Composite1 = 0x05,
        Composite2 = 0x06,
        SVideo1 = 0x07,
        SVideo2 = 0x08,
        Tuner1 = 0x09,
        Tuner2 = 0x0A,
        Tuner3 = 0x0B,
        ComponentYPrPb1 = 0x0C,
        ComponentYPrPb2 = 0x0D,
        ComponentYPrPb3 = 0x0E,
        DisplayPort1 = 0x0F,
        DisplayPort2 = 0x10,
        HDMI1 = 0x11,
        HDMI2 = 0x12,
        HDMI3 = 0x13,
        HDMI4 = 0x14,
        UsbC = 0x1B
    }

    /// <summary>
    /// USB port selection for KVM monitors
    /// </summary>
    public enum UsbPort : uint
    {
        Port1 = 0x01,
        Port2 = 0x02,
        Port3 = 0x03,
        Port4 = 0x04,
        Auto = 0xFF  // Follow video input (if supported)
    }

    /// <summary>
    /// Power mode values (VCP code 0xD6)
    /// </summary>
    public enum PowerMode : uint
    {
        On = 0x01,
        Standby = 0x02,
        Suspend = 0x03,
        Off = 0x04
    }

    #endregion

    #region Public Methods - Input Switching

    /// <summary>
    /// Switch the monitor to a specific input source
    /// </summary>
    public static void SwitchInput(InputSource input, int monitorIndex = 0)
    {
        using var monitor = GetMonitor(monitorIndex);

        if (!SetVCPFeature(monitor.Handle, VCP_INPUT_SOURCE, (uint)input))
        {
            throw new InvalidOperationException(
                $"Failed to set input source. Error: {Marshal.GetLastWin32Error()}");
        }
    }

    /// <summary>
    /// Get the current input source
    /// </summary>
    public static InputSource GetCurrentInput(int monitorIndex = 0)
    {
        using var monitor = GetMonitor(monitorIndex);

        if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, VCP_INPUT_SOURCE,
            out _, out var currentValue, out _))
        {
            return (InputSource)currentValue;
        }

        throw new InvalidOperationException("Failed to get current input source");
    }

    #endregion

    #region Public Methods - USB/KVM Switching

    /// <summary>
    /// Switch USB routing on KVM monitors
    /// </summary>
    /// <param name="port">USB port to switch to</param>
    /// <param name="monitorIndex">Monitor index (0-based)</param>
    /// <param name="usbCodesToTry">Optional array of VCP codes to try. If null, tries common codes.</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool TrySwitchUsb(UsbPort port, int monitorIndex = 0, params byte[] usbCodesToTry)
    {
        if (usbCodesToTry == null || usbCodesToTry.Length == 0)
        {
            // Try common manufacturer codes
            usbCodesToTry = new byte[]
            {
                VCP_USB_SWITCH_DELL,
                VCP_KVM_SWITCH_DELL_ALT,
                VCP_USB_SWITCH_HP,
                VCP_USB_SWITCH_LG
            };
        }

        using var monitor = GetMonitor(monitorIndex);

        foreach (var code in usbCodesToTry)
        {
            if (SetVCPFeature(monitor.Handle, code, (uint)port))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Switch USB routing, throwing an exception on failure
    /// </summary>
    public static void SwitchUsb(UsbPort port, int monitorIndex = 0, params byte[] usbCodesToTry)
    {
        if (!TrySwitchUsb(port, monitorIndex, usbCodesToTry))
        {
            throw new InvalidOperationException(
                "Failed to switch USB. Monitor may not support USB switching via DDC/CI.");
        }
    }

    /// <summary>
    /// Get the current USB port (tries common codes)
    /// </summary>
    public static UsbPort? GetCurrentUsbPort(int monitorIndex = 0)
    {
        using var monitor = GetMonitor(monitorIndex);

        var codesToTry = new byte[]
        {
            VCP_USB_SWITCH_DELL,
            VCP_KVM_SWITCH_DELL_ALT,
            VCP_USB_SWITCH_HP,
            VCP_USB_SWITCH_LG
        };

        foreach (var code in codesToTry)
        {
            if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, code,
                out _, out var currentValue, out _))
            {
                return (UsbPort)currentValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Switch both video input and USB together (common KVM use case)
    /// </summary>
    public static void SwitchKvm(InputSource videoInput, UsbPort usbPort, int monitorIndex = 0)
    {
        SwitchInput(videoInput, monitorIndex);
        Thread.Sleep(100); // Small delay for monitor to process
        TrySwitchUsb(usbPort, monitorIndex);
    }

    #endregion

    #region Public Methods - Display Settings

    /// <summary>
    /// Set monitor brightness (0-100)
    /// </summary>
    public static void SetBrightness(byte brightness, int monitorIndex = 0)
    {
        if (brightness > 100)
            throw new ArgumentException("Brightness must be 0-100");

        using var monitor = GetMonitor(monitorIndex);

        if (!SetVCPFeature(monitor.Handle, VCP_BRIGHTNESS, brightness))
        {
            throw new InvalidOperationException("Failed to set brightness");
        }
    }

    /// <summary>
    /// Get current brightness (0-100)
    /// </summary>
    public static byte GetBrightness(int monitorIndex = 0)
    {
        using var monitor = GetMonitor(monitorIndex);

        if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, VCP_BRIGHTNESS,
            out _, out var currentValue, out _))
        {
            return (byte)currentValue;
        }

        throw new InvalidOperationException("Failed to get brightness");
    }

    /// <summary>
    /// Set monitor contrast (0-100)
    /// </summary>
    public static void SetContrast(byte contrast, int monitorIndex = 0)
    {
        if (contrast > 100)
            throw new ArgumentException("Contrast must be 0-100");

        using var monitor = GetMonitor(monitorIndex);

        if (!SetVCPFeature(monitor.Handle, VCP_CONTRAST, contrast))
        {
            throw new InvalidOperationException("Failed to set contrast");
        }
    }

    /// <summary>
    /// Get current contrast (0-100)
    /// </summary>
    public static byte GetContrast(int monitorIndex = 0)
    {
        using var monitor = GetMonitor(monitorIndex);

        if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, VCP_CONTRAST,
            out _, out var currentValue, out _))
        {
            return (byte)currentValue;
        }

        throw new InvalidOperationException("Failed to get contrast");
    }

    /// <summary>
    /// Set monitor power mode
    /// </summary>
    public static void SetPowerMode(PowerMode mode, int monitorIndex = 0)
    {
        using var monitor = GetMonitor(monitorIndex);

        if (!SetVCPFeature(monitor.Handle, VCP_POWER_MODE, (uint)mode))
        {
            throw new InvalidOperationException("Failed to set power mode");
        }
    }

    #endregion

    #region Public Methods - Discovery

    /// <summary>
    /// Get information about all connected monitors
    /// </summary>
    public static List<MonitorInfo> GetMonitors()
    {
        var monitors = GetPhysicalMonitors();
        var result = new List<MonitorInfo>();

        for (var i = 0; i < monitors.Length; i++)
        {
            result.Add(new MonitorInfo
            {
                Index = i,
                Description = monitors[i].szPhysicalMonitorDescription,
                Handle = monitors[i].hPhysicalMonitor
            });
        }

        return result;
    }

    /// <summary>
    /// Discover what VCP codes a monitor supports
    /// </summary>
    public static void DiscoverCapabilities(int monitorIndex = 0)
    {
        using var monitor = GetMonitor(monitorIndex);

        Console.WriteLine($"Monitor: {monitor.Description}");
        Console.WriteLine($"Index: {monitorIndex}");
        Console.WriteLine("\n=== Standard VCP Codes ===");

        var standardCodes = new (byte code, string name)[]
        {
            (0x10, "Brightness"),
            (0x12, "Contrast"),
            (0x14, "Color Temperature"),
            (0x60, "Input Source"),
            (0x62, "Audio Volume"),
            (0x8D, "Audio Mute"),
            (0xD6, "Power Mode")
        };

        foreach (var (code, name) in standardCodes)
        {
            if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, code,
                out _, out var current, out var max))
            {
                Console.WriteLine($"  0x{code:X2} {name,-20}: Current={current}, Max={max}");
            }
        }

        Console.WriteLine("\n=== Manufacturer-Specific Codes (0xE0-0xFF) ===");
        for (byte code = 0xE0; code <= 0xFF; code++)
        {
            if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, code,
                out _, out var current, out var max))
            {
                Console.WriteLine($"  0x{code:X2}: Current={current}, Max={max}");
            }
        }
    }

    /// <summary>
    /// Discover which VCP code controls USB switching
    /// </summary>
    public static void DiscoverUsbCode(int monitorIndex = 0)
    {
        Console.WriteLine("Testing manufacturer-specific USB/KVM codes...");
        Console.WriteLine("Watch your monitor's USB device switching during this test.\n");

        using var monitor = GetMonitor(monitorIndex);

        for (byte code = 0xE0; code <= 0xFF; code++)
        {
            // First check if code is readable
            if (GetVCPFeatureAndVCPFeatureReply(monitor.Handle, code,
                out _, out var originalValue, out var max))
            {
                Console.WriteLine($"Testing code 0x{code:X2} (current value: {originalValue}, max: {max})");

                // Try toggling between values
                for (uint testValue = 1; testValue <= Math.Min(4, max); testValue++)
                {
                    if (testValue == originalValue) continue;

                    if (SetVCPFeature(monitor.Handle, code, testValue))
                    {
                        Console.WriteLine($"  Set value {testValue} - Did USB devices switch? (y/n)");
                        System.Threading.Thread.Sleep(1500);
                    }
                }

                // Restore original value
                SetVCPFeature(monitor.Handle, code, originalValue);
                Console.WriteLine($"  Restored to {originalValue}\n");
            }
        }
    }

    #endregion

    #region Helper Methods

    private static PHYSICAL_MONITOR[] GetPhysicalMonitors()
    {
        var physicalMonitors = new List<PHYSICAL_MONITOR>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var monitorCount))
                {
                    var monitors = new PHYSICAL_MONITOR[monitorCount];
                    if (GetPhysicalMonitorsFromHMONITOR(hMonitor, monitorCount, monitors))
                    {
                        physicalMonitors.AddRange(monitors);
                    }
                }
                return true;
            }, IntPtr.Zero);

        return physicalMonitors.ToArray();
    }

    private static MonitorHandle GetMonitor(int monitorIndex)
    {
        var monitors = GetPhysicalMonitors();

        if (monitorIndex < 0 || monitorIndex >= monitors.Length)
            throw new ArgumentException($"Monitor index {monitorIndex} not found. Available monitors: 0-{monitors.Length - 1}");

        var monitor = monitors[monitorIndex];
        return new MonitorHandle(monitor.hPhysicalMonitor, monitor.szPhysicalMonitorDescription);
    }

    #endregion

    #region Helper Classes

    private class MonitorHandle : IDisposable
    {
        public IntPtr Handle { get; }
        public string Description { get; }

        public MonitorHandle(IntPtr handle, string description)
        {
            Handle = handle;
            Description = description;
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyPhysicalMonitor(Handle);
            }
        }
    }

    public class MonitorInfo
    {
        public int Index { get; set; }
        public string Description { get; set; } = string.Empty;
        public IntPtr Handle { get; set; }
    }

    #endregion
}