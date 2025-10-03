namespace DustyPig.MediaMetadata;

/// <summary>
/// Series or Movie Ids, not for Episodes
/// </summary>
public class Query
{
    public string? ImdbId { get; set; }

    public int? TvdbId { get; set; }

    public int? TmdbId { get; set; }

    public string? Title { get; set; }

    public int? Year { get; set; }
}
