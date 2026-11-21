namespace BreakWindow.Core;

public interface IIdleSource
{
    TimeSpan IdleTime { get; }
}
