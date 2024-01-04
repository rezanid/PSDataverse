namespace PSDataverse;

using System.Globalization;
using System.Linq;
using System.Text;

public static class ScribanExtensions
{
    /// <summary>
    /// Removes all diacritics and spaces from a string.
    /// </summary>
    public static string RemoveDiacriticsAndSpace(this string text)
        => text.Normalize(NormalizationForm.FormD).EnumerateRunes()
        .Where(c => Rune.GetUnicodeCategory(c) is not UnicodeCategory.NonSpacingMark and not UnicodeCategory.NonSpacingMark)
        .Aggregate(new StringBuilder(), (sb, c) => sb.Append(c)).ToString().Normalize(NormalizationForm.FormC);

    public static string Tokenize(string input)
    {
        if (input == null)
        {
            return null;
        }

        var normalizedString = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizedString.Length);
        UnicodeCategory? previousCategory = null;

        foreach (var c in normalizedString)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(previousCategory is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.DecimalDigitNumber ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c));
                }
                previousCategory = uc;
            }
        }
        if (char.IsDigit(sb[0]))
        {
            sb.Insert(0, '_');
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
