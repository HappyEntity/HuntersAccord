namespace HuntersAccord.Core.Emulator;

/// <summary>A PS4 system language (shadPS4 <c>General.console_language</c>); games read it at start-up.</summary>
public sealed record ConsoleLanguage(int Id, string NativeName, string? UiCode = null);

public static class ConsoleLanguages
{
    public static IReadOnlyList<ConsoleLanguage> All { get; } =
    [
        new(1, "English (US)", "en"),
        new(18, "English (UK)"),
        new(8, "Русский", "ru"),
        new(4, "Deutsch", "de"),
        new(2, "Français", "fr"),
        new(3, "Español", "es"),
        new(20, "Español (Latinoamérica)"),
        new(5, "Italiano", "it"),
        new(7, "Português", "pt"),
        new(17, "Português (Brasil)"),
        new(16, "Polski", "pl"),
        new(6, "Nederlands", "nl"),
        new(19, "Türkçe", "tr"),
        new(0, "日本語", "ja"),
        new(9, "한국어", "ko"),
        new(11, "简体中文", "zh"),
        new(10, "繁體中文"),
    ];

    public const int English = 1;

    public static int ForUiLanguage(string uiCode) =>
        All.FirstOrDefault(l => l.UiCode == uiCode)?.Id ?? English;
}
