using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct2D;
using Windows.Win32.Graphics.Direct2D.Common;
using Windows.Win32.Graphics.DirectWrite;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace BreakWindow;

internal readonly record struct PopupButton(float X, float Y, float Width, float Height, string Label, bool Primary, Action Click)
{
    public bool Contains(float x, float y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

[SupportedOSPlatform("windows10.0.17763")]
internal abstract unsafe class Popup
{
    protected const string TextFont = "Segoe UI Variable Text";
    protected const string DisplayFont = "Segoe UI Variable Display";
    protected const float Margin = 12;

    private const string ClassName = "breakwindow.popup";
    private static readonly HWND Topmost = (HWND)(-1);
    private static bool registered;

    private readonly Dictionary<(string Family, float Size, DWRITE_FONT_WEIGHT Weight, DWRITE_TEXT_ALIGNMENT Align), nint> formats = [];
    private HWND hwnd;
    private GCHandle self;
    private uint dpi = 96;
    private bool backdrop;
    private bool dark;
    private Palette palette;
    private ID2D1HwndRenderTarget* target;
    private ID2D1SolidColorBrush* brush;
    private int hovered = -1;
    private int pressed = -1;
    private bool trackingLeave;

    public bool IsOpen => !hwnd.IsNull;

    protected abstract float WidthDips { get; }

    protected abstract float HeightDips { get; }

    protected abstract IReadOnlyList<PopupButton> Buttons { get; }

    protected Palette Colors => palette;

    public void Show(bool darkMode)
    {
        if (IsOpen)
        {
            return;
        }

        Register();
        Graphics.Acquire();
        dark = darkMode;
        palette = Theme.Palette(dark);
        var bounds = Bounds(out dpi);
        self = GCHandle.Alloc(this);
        fixed (char* className = ClassName)
        {
            hwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
                className,
                className,
                WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_CAPTION,
                bounds.left,
                bounds.top,
                bounds.Width,
                bounds.Height,
                HWND.Null,
                HMENU.Null,
                Module,
                (void*)GCHandle.ToIntPtr(self));
        }

        if (hwnd.IsNull)
        {
            self.Free();
            Graphics.Release();
            return;
        }

        ApplyWindowAttributes();
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        PInvoke.DwmExtendFrameIntoClientArea(hwnd, &margins);
        PInvoke.SetWindowPos(hwnd, Topmost, bounds.left, bounds.top, bounds.Width, bounds.Height, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        PInvoke.DefWindowProc(hwnd, PInvoke.WM_NCACTIVATE, 1, 0);
    }

    public void Hide()
    {
        if (IsOpen)
        {
            PInvoke.DestroyWindow(hwnd);
        }
    }

    public void SetTheme(bool darkMode)
    {
        if (!IsOpen || darkMode == dark)
        {
            return;
        }

        dark = darkMode;
        palette = Theme.Palette(dark);
        ApplyWindowAttributes();
        Invalidate();
    }

    public void Reposition()
    {
        if (!IsOpen)
        {
            return;
        }

        var bounds = Bounds(out var monitorDpi);
        if (monitorDpi != dpi)
        {
            dpi = monitorDpi;
            ReleaseTarget();
        }

        PInvoke.SetWindowPos(hwnd, Topmost, bounds.left, bounds.top, bounds.Width, bounds.Height, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        Invalidate();
    }

    protected void Invalidate()
    {
        if (IsOpen)
        {
            PInvoke.InvalidateRect(hwnd, (RECT*)null, false);
        }
    }

    protected abstract RECT Place(RECT workArea, int width, int height);

    protected abstract void Draw();

    protected void DrawText(string text, float x, float y, float width, float height, string family, float size, DWRITE_FONT_WEIGHT weight, D2D1_COLOR_F color, DWRITE_TEXT_ALIGNMENT align = DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_LEADING)
    {
        var format = Format(family, size, weight, align);
        var rect = new D2D_RECT_F { left = x, top = y, right = x + width, bottom = y + height };
        brush->SetColor(&color);
        fixed (char* p = text)
        {
            target->DrawText(p, (uint)text.Length, format, &rect, (ID2D1Brush*)brush, D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_NONE, DWRITE_MEASURING_MODE.DWRITE_MEASURING_MODE_NATURAL);
        }
    }

    private static HINSTANCE Module => (HINSTANCE)(nint)PInvoke.GetModuleHandle((PCWSTR)null).Value;

    private static void Register()
    {
        if (registered)
        {
            return;
        }

        registered = true;
        fixed (char* className = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = Module,
                hCursor = PInvoke.LoadCursor(HINSTANCE.Null, PInvoke.IDC_ARROW),
                lpszClassName = className,
            };
            PInvoke.RegisterClassEx(&wc);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == PInvoke.WM_NCCREATE)
        {
            var create = (CREATESTRUCTW*)lParam.Value;
            PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_USERDATA, (nint)create->lpCreateParams);
        }

        var handle = PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWLP_USERDATA);
        if (handle != 0 && GCHandle.FromIntPtr(handle).Target is Popup popup)
        {
            return popup.HandleMessage(hwnd, msg, wParam, lParam);
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private LRESULT HandleMessage(HWND window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case PInvoke.WM_NCCALCSIZE when wParam.Value != 0:
                return default;

            case PInvoke.WM_NCHITTEST:
                return (LRESULT)(nint)PInvoke.HTCLIENT;

            case PInvoke.WM_NCACTIVATE:
                return PInvoke.DefWindowProc(window, msg, 1, lParam);

            case PInvoke.WM_MOUSEACTIVATE:
                return (LRESULT)(nint)PInvoke.MA_NOACTIVATE;

            case PInvoke.WM_ERASEBKGND:
                return (LRESULT)1;

            case PInvoke.WM_PAINT:
                PAINTSTRUCT ps;
                PInvoke.BeginPaint(window, &ps);
                Render();
                PInvoke.EndPaint(window, &ps);
                return default;

            case PInvoke.WM_SIZE:
                if (target is not null)
                {
                    var size = ClientSize();
                    target->Resize(&size);
                }

                return default;

            case PInvoke.WM_DPICHANGED:
                Reposition();
                return default;

            case PInvoke.WM_MOUSEMOVE:
                OnMouseMove(lParam);
                return default;

            case PInvoke.WM_MOUSELEAVE:
                trackingLeave = false;
                SetHovered(-1);
                return default;

            case PInvoke.WM_LBUTTONDOWN:
                pressed = HitTest(lParam);
                if (pressed >= 0)
                {
                    PInvoke.SetCapture(window);
                    Invalidate();
                }

                return default;

            case PInvoke.WM_LBUTTONUP:
                var clicked = pressed >= 0 && HitTest(lParam) == pressed ? pressed : -1;
                pressed = -1;
                PInvoke.ReleaseCapture();
                Invalidate();
                if (clicked >= 0)
                {
                    Buttons[clicked].Click();
                }

                return default;

            case PInvoke.WM_CLOSE:
                return default;

            case PInvoke.WM_SYSCOMMAND when (wParam.Value & 0xFFF0) == PInvoke.SC_CLOSE:
                return default;

            case PInvoke.WM_NCDESTROY:
                OnDestroyed(window);
                return PInvoke.DefWindowProc(window, msg, wParam, lParam);
        }

        return PInvoke.DefWindowProc(window, msg, wParam, lParam);
    }

    private void OnDestroyed(HWND window)
    {
        PInvoke.SetWindowLongPtr(window, WINDOW_LONG_PTR_INDEX.GWLP_USERDATA, 0);
        ReleaseTarget();
        hwnd = HWND.Null;
        hovered = -1;
        pressed = -1;
        trackingLeave = false;
        self.Free();
        Graphics.Release();
    }

    private void ApplyWindowAttributes()
    {
        BOOL useDark = dark;
        PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &useDark, (uint)sizeof(BOOL));
        var corners = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &corners, sizeof(DWM_WINDOW_CORNER_PREFERENCE));
        var backdropType = DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
        backdrop = PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, &backdropType, sizeof(DWM_SYSTEMBACKDROP_TYPE)).Succeeded;
    }

    private RECT Bounds(out uint monitorDpi)
    {
        var monitor = PInvoke.MonitorFromPoint(default, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        PInvoke.GetMonitorInfo(monitor, &info);
        uint dpiX, dpiY;
        if (PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, &dpiX, &dpiY).Failed)
        {
            dpiX = 96;
        }

        monitorDpi = dpiX;
        var scale = dpiX / 96f;
        return Place(info.rcWork, (int)MathF.Ceiling(WidthDips * scale), (int)MathF.Ceiling(HeightDips * scale));
    }

    protected int Scale(float dips) => (int)MathF.Round(dips * dpi / 96f);

    private void Render()
    {
        if (!EnsureTarget())
        {
            return;
        }

        target->BeginDraw();
        var clear = backdrop ? default : palette.Background;
        target->Clear(&clear);
        Draw();
        for (var i = 0; i < Buttons.Count; i++)
        {
            DrawButton(Buttons[i], i);
        }

        if (target->EndDraw() == HRESULT.D2DERR_RECREATE_TARGET)
        {
            ReleaseTarget();
            Invalidate();
        }
    }

    private void DrawButton(PopupButton button, int index)
    {
        var rect = new D2D1_ROUNDED_RECT
        {
            rect = new D2D_RECT_F { left = button.X, top = button.Y, right = button.X + button.Width, bottom = button.Y + button.Height },
            radiusX = 4,
            radiusY = 4,
        };
        var isPressed = pressed == index && hovered == index;
        var isHovered = hovered == index;
        D2D1_COLOR_F fill;
        D2D1_COLOR_F text;
        if (button.Primary)
        {
            fill = palette.Accent;
            fill.a = isPressed ? 0.8f : isHovered ? 0.9f : 1f;
            text = palette.TextOnAccent;
        }
        else
        {
            fill = isPressed ? palette.ButtonFillPressed : isHovered ? palette.ButtonFillHover : palette.ButtonFill;
            text = palette.TextPrimary;
            var stroke = palette.ButtonStroke;
            brush->SetColor(&stroke);
            var inner = rect;
            inner.rect.left += 0.5f;
            inner.rect.top += 0.5f;
            inner.rect.right -= 0.5f;
            inner.rect.bottom -= 0.5f;
            target->DrawRoundedRectangle(&inner, (ID2D1Brush*)brush, 1, null);
        }

        brush->SetColor(&fill);
        target->FillRoundedRectangle(&rect, (ID2D1Brush*)brush);
        DrawText(button.Label, button.X, button.Y - 1, button.Width, button.Height, TextFont, 14, DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_NORMAL, text, DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);
    }

    private bool EnsureTarget()
    {
        if (target is not null)
        {
            return true;
        }

        var props = new D2D1_RENDER_TARGET_PROPERTIES
        {
            // A hardware target loads the GPU driver into the process: about 38 MB private instead of 8 MB.
            type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
            pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
            dpiX = dpi,
            dpiY = dpi,
        };
        var hwndProps = new D2D1_HWND_RENDER_TARGET_PROPERTIES
        {
            hwnd = hwnd,
            pixelSize = ClientSize(),
            presentOptions = D2D1_PRESENT_OPTIONS.D2D1_PRESENT_OPTIONS_NONE,
        };

        ID2D1HwndRenderTarget* created;
        try
        {
            Graphics.D2D->CreateHwndRenderTarget(&props, &hwndProps, &created);
        }
        catch (COMException)
        {
            return false;
        }

        target = created;
        target->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        var white = new D2D1_COLOR_F { r = 1, g = 1, b = 1, a = 1 };
        ID2D1SolidColorBrush* solid;
        target->CreateSolidColorBrush(&white, null, &solid);
        brush = solid;
        return true;
    }

    private void ReleaseTarget()
    {
        foreach (var format in formats.Values)
        {
            ((IDWriteTextFormat*)format)->Release();
        }

        formats.Clear();
        if (brush is not null)
        {
            brush->Release();
            brush = null;
        }

        if (target is not null)
        {
            target->Release();
            target = null;
        }
    }

    private IDWriteTextFormat* Format(string family, float size, DWRITE_FONT_WEIGHT weight, DWRITE_TEXT_ALIGNMENT align)
    {
        var key = (family, size, weight, align);
        if (formats.TryGetValue(key, out var existing))
        {
            return (IDWriteTextFormat*)existing;
        }

        IDWriteTextFormat* format;
        fixed (char* familyName = family)
        fixed (char* locale = "en-us")
        {
            Graphics.DWrite->CreateTextFormat(familyName, null, weight, DWRITE_FONT_STYLE.DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH.DWRITE_FONT_STRETCH_NORMAL, size, locale, &format);
        }

        format->SetTextAlignment(align);
        format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT.DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
        format->SetWordWrapping(DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_NO_WRAP);
        formats[key] = (nint)format;
        return format;
    }

    private D2D_SIZE_U ClientSize()
    {
        RECT client;
        PInvoke.GetClientRect(hwnd, &client);
        return new D2D_SIZE_U { width = (uint)client.Width, height = (uint)client.Height };
    }

    private void OnMouseMove(LPARAM lParam)
    {
        if (!trackingLeave)
        {
            var track = new TRACKMOUSEEVENT { cbSize = (uint)sizeof(TRACKMOUSEEVENT), dwFlags = TRACKMOUSEEVENT_FLAGS.TME_LEAVE, hwndTrack = hwnd };
            trackingLeave = PInvoke.TrackMouseEvent(&track);
        }

        SetHovered(HitTest(lParam));
    }

    private void SetHovered(int index)
    {
        if (index != hovered)
        {
            hovered = index;
            Invalidate();
        }
    }

    private int HitTest(LPARAM lParam)
    {
        var scale = dpi / 96f;
        var x = (short)(lParam.Value & 0xFFFF) / scale;
        var y = (short)((lParam.Value >> 16) & 0xFFFF) / scale;
        for (var i = 0; i < Buttons.Count; i++)
        {
            if (Buttons[i].Contains(x, y))
            {
                return i;
            }
        }

        return -1;
    }
}
