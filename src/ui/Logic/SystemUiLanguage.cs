using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Nikse.SubtitleEdit.Logic;

/// <summary>
/// Picks the UI translation that matches the operating system's language, so a first start
/// on a Swedish Mac comes up in Swedish instead of English with a language list to dig through.
/// The match is made against the user's ordered list of preferred languages (System Settings >
/// General > Language &amp; Region), and the first language in that list that has a translation
/// wins - English included, so "English, Swedish" stays English.
/// </summary>
public static class SystemUiLanguage
{
    /// <summary>Language tag (BCP 47) -> translation file name without ".json".</summary>
    private static readonly Dictionary<string, string> TranslationByTag = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = "Arabic",
        ["az"] = "Azerbaijani",
        ["bg"] = "Bulgarian",
        ["ca"] = "Catalan",
        ["ckb"] = "Central Kurdish",
        ["zh-Hans"] = "ChineseSimplified",
        ["zh-Hant"] = "ChineseTraditional",
        ["hr"] = "Croatian",
        ["cs"] = "Czech",
        ["da"] = "Danish",
        ["nl"] = "Dutch",
        ["en"] = "English",
        ["fi"] = "Finnish",
        ["fr"] = "French",
        ["de"] = "German",
        ["el"] = "Greek",
        ["he"] = "Hebrew",
        ["iw"] = "Hebrew",
        ["hu"] = "Hungarian",
        ["id"] = "Indonesian",
        ["in"] = "Indonesian",
        ["it"] = "Italian",
        ["ja"] = "Japanese",
        ["ko"] = "Korean",
        ["mk"] = "Macedonian",
        ["nb"] = "Norwegian",
        ["nn"] = "Norwegian",
        ["no"] = "Norwegian",
        ["fa"] = "Persian",
        ["pl"] = "Polish",
        ["pt-BR"] = "Portuguese (Brazil)",
        ["pt"] = "Portuguese",
        ["ro"] = "Romanian",
        ["ru"] = "Russian",
        ["es"] = "Spanish",
        ["sv"] = "Swedish",
        ["th"] = "Thai",
        ["tr"] = "Turkish",
        ["uk"] = "Ukrainian",
        ["vi"] = "Vietnamese",
    };

    /// <summary>Translation file name -> the language code macOS uses for it (CFBundleLocalizations).</summary>
    private static readonly Dictionary<string, string> AppleCodeByTranslation = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arabic"] = "ar",
        ["Azerbaijani"] = "az",
        ["Bulgarian"] = "bg",
        ["Catalan"] = "ca",
        ["Central Kurdish"] = "ckb",
        ["ChineseSimplified"] = "zh-Hans",
        ["ChineseTraditional"] = "zh-Hant",
        ["Croatian"] = "hr",
        ["Czech"] = "cs",
        ["Danish"] = "da",
        ["Dutch"] = "nl",
        ["English"] = "en",
        ["Finnish"] = "fi",
        ["French"] = "fr",
        ["German"] = "de",
        ["Greek"] = "el",
        ["Hebrew"] = "he",
        ["Hungarian"] = "hu",
        ["Indonesian"] = "id",
        ["Italian"] = "it",
        ["Japanese"] = "ja",
        ["Korean"] = "ko",
        ["Macedonian"] = "mk",
        ["Norwegian"] = "nb",
        ["Persian"] = "fa",
        ["Polish"] = "pl",
        ["Portuguese (Brazil)"] = "pt-BR",
        ["Portuguese"] = "pt-PT",
        ["Romanian"] = "ro",
        ["Russian"] = "ru",
        ["Spanish"] = "es",
        ["Swedish"] = "sv",
        ["Thai"] = "th",
        ["Turkish"] = "tr",
        ["Ukrainian"] = "uk",
        ["Vietnamese"] = "vi",
    };

    /// <summary>
    /// The macOS language code for a translation name ("Swedish" -> "sv"), or null for a
    /// translation macOS has no code for here (e.g. a user-made one).
    /// </summary>
    public static string? GetAppleLanguageCode(string? translationName) =>
        string.IsNullOrEmpty(translationName) ? null : AppleCodeByTranslation.GetValueOrDefault(translationName);

    /// <summary>
    /// Returns the translation name (e.g. "Swedish") for the first preferred language that has
    /// one, or null when none does.
    /// </summary>
    public static string? FindTranslation(IEnumerable<string> preferredLanguages)
    {
        foreach (var raw in preferredLanguages)
        {
            var match = FindTranslation(raw);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>Maps one language tag such as "sv-SE", "pt_BR" or "zh-Hant-TW" to a translation name.</summary>
    public static string? FindTranslation(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return null;
        }

        // "sv_SE", "sv-SE@rg=sezzzz" and "sv-SE.UTF-8" all mean Swedish.
        var tag = languageTag.Trim().Replace('_', '-');
        var cut = tag.IndexOfAny(['@', '.']);
        if (cut >= 0)
        {
            tag = tag[..cut];
        }

        var parts = tag.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var language = parts[0].ToLowerInvariant();
        string? script = null;
        string? region = null;
        for (var i = 1; i < parts.Length; i++)
        {
            if (parts[i].Length == 4 && script == null)
            {
                script = parts[i];
            }
            else if ((parts[i].Length == 2 || parts[i].Length == 3) && region == null)
            {
                region = parts[i].ToUpperInvariant();
            }
        }

        if (language == "zh")
        {
            // Chinese needs the script; without one, Taiwan, Hong Kong and Macau use Traditional.
            script ??= region is "TW" or "HK" or "MO" ? "Hant" : "Hans";
            return TranslationByTag.GetValueOrDefault("zh-" + script);
        }

        if (language == "pt")
        {
            // Apple's plain "pt" is Brazilian Portuguese; Portugal is "pt-PT".
            return region is null or "BR" ? TranslationByTag["pt-BR"] : TranslationByTag["pt"];
        }

        return TranslationByTag.GetValueOrDefault(language);
    }

    /// <summary>
    /// The user's preferred languages in priority order. On macOS this is the list from
    /// System Settings; .NET's CurrentUICulture is no substitute there, because an app started
    /// from Finder or the Dock has no LANG variable and .NET then falls back to the invariant culture.
    /// </summary>
    public static IReadOnlyList<string> GetPreferredLanguages()
    {
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                var languages = MacPreferredLanguages();
                if (languages.Count > 0)
                {
                    return languages;
                }
            }
            catch (Exception exception)
            {
                Se.LogError(exception, "Could not read macOS preferred languages");
            }
        }

        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        return string.IsNullOrEmpty(culture) ? [] : [culture];
    }

    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint KCFStringEncodingUtf8 = 0x08000100;

    [DllImport(CoreFoundation)] private static extern IntPtr CFLocaleCopyPreferredLanguages();
    [DllImport(CoreFoundation)] private static extern nint CFArrayGetCount(IntPtr array);
    [DllImport(CoreFoundation)] private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);
    [DllImport(CoreFoundation)] private static extern byte CFStringGetCString(IntPtr str, byte[] buffer, nint bufferSize, uint encoding);
    [DllImport(CoreFoundation)] private static extern void CFRelease(IntPtr cf);

    private static List<string> MacPreferredLanguages()
    {
        var result = new List<string>();
        var array = CFLocaleCopyPreferredLanguages();
        if (array == IntPtr.Zero)
        {
            return result;
        }

        try
        {
            var buffer = new byte[256];
            var count = CFArrayGetCount(array);
            for (nint i = 0; i < count; i++)
            {
                var str = CFArrayGetValueAtIndex(array, i);
                if (str != IntPtr.Zero && CFStringGetCString(str, buffer, buffer.Length, KCFStringEncodingUtf8) != 0)
                {
                    var length = Array.IndexOf(buffer, (byte)0);
                    result.Add(System.Text.Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length));
                }
            }
        }
        finally
        {
            CFRelease(array);
        }

        return result;
    }
}
