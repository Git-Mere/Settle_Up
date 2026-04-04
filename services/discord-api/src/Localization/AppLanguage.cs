public enum AppLanguage
{
    English,
    Korean
}

public static class AppLanguageParser
{
    public static bool TryParse(string? value, out AppLanguage language)
    {
        language = AppLanguage.English;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "english" or "en" => SetLanguage(AppLanguage.English, out language),
            "korean" or "ko" => SetLanguage(AppLanguage.Korean, out language),
            _ => false
        };
    }

    private static bool SetLanguage(AppLanguage value, out AppLanguage language)
    {
        language = value;
        return true;
    }
}
