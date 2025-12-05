

    // Common input source values
    public enum InputSource : uint
    {
        VGA = 0x01,
        DVI1 = 0x03,
        DVI2 = 0x04,
        HDMI1 = 0x11,
        HDMI2 = 0x12,
        DisplayPort1 = 0x0F,
        DisplayPort2 = 0x10,
        USBC = 0x1B
    }
public class DellKvmController
{
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

    [StructLayout(LayoutKind.Sequential)]
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

    private const byte VCP_INPUT_SOURCE = 0x60;

    public static void SwitchInput(InputSource input, int monitorIndex = 0)
    {
        var monitors = GetPhysicalMonitors();

        if (monitorIndex >= monitors.Length)
            throw new ArgumentException($"Monitor index {monitorIndex} not found");

        var monitor = monitors[monitorIndex];

        try
        {
            if (!SetVCPFeature(monitor.hPhysicalMonitor, VCP_INPUT_SOURCE, (uint)input))
            {
                throw new InvalidOperationException(
                    $"Failed to set input source. Error: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            DestroyPhysicalMonitor(monitor.hPhysicalMonitor);
        }
    }

    public static InputSource GetCurrentInput(int monitorIndex = 0)
    {
        var monitors = GetPhysicalMonitors();

        if (monitorIndex >= monitors.Length)
            throw new ArgumentException($"Monitor index {monitorIndex} not found");

        var monitor = monitors[monitorIndex];

        try
        {
            if (GetVCPFeatureAndVCPFeatureReply(monitor.hPhysicalMonitor, VCP_INPUT_SOURCE,
                    out _, out var currentValue, out _))
            {
                return (InputSource)currentValue;
            }

            throw new InvalidOperationException("Failed to get current input source");
        }
        finally
        {
            DestroyPhysicalMonitor(monitor.hPhysicalMonitor);
        }
    }

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

}