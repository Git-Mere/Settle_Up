using System.Collections.Concurrent;

public sealed class UserLanguagePreferenceStore
{
    private readonly ConcurrentDictionary<string, AppLanguage> _preferences = new(StringComparer.Ordinal);

    public AppLanguage GetLanguage(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return AppLanguage.English;
        }

        return _preferences.TryGetValue(userId, out var language)
            ? language
            : AppLanguage.English;
    }

    public AppLanguage SetLanguage(string userId, AppLanguage language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        _preferences[userId] = language;
        return language;
    }
}
