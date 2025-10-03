using DustyPig.API.v3.MPAA;
using System;
using System.Collections.Generic;

namespace DustyPig.MediaMetadata;

public class Movie
{
    public string? ImdbId { get; set; }

    public int? TvdbId { get; set; }

    public int? TmdbId { get; set; }

    public string? Title { get; set; }

    public string? PosterUrl { get; set; }

    public string? BackdropUrl { get; set; }

    public List<string>? Genres { get; set; }

    public string? Overview { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public int? Year => ReleaseDate?.Year;

    public MovieRatings? MovieRating { get; set; }

    public List<string>? Cast { get; set; }

    public List<string>? Directors { get; set; }

    public List<string>? Writers { get; set; }

    public List<string>? Producers { get; set; }
}
