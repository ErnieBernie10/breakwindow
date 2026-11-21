namespace BreakWindow;

internal static class Program
{
    private static int Main()
    {
        Windows.Win32.PInvoke.MessageBeep(0);
        return 0;
    }
}
