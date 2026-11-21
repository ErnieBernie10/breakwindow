using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace BreakWindow;

[SupportedOSPlatform("windows10.0.17763")]
internal sealed unsafe class Tray : IDisposable
{
    private const ushort AppIconId = 32512;

    private readonly HWND owner;
    private readonly uint callbackMessage;
    private readonly HICON icon;
    private string tooltip = "";
    private bool added;

    public Tray(HWND owner, uint callbackMessage)
    {
        this.owner = owner;
        this.callbackMessage = callbackMessage;
        var dpi = PInvoke.GetDpiForSystem();
        var size = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, dpi);
        var module = (HINSTANCE)(nint)PInvoke.GetModuleHandle((PCWSTR)null).Value;
        icon = (HICON)(nint)PInvoke.LoadImage(module, (char*)AppIconId, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, IMAGE_FLAGS.LR_DEFAULTCOLOR).Value;
        if (icon.IsNull)
        {
            icon = PInvoke.LoadIcon(HINSTANCE.Null, PInvoke.IDI_APPLICATION);
        }
    }

    public void Add()
    {
        var data = Data();
        data.uFlags |= NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON;
        data.uCallbackMessage = callbackMessage;
        data.hIcon = icon;
        added = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, &data);
        data.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, &data);
    }

    public void SetTooltip(string text)
    {
        if (text == tooltip)
        {
            return;
        }

        tooltip = text;
        if (added)
        {
            var data = Data();
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, &data);
        }
    }

    public void Dispose()
    {
        if (added)
        {
            var data = Data();
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, &data);
            added = false;
        }

        PInvoke.DestroyIcon(icon);
    }

    private NOTIFYICONDATAW Data()
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = owner,
            uID = 1,
            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP,
        };
        var tip = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        data.szTip = tip;
        return data;
    }
}
