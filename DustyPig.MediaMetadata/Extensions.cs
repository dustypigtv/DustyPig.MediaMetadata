using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DustyPig.MediaMetadata;

internal static class Extensions
{
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? self) => string.IsNullOrWhiteSpace(self);


    public static string NormalizeString(this string? self)
    {
        self = (self + string.Empty).Trim();

        return self

            //The original annoyances
            .Replace("â€˜", "\"")
            .Replace("â€™", "'")
            .Replace("â€¦", "!")
            .Replace("â€œ", "\"")
            .Replace("â€", "\"")
            .Replace("â€“", "-")

            ////Latin diacritics (https://jkorpela.fi/latin1/3.3.html)

            .Replace("¨", "\"")
            .Replace("´", "'")
            .Replace("`", "'")
            .Replace("¸", ",")



            //Handle other diacritics
            .RemoveDiacritics()

            //Handle wierd versions of common chars
            .Replace("‘", "'")
            .Replace("’", "'")
            .Replace("–", "-")
            .Replace("·", "-")
            .Replace("…", "...")


            //Other misc fixes
            .Replace("\0", "")
            .Trim();
    }

    static string RemoveDiacritics(this string? self)
    {
        if (self.IsNullOrWhiteSpace())
            return string.Empty;
        string normal = self.Normalize(NormalizationForm.FormD);
        IEnumerable<char> withoutDiacritics = normal.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new([.. withoutDiacritics]);
    }



    public static bool ICEquals(this string? self, string? compare)
    {
        if (self == null && compare == null)
            return true;

        if (self == null || compare == null)
            return false;

        return self.Equals(compare, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ICStartsWith(this string? self, string? text)
    {
        if (self == null && text == null)
            return true;

        if (self == null || text == null)
            return false;

        return self.StartsWith(text, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ICEndsWith(this string? self, string? text)
    {
        if (self == null && text == null)
            return true;

        if (self == null || text == null)
            return false;

        return self.EndsWith(text, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ICContains(this string? self, string? text)
    {
        if (self == null && text == null)
            return true;

        if (self == null || text == null)
            return false;

        return self.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ICContains(this IEnumerable<string>? lst, string? text)
    {
        if (lst == null)
            return false;

        return lst.Any(item => item.ICEquals(text));
    }

}
