namespace BreakWindow.Core;

public interface IClock
{
    DateTimeOffset Now { get; }
}
