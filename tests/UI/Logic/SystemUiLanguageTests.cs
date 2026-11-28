using Nikse.SubtitleEdit.Logic;

namespace UITests.Logic;

/// <summary>
/// First start on macOS picks the UI translation from the system's preferred languages
/// instead of always starting in English.
/// </summary>
public class SystemUiLanguageTests
{
    [Theory]
    [InlineData("sv-SE", "Swedish")]
    [InlineData("sv", "Swedish")]
    [InlineData("sv_FI", "Swedish")]
    [InlineData("sv-SE@rg=sezzzz", "Swedish")]
    [InlineData("de-CH.UTF-8", "German")]
    [InlineData("en-GB", "English")]
    [InlineData("nb-NO", "Norwegian")]
    [InlineData("nn", "Norwegian")]
    [InlineData("ja-JP", "Japanese")]
    [InlineData("pt", "Portuguese (Brazil)")]
    [InlineData("pt-BR", "Portuguese (Brazil)")]
    [InlineData("pt-PT", "Portuguese")]
    [InlineData("zh-Hans-SE", "ChineseSimplified")]
    [InlineData("zh-Hant-TW", "ChineseTraditional")]
    [InlineData("zh-TW", "ChineseTraditional")]
    [InlineData("zh-CN", "ChineseSimplified")]
    [InlineData("ckb", "Central Kurdish")]
    public void MapsLanguageTagToTranslation(string tag, string expected)
    {
        Assert.Equal(expected, SystemUiLanguage.FindTranslation(tag));
    }

    [Theory]
    [InlineData("")]
    [InlineData("xx-YY")]
    [InlineData("is-IS")]
    public void UnknownLanguageHasNoTranslation(string tag)
    {
        Assert.Null(SystemUiLanguage.FindTranslation(tag));
    }

    [Fact]
    public void FirstPreferredLanguageWithTranslationWins()
    {
        Assert.Equal("Swedish", SystemUiLanguage.FindTranslation(["is-IS", "sv-SE", "en-US"]));
        Assert.Equal("English", SystemUiLanguage.FindTranslation(["en-US", "sv-SE"]));
        Assert.Null(SystemUiLanguage.FindTranslation(["is-IS"]));
    }

    [Fact]
    public void ReadsMacPreferredLanguages()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        // The system-wide list (not this process's own AppleLanguages) is always set on macOS.
        Assert.NotEmpty(SystemUiLanguage.GetPreferredLanguages());
        Assert.All(SystemUiLanguage.GetPreferredLanguages(), p => Assert.False(string.IsNullOrWhiteSpace(p)));
    }

    [Fact]
    public void EveryMappedTranslationShipsAsAFile()
    {
        var folder = FindLanguagesFolder();
        if (folder == null)
        {
            return; // not running from inside a checkout
        }

        string[] tags =
        [
            "ar", "az", "bg", "ca", "ckb", "zh-Hans", "zh-Hant", "hr", "cs", "da", "nl", "en", "fi", "fr",
            "de", "el", "he", "hu", "id", "it", "ja", "ko", "mk", "nb", "fa", "pl", "pt-BR", "pt-PT",
            "ro", "ru", "es", "sv", "th", "tr", "uk", "vi",
        ];

        foreach (var tag in tags)
        {
            var name = SystemUiLanguage.FindTranslation(tag);
            Assert.NotNull(name);
            Assert.True(File.Exists(Path.Combine(folder, name + ".json")), $"{tag} -> {name}.json missing");
        }

        // Every shipped translation is reachable from some tag.
        Assert.Equal(Directory.GetFiles(folder, "*.json").Length, tags.Select(SystemUiLanguage.FindTranslation).Distinct().Count());
    }

    [Fact]
    public void EveryTranslationHasAnAppleCodeListedInInfoPlist()
    {
        var folder = FindLanguagesFolder();
        if (folder == null)
        {
            return; // not running from inside a checkout
        }

        var plist = Path.Combine(folder, "..", "..", "..", "..", "installer", "macBundle", "SubtitleEdit.app", "Contents", "Info.plist");
        var plistText = File.ReadAllText(plist);
        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var code = SystemUiLanguage.GetAppleLanguageCode(name);
            Assert.True(code != null, $"{name} has no macOS language code");
            Assert.Contains($"<string>{code}</string>", plistText);
        }
    }

    private static string? FindLanguagesFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ui", "Assets", "Languages");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
