using MonitorControl;

public class DellKvmControllerTests
{
    [Fact]
    public void DisplayPort1() =>
        DdcCiController.SwitchInput(DdcCiController.InputSource.DisplayPort1);

    [Fact]
    public void HDMI1() =>
        DdcCiController.SwitchInput(DdcCiController.InputSource.HDMI1);
}

public class Usage
{
    public static void Foo()
    {
        // List all monitors
        var monitors = DdcCiController.GetMonitors();
        foreach (var monitor in monitors)
        {
            Console.WriteLine($"Monitor {monitor.Index}: {monitor.Description}");
        }

// Discover what your monitor supports
        DdcCiController.DiscoverCapabilities(0);

// Switch input
        DdcCiController.SwitchInput(DdcCiController.InputSource.HDMI1);
        DdcCiController.SwitchInput(DdcCiController.InputSource.DisplayPort1, monitorIndex: 1);

// Get current input
        var currentInput = DdcCiController.GetCurrentInput();
        Console.WriteLine($"Current input: {currentInput}");

// Switch KVM (video + USB together)
        DdcCiController.SwitchKvm(
            DdcCiController.InputSource.DisplayPort1,
            DdcCiController.UsbPort.Port1);

// Try USB switching (won't throw if unsupported)
        if (DdcCiController.TrySwitchUsb(DdcCiController.UsbPort.Port2))
        {
            Console.WriteLine("USB switched successfully");
        }

// Brightness control
        DdcCiController.SetBrightness(75);
        var brightness = DdcCiController.GetBrightness();

// Discover USB code for your monitor
        DdcCiController.DiscoverUsbCode(0);
    }
}