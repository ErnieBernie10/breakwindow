namespace BreakWindow;

internal static class Program
{
    private static int Main()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return 1;
        }

        using var mutex = new Mutex(true, @"Local\breakwindow", out var isFirst);
        if (!isFirst)
        {
            return 0;
        }

        return new App().Run();
    }
}
