public class DellKvmControllerTests
{
    [Fact]
    public void DisplayPort1() =>
        DellKvmController.SwitchInput(InputSource.DisplayPort1);

    [Fact]
    public void HDMI1() =>
        DellKvmController.SwitchInput(InputSource.HDMI1);
}