using Avalonia.Controls;
using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Nikse.SubtitleEdit.Logic;

/// <summary>
/// The small things that make a macOS document window feel native, sent straight to AppKit:
/// <list type="bullet">
/// <item>the dot in the red close button while there are unsaved changes (NSWindow.documentEdited),
/// which on macOS replaces the "*" other platforms put in the title;</item>
/// <item>the file icon next to the title (NSWindow.representedFilename): Cmd-click it for the
/// folder path, drag it to Finder or Mail;</item>
/// <item>the system list of recent documents (NSDocumentController), shown when right-clicking
/// the app in the Dock;</item>
/// <item>the language of the parts macOS draws itself - Open/Save panels, Services, Dictation -
/// which follows the app's per-application language (AppleLanguages).</item>
/// </list>
/// Every call is best effort: a failure is logged and the window simply behaves as before.
/// </summary>
internal static partial class MacDocumentWindow
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    [LibraryImport(LibObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_getClass(string name);

    [LibraryImport(LibObjC, StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr SendPtr(IntPtr receiver, IntPtr sel);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr SendPtrPtr(IntPtr receiver, IntPtr sel, IntPtr arg);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial void SendVoidPtr(IntPtr receiver, IntPtr sel, IntPtr arg);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial void SendVoidBool(IntPtr receiver, IntPtr sel, sbyte arg);

    [LibraryImport(LibObjC, StringMarshalling = StringMarshalling.Utf8, EntryPoint = "objc_msgSend")]
    private static partial IntPtr SendPtrUtf8(IntPtr receiver, IntPtr sel, string arg);

    /// <summary>Shows or clears the unsaved-changes dot in the window's close button.</summary>
    public static void SetDocumentEdited(Window? window, bool edited)
    {
        Run(window, nsWindow => SendVoidBool(nsWindow, sel_registerName("setDocumentEdited:"), edited ? (sbyte)1 : (sbyte)0));
    }

    /// <summary>Puts the file's icon next to the title (empty path removes it).</summary>
    public static void SetRepresentedFile(Window? window, string? fileName)
    {
        var path = !string.IsNullOrEmpty(fileName) && File.Exists(fileName) ? Path.GetFullPath(fileName) : string.Empty;
        Run(window, nsWindow => SendVoidPtr(nsWindow, sel_registerName("setRepresentedFilename:"), NsString(path)));
    }

    /// <summary>Adds the file to macOS' recent documents (the app's Dock menu).</summary>
    public static void NoteRecentDocument(string? fileName)
    {
        if (!OperatingSystem.IsMacOS() || string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
        {
            return;
        }

        try
        {
            var url = SendPtrPtr(objc_getClass("NSURL"), sel_registerName("fileURLWithPath:"), NsString(Path.GetFullPath(fileName)));
            var controller = SendPtr(objc_getClass("NSDocumentController"), sel_registerName("sharedDocumentController"));
            if (url != IntPtr.Zero && controller != IntPtr.Zero)
            {
                SendVoidPtr(controller, sel_registerName("noteNewRecentDocumentURL:"), url);
            }
        }
        catch (Exception exception)
        {
            Se.LogError(exception, "Could not add macOS recent document");
        }
    }

    private static IntPtr NsString(string value) =>
        SendPtrUtf8(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), value);

    private static void Run(Window? window, Action<IntPtr> action)
    {
        if (!OperatingSystem.IsMacOS() || window == null)
        {
            return;
        }

        try
        {
            var handle = window.TryGetPlatformHandle();
            if (handle == null || handle.Handle == IntPtr.Zero ||
                !string.Equals(handle.HandleDescriptor, "NSWindow", StringComparison.Ordinal))
            {
                return;
            }

            action(handle.Handle);
        }
        catch (Exception exception)
        {
            Se.LogError(exception, "macOS document window update failed");
        }
    }

    // ── Language of the system-drawn UI ─────────────────────────────────────

    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint KCFStringEncodingUtf8 = 0x08000100;

    [DllImport(CoreFoundation)] private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, byte[] cStr, uint encoding);
    [DllImport(CoreFoundation)] private static extern IntPtr CFArrayCreate(IntPtr allocator, IntPtr[] values, nint numValues, IntPtr callBacks);
    [DllImport(CoreFoundation)] private static extern void CFPreferencesSetAppValue(IntPtr key, IntPtr value, IntPtr applicationId);
    [DllImport(CoreFoundation)] private static extern byte CFPreferencesAppSynchronize(IntPtr applicationId);
    [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr cf);

    /// <summary>
    /// Makes the parts of the UI that macOS draws itself use the same language as Subtitle Edit,
    /// by setting this application's AppleLanguages preference - the same setting as System
    /// Settings &gt; General &gt; Language &amp; Region &gt; Applications. Must run before AppKit
    /// starts to apply to the current launch; a later change applies from the next start.
    /// </summary>
    public static void SyncSystemLanguage(string? translationName)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var code = SystemUiLanguage.GetAppleLanguageCode(translationName);
        if (code == null || code == _syncedLanguageCode)
        {
            return;
        }

        _syncedLanguageCode = code;

        var key = IntPtr.Zero;
        var value = IntPtr.Zero;
        var array = IntPtr.Zero;
        var appId = IntPtr.Zero;
        try
        {
            key = CfString("AppleLanguages");
            value = CfString(code);
            appId = CfString("kCFPreferencesCurrentApplication");
            var callBacks = NativeLibrary.GetExport(NativeLibrary.Load(CoreFoundation), "kCFTypeArrayCallBacks");
            array = CFArrayCreate(IntPtr.Zero, [value], 1, callBacks);
            if (array != IntPtr.Zero)
            {
                CFPreferencesSetAppValue(key, array, appId);
                CFPreferencesAppSynchronize(appId);
            }
        }
        catch (Exception exception)
        {
            Se.LogError(exception, "Could not set macOS application language");
        }
        finally
        {
            foreach (var cf in new[] { array, value, key, appId })
            {
                if (cf != IntPtr.Zero)
                {
                    CFRelease(cf);
                }
            }
        }
    }

    private static string? _syncedLanguageCode;

    private static IntPtr CfString(string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s + "\0");
        return CFStringCreateWithCString(IntPtr.Zero, bytes, KCFStringEncodingUtf8);
    }
}
