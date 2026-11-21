using BreakWindow.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace BreakWindow;

internal sealed class SettingsFile : IDisposable
{
    private readonly string directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "breakwindow");

    public SettingsFile()
    {
        FilePath = Path.Combine(directory, "settings.json");
        Directory.CreateDirectory(directory);
        if (!File.Exists(FilePath))
        {
            File.WriteAllText(FilePath, SettingsJson.Serialize(new Settings()));
        }

        ChangeHandle = PInvoke.FindFirstChangeNotification(
            directory,
            false,
            FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_FILE_NAME | FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_LAST_WRITE | FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_SIZE);
        Reload();
    }

    public string FilePath { get; }

    public FindCloseChangeNotificationSafeHandle ChangeHandle { get; }

    public Settings Settings { get; private set; } = new();

    public string? Error { get; private set; }

    public bool RetryPending { get; private set; }

    public void Rearm()
    {
        if (!ChangeHandle.IsInvalid)
        {
            PInvoke.FindNextChangeNotification((HANDLE)ChangeHandle.DangerousGetHandle());
        }
    }

    public void Reload()
    {
        string json;
        try
        {
            json = File.ReadAllText(FilePath);
        }
        catch (FileNotFoundException)
        {
            RetryPending = false;
            Error = "settings.json not found";
            return;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            RetryPending = true;
            return;
        }

        RetryPending = false;
        if (SettingsJson.TryParse(json, out var settings, out var error))
        {
            Settings = settings;
            Error = null;
        }
        else
        {
            Error = "settings.json: " + error;
        }
    }

    public void Dispose() => ChangeHandle.Dispose();
}
