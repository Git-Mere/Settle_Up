public static class ReceiptItemNameNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        var lastWasWhitespace = false;

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLowerInvariant(character);
                lastWasWhitespace = false;
                continue;
            }

            if (char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character))
            {
                if (length == 0 || lastWasWhitespace)
                {
                    continue;
                }

                buffer[length++] = ' ';
                lastWasWhitespace = true;
            }
        }

        return length > 0 && buffer[length - 1] == ' '
            ? new string(buffer[..(length - 1)])
            : new string(buffer[..length]);
    }

    public static string CleanDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown Item";
        }

        return string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
