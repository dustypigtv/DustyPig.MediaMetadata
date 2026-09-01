using System.Text.RegularExpressions;

namespace DustyPig.MediaMetadata;

public partial record MovieTitleAndEdition(string Title, string? Edition)
{
    [GeneratedRegex(@"\((?!\s*\))([^)]+)\)$")]
    private static partial Regex MovieEditionsRegex();

    [GeneratedRegex(@"\(\s*\)$")]
    private static partial Regex EmptyMovieEditionRegex();

    public static MovieTitleAndEdition Parse(string title)
    {
        title = (title + string.Empty).Trim();
        var match = MovieEditionsRegex().Match(title);
        if (match.Success)
        {
            title = title[..match.Index].TrimEnd();
            var edition = match.Groups[1].Value.Trim();
            return new(title, edition);
        }
        else
        {
            match = EmptyMovieEditionRegex().Match(title);
            if (match.Success)
            {
                title = title[..match.Index].TrimEnd();
            }
        }

        return new(title, null);
    }
}

