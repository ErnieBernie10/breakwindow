using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using BreakWindow.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace BreakWindow;

[SupportedOSPlatform("windows10.0.17763")]
internal sealed unsafe class App
{
    private const nuint TimerId = 1;
    private const uint PollMilliseconds = 5000;
    private const uint CountdownMilliseconds = 1000;
    private const uint TrayMessage = PInvoke.WM_APP + 1;
    private const uint NinKeySelect = PInvoke.NIN_SELECT | 1;

    private const nuint CmdClock = 1;
    private const nuint CmdBreakNow = 2;
    private const nuint CmdSettings = 3;
    private const nuint CmdQuit = 4;

    private static App? current;

    private readonly SettingsFile settingsFile = new();
    private readonly BreakTracker tracker;
    private HWND hwnd;
    private uint taskbarCreated;
    private Tray? tray;
    private uint timerInterval;
    private bool darkMode;

    public App()
    {
        tracker = new BreakTracker(settingsFile.Settings, new SystemClock(), new LastInputIdleSource());
    }

    public int Run()
    {
        current = this;
        Autostart.Apply(tracker.Settings.StartWithWindows);
        darkMode = MenuTheme.AppsUseDarkTheme;
        MenuTheme.Apply(darkMode);

        var instance = (HINSTANCE)(nint)PInvoke.GetModuleHandle((PCWSTR)null).Value;
        fixed (char* className = "breakwindow.app")
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = instance,
                lpszClassName = className,
            };
            PInvoke.RegisterClassEx(&wc);
            hwnd = PInvoke.CreateWindowEx(0, className, className, 0, 0, 0, 0, 0, HWND.Null, HMENU.Null, instance, null);
        }

        PInvoke.WTSRegisterSessionNotification(hwnd, PInvoke.NOTIFY_FOR_THIS_SESSION);
        taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
        tray = new Tray(hwnd, TrayMessage);
        tray.Add();
        Sync();

        RunLoop();

        PInvoke.WTSUnRegisterSessionNotification(hwnd);
        tray.Dispose();
        settingsFile.Dispose();
        return 0;
    }

    private void RunLoop()
    {
        var change = settingsFile.ChangeHandle.IsInvalid ? HANDLE.Null : (HANDLE)settingsFile.ChangeHandle.DangerousGetHandle();
        var count = change.IsNull ? 0u : 1u;
        while (true)
        {
            var result = PInvoke.MsgWaitForMultipleObjectsEx(count, &change, PInvoke.INFINITE, QUEUE_STATUS_FLAGS.QS_ALLINPUT, MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS.MWMO_INPUTAVAILABLE);
            if (count == 1 && result == WAIT_EVENT.WAIT_OBJECT_0)
            {
                settingsFile.Rearm();
                ReloadSettings();
            }

            MSG msg;
            while (PInvoke.PeekMessage(&msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
            {
                if (msg.message == PInvoke.WM_QUIT)
                {
                    return;
                }

                PInvoke.TranslateMessage(&msg);
                PInvoke.DispatchMessage(&msg);
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) =>
        current is { } app && app.hwnd == hwnd
            ? app.HandleMessage(msg, wParam, lParam)
            : PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

    private LRESULT HandleMessage(uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case PInvoke.WM_TIMER when wParam.Value == TimerId:
                if (settingsFile.RetryPending)
                {
                    ReloadSettings();
                }

                tracker.Tick();
                Sync();
                return default;

            case TrayMessage:
                var trayEvent = (uint)(lParam.Value & 0xFFFF);
                if (trayEvent is PInvoke.WM_CONTEXTMENU or PInvoke.NIN_SELECT or NinKeySelect)
                {
                    ShowMenu();
                }

                return default;

            case PInvoke.WM_WTSSESSION_CHANGE:
                if (wParam.Value == PInvoke.WTS_SESSION_LOCK)
                {
                    tracker.SetLocked(true);
                }
                else if (wParam.Value == PInvoke.WTS_SESSION_UNLOCK)
                {
                    tracker.SetLocked(false);
                }

                Sync();
                return default;

            case PInvoke.WM_POWERBROADCAST:
                if (wParam.Value == PInvoke.PBT_APMSUSPEND)
                {
                    tracker.SetSuspended(true);
                }
                else if (wParam.Value == PInvoke.PBT_APMRESUMEAUTOMATIC)
                {
                    tracker.SetSuspended(false);
                }

                Sync();
                return (LRESULT)1;

            case PInvoke.WM_SETTINGCHANGE:
                if (lParam.Value != 0 && new string((char*)lParam.Value) == "ImmersiveColorSet")
                {
                    OnThemeChanged();
                }

                return default;

            case PInvoke.WM_ENDSESSION:
                if (wParam.Value != 0)
                {
                    tray?.Dispose();
                }

                return default;

            case PInvoke.WM_DESTROY:
                PInvoke.PostQuitMessage(0);
                return default;
        }

        if (msg == taskbarCreated && taskbarCreated != 0)
        {
            tray?.Add();
            return default;
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void OnThemeChanged()
    {
        var dark = MenuTheme.AppsUseDarkTheme;
        if (dark == darkMode)
        {
            return;
        }

        darkMode = dark;
        MenuTheme.Apply(dark);
    }

    private void ReloadSettings()
    {
        settingsFile.Reload();
        tracker.UpdateSettings(settingsFile.Settings);
        Autostart.Apply(tracker.Settings.StartWithWindows);
        Sync();
    }

    private void Sync()
    {
        var interval = tracker.Phase == Phase.OnBreak ? CountdownMilliseconds : PollMilliseconds;
        if (interval != timerInterval)
        {
            timerInterval = interval;
            PInvoke.SetTimer(hwnd, TimerId, interval, null);
        }

        var tooltip = StatusText.Tooltip(tracker);
        if (settingsFile.Error is { } error)
        {
            tooltip += "\n" + error + ", using last good settings";
        }

        tray?.SetTooltip(tooltip);
    }

    private void ShowMenu()
    {
        var menu = PInvoke.CreatePopupMenu();
        var flags = MENU_ITEM_FLAGS.MF_STRING;
        AddItem(menu, flags, CmdClock, tracker.IsActive ? "Clock out" : "Clock in");
        var canBreak = tracker.Phase is Phase.Working or Phase.BreakDue;
        AddItem(menu, canBreak ? flags : flags | MENU_ITEM_FLAGS.MF_GRAYED, CmdBreakNow, "Take break now");
        AddItem(menu, flags, CmdSettings, "Open settings");
        AddItem(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
        AddItem(menu, flags, CmdQuit, "Quit");

        System.Drawing.Point cursor;
        PInvoke.GetCursorPos(&cursor);
        PInvoke.SetForegroundWindow(hwnd);
        var command = (nuint)(int)PInvoke.TrackPopupMenuEx(
            menu,
            (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN),
            cursor.X,
            cursor.Y,
            hwnd,
            null);
        PInvoke.PostMessage(hwnd, PInvoke.WM_NULL, default, default);
        PInvoke.DestroyMenu(menu);

        switch (command)
        {
            case CmdClock:
                if (tracker.IsActive)
                {
                    tracker.ClockOut();
                }
                else
                {
                    tracker.ClockIn();
                }

                break;
            case CmdBreakNow:
                tracker.StartBreak();
                break;
            case CmdSettings:
                OpenSettings();
                break;
            case CmdQuit:
                PInvoke.DestroyWindow(hwnd);
                return;
        }

        Sync();
    }

    private static void AddItem(HMENU menu, MENU_ITEM_FLAGS flags, nuint id, string? text)
    {
        fixed (char* p = text)
        {
            PInvoke.AppendMenu(menu, flags, id, p);
        }
    }

    private void OpenSettings()
    {
        var result = PInvoke.ShellExecute(hwnd, "open", settingsFile.FilePath, null, null, SHOW_WINDOW_CMD.SW_SHOWNORMAL);
        if ((nint)result.DangerousGetHandle() <= 32)
        {
            PInvoke.ShellExecute(hwnd, "open", "notepad.exe", $"\"{settingsFile.FilePath}\"", null, SHOW_WINDOW_CMD.SW_SHOWNORMAL);
        }
    }
}
