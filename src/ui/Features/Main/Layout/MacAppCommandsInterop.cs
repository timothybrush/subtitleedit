using System;
using System.Runtime.InteropServices;

namespace Nikse.SubtitleEdit.Features.Main.Layout;

/// <summary>
/// The standard macOS app-menu commands (Hide, Hide Others, Show All). Avalonia adds these
/// itself, but with hard-coded English titles; Subtitle Edit turns Avalonia's items off
/// (MacOSPlatformOptions.DisableDefaultApplicationMenuItems) and adds translated ones that
/// send the same NSApplication messages AppKit's own menu items would.
/// </summary>
internal static partial class MacAppCommandsInterop
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    [LibraryImport(LibObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_getClass(string name);

    [LibraryImport(LibObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr SendPtr(IntPtr receiver, IntPtr sel);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial void SendVoidPtr(IntPtr receiver, IntPtr sel, IntPtr arg);

    public static void HideApp() => Send("hide:");

    public static void HideOtherApps() => Send("hideOtherApplications:");

    public static void ShowAllApps() => Send("unhideAllApplications:");

    private static void Send(string selector)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            var nsApp = SendPtr(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            if (nsApp != IntPtr.Zero)
            {
                SendVoidPtr(nsApp, sel_registerName(selector), IntPtr.Zero);
            }
        }
        catch (Exception exception)
        {
            Nikse.SubtitleEdit.Logic.Config.Se.LogError(exception, "macOS app menu command failed: " + selector);
        }
    }
}
