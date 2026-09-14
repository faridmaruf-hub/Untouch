namespace Untouch;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "Untouch.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Untouch is already running (check the system tray).",
                "Untouch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayAppContext());
    }
}
