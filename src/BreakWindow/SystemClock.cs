using BreakWindow.Core;

namespace BreakWindow;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
