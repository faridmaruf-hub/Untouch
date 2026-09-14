namespace RawHidDiag;

static class Program
{
    [STAThread]
    static void Main()
    {
        Native.AllocConsole();
        ApplicationConfiguration.Initialize();
        Application.Run(new ProbeForm());
    }
}
