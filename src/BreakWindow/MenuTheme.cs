using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.LibraryLoader;

namespace BreakWindow;

internal static unsafe class MenuTheme
{
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static delegate* unmanaged<int, int> setPreferredAppMode;
    private static delegate* unmanaged<void> flushMenuThemes;
    private static bool loaded;

    public static bool AppsUseDarkTheme => Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", 1) is 0;

    // SetPreferredAppMode (ordinal 135) and FlushMenuThemes (ordinal 136) are undocumented uxtheme exports.
    public static void Apply(bool dark)
    {
        if (!loaded)
        {
            loaded = true;
            var uxtheme = PInvoke.LoadLibraryEx("uxtheme.dll", LOAD_LIBRARY_FLAGS.LOAD_LIBRARY_SEARCH_SYSTEM32);
            if (!uxtheme.IsInvalid)
            {
                var module = (HMODULE)uxtheme.DangerousGetHandle();
                setPreferredAppMode = (delegate* unmanaged<int, int>)PInvoke.GetProcAddress(module, new PCSTR((byte*)135)).Value;
                flushMenuThemes = (delegate* unmanaged<void>)PInvoke.GetProcAddress(module, new PCSTR((byte*)136)).Value;
            }
        }

        if (setPreferredAppMode is not null && flushMenuThemes is not null)
        {
            setPreferredAppMode(dark ? 2 : 3);
            flushMenuThemes();
        }
    }
}
