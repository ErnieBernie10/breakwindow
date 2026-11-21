using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct2D.Common;
using Windows.Win32.System.LibraryLoader;

namespace BreakWindow;

internal readonly record struct Palette(
    D2D1_COLOR_F Background,
    D2D1_COLOR_F TextPrimary,
    D2D1_COLOR_F TextSecondary,
    D2D1_COLOR_F Accent,
    D2D1_COLOR_F TextOnAccent,
    D2D1_COLOR_F ButtonFill,
    D2D1_COLOR_F ButtonFillHover,
    D2D1_COLOR_F ButtonFillPressed,
    D2D1_COLOR_F ButtonStroke);

internal static unsafe class Theme
{
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AccentKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";

    private static delegate* unmanaged<int, int> setPreferredAppMode;
    private static delegate* unmanaged<void> flushMenuThemes;
    private static bool loaded;

    public static bool AppsUseDarkTheme => Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", 1) is 0;

    public static Palette Palette(bool dark) => dark
        ? new Palette(
            Background: Rgb(0x20, 0x20, 0x20),
            TextPrimary: Rgb(0xFF, 0xFF, 0xFF),
            TextSecondary: Rgb(0xFF, 0xFF, 0xFF, 0.786f),
            Accent: AccentShade(1) ?? Rgb(0x4C, 0xC2, 0xFF),
            TextOnAccent: Rgb(0x00, 0x00, 0x00),
            ButtonFill: Rgb(0xFF, 0xFF, 0xFF, 0.0605f),
            ButtonFillHover: Rgb(0xFF, 0xFF, 0xFF, 0.0837f),
            ButtonFillPressed: Rgb(0xFF, 0xFF, 0xFF, 0.0326f),
            ButtonStroke: Rgb(0xFF, 0xFF, 0xFF, 0.093f))
        : new Palette(
            Background: Rgb(0xF3, 0xF3, 0xF3),
            TextPrimary: Rgb(0x00, 0x00, 0x00, 0.896f),
            TextSecondary: Rgb(0x00, 0x00, 0x00, 0.606f),
            Accent: AccentShade(4) ?? Rgb(0x00, 0x5F, 0xB8),
            TextOnAccent: Rgb(0xFF, 0xFF, 0xFF),
            ButtonFill: Rgb(0xFF, 0xFF, 0xFF, 0.7f),
            ButtonFillHover: Rgb(0xF9, 0xF9, 0xF9, 0.5f),
            ButtonFillPressed: Rgb(0xF9, 0xF9, 0xF9, 0.3f),
            ButtonStroke: Rgb(0x00, 0x00, 0x00, 0.0578f));

    // SetPreferredAppMode (ordinal 135) and FlushMenuThemes (ordinal 136) are undocumented uxtheme exports.
    public static void ApplyToMenus(bool dark)
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

    // AccentPalette holds eight RGBA entries from AccentLight3 down to AccentDark3.
    private static D2D1_COLOR_F? AccentShade(int index) =>
        Registry.GetValue(AccentKey, "AccentPalette", null) is byte[] { Length: >= 32 } palette
            ? Rgb(palette[index * 4], palette[(index * 4) + 1], palette[(index * 4) + 2])
            : null;

    private static D2D1_COLOR_F Rgb(byte r, byte g, byte b, float a = 1f) =>
        new() { r = r / 255f, g = g / 255f, b = b / 255f, a = a };
}
