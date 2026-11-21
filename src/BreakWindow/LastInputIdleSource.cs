using BreakWindow.Core;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace BreakWindow;

internal sealed unsafe class LastInputIdleSource : IIdleSource
{
    public TimeSpan IdleTime
    {
        get
        {
            var info = new LASTINPUTINFO { cbSize = (uint)sizeof(LASTINPUTINFO) };
            if (!PInvoke.GetLastInputInfo(&info))
            {
                return TimeSpan.Zero;
            }

            return TimeSpan.FromMilliseconds(unchecked(PInvoke.GetTickCount() - info.dwTime));
        }
    }
}
