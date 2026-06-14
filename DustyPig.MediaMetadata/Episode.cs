using System;
using System.Collections.Generic;

namespace DustyPig.MediaMetadata;

public class Episode
{
    public string? SeriesImdbId { get; set; }

    public int? SeriesTvdbId { get; set; }

    public int? SeriesTmdbId { get; set; }

    public string? ImdbId { get; set; }

    public string? ImdbUrl { get; set; }

    public int? TvdbId { get; set; }

    public string? TvdbUrl { get; set; }

    public int? TmdbId { get; set; }

    public string? TmdbUrl { get; set; }

    public int Season { get; set; }

    public int Number { get; set; }

    public string? Title { get; set; }

    public string? Overview { get; set; }

    public DateOnly? FirstAired { get; set; }

    public List<string>? Cast { get; set; }

    public List<string>? Directors { get; set; }

    public List<string>? Writers { get; set; }

    public List<string>? Producers { get; set; }

    public string? ScreenshotUrl { get; set; }

    public override string ToString() => $"s{Season:00}e{Number:00} - {Title}";
}
