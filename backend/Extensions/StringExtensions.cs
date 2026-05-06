namespace backend.Extensions;

/// <summary>
/// Extension methods for string formatting and manipulation.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string to Title Case (e.g., "chicken stir fry" -> "Chicken Stir Fry").
    /// Each word's first letter is capitalized, and the rest is lowercase.
    /// </summary>
    /// <param name="input">The string to convert. Can be null or whitespace.</param>
    /// <returns>The title-cased string, or empty string if input is null/whitespace.</returns>
    public static string ToTitleCase(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input ?? string.Empty;

        var words = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
            }
        }
        return string.Join(' ', words);
    }
}
