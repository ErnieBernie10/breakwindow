using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Graphics.Direct2D;
using Windows.Win32.Graphics.DirectWrite;

namespace BreakWindow;

[SupportedOSPlatform("windows10.0.17763")]
internal static unsafe class Graphics
{
    private static int users;

    public static ID2D1Factory* D2D { get; private set; }

    public static IDWriteFactory* DWrite { get; private set; }

    public static void Acquire()
    {
        if (users++ > 0)
        {
            return;
        }

        void* d2d;
        var d2dIid = ID2D1Factory.IID_Guid;
        PInvoke.D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, &d2dIid, null, &d2d).ThrowOnFailure();
        D2D = (ID2D1Factory*)d2d;

        void* dwrite;
        var dwriteIid = IDWriteFactory.IID_Guid;
        PInvoke.DWriteCreateFactory(DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, &dwriteIid, &dwrite).ThrowOnFailure();
        DWrite = (IDWriteFactory*)dwrite;
    }

    public static void Release()
    {
        if (--users > 0)
        {
            return;
        }

        DWrite->Release();
        DWrite = null;
        D2D->Release();
        D2D = null;
    }
}
