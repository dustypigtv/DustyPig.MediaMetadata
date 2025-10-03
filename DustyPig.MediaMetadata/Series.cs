using DustyPig.API.v3.MPAA;
using System.Collections.Generic;

namespace DustyPig.MediaMetadata;

public class Series
{
    public string? ImdbId { get; set; }

    public int? TvdbId { get; set; }

    public int? TmdbId { get; set; }

    public string? Title { get; set; }

    public int? Year { get; set; }

    public string? TvdbSlug { get; set; }

    public string? PosterUrl { get; set; }

    public string? BackdropUrl { get; set; }

    public List<string>? Genres { get; set; }

    public string? Overview { get; set; }

    public TVRatings? TVRating { get; set; }
}
