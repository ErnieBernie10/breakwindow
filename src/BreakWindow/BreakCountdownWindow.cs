using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.DirectWrite;

namespace BreakWindow;

[SupportedOSPlatform("windows10.0.17763")]
internal sealed class BreakCountdownWindow : Popup
{
    private const float Width = 280;
    private const float Height = 168;

    private readonly PopupButton[] buttons;
    private string remaining = "";

    public BreakCountdownWindow(Action cancel)
    {
        buttons = [new PopupButton((Width - 120) / 2, Height - 20 - 32, 120, 32, "Cancel", false, cancel)];
    }

    protected override float WidthDips => Width;

    protected override float HeightDips => Height;

    protected override IReadOnlyList<PopupButton> Buttons => buttons;

    public void SetRemaining(string text)
    {
        if (text != remaining)
        {
            remaining = text;
            Invalidate();
        }
    }

    protected override RECT Place(RECT workArea, int width, int height)
    {
        var left = workArea.left + ((workArea.Width - width) / 2);
        var top = workArea.top + ((workArea.Height - height) / 2);
        return new RECT(left, top, left + width, top + height);
    }

    protected override void Draw()
    {
        DrawText(remaining, 0, 20, Width, 72, DisplayFont, 56, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_SEMI_BOLD, Colors.TextPrimary, DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);
    }
}
