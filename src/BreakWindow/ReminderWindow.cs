using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.DirectWrite;

namespace BreakWindow;

[SupportedOSPlatform("windows10.0.17763")]
internal sealed class ReminderWindow : Popup
{
    private const float Width = 340;
    private const float Height = 88;
    private const float Padding = 20;

    private readonly PopupButton[] buttons;
    private string overdue = "";

    public ReminderWindow(Action startBreak)
    {
        buttons = [new PopupButton(Width - Padding - 120, (Height - 32) / 2, 120, 32, "Start break", true, startBreak)];
    }

    protected override float WidthDips => Width;

    protected override float HeightDips => Height;

    protected override IReadOnlyList<PopupButton> Buttons => buttons;

    public void SetOverdue(string text)
    {
        if (text != overdue)
        {
            overdue = text;
            Invalidate();
        }
    }

    protected override RECT Place(RECT workArea, int width, int height)
    {
        var margin = (int)MathF.Round(Margin * width / Width);
        var left = workArea.right - width - margin;
        var top = workArea.bottom - height - margin;
        return new RECT(left, top, left + width, top + height);
    }

    protected override void Draw()
    {
        var textWidth = Width - (Padding * 3) - 120;
        DrawText("Break due", Padding, 16, textWidth, 28, DisplayFont, 20, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_SEMI_BOLD, Colors.TextPrimary);
        DrawText(overdue, Padding, 46, textWidth, 22, TextFont, 14, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL, Colors.TextSecondary);
    }
}
