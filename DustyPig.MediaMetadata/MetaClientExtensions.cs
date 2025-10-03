using DustyPig.API.v3.MPAA;
using DustyPig.TVDB.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DustyPig.MediaMetadata;

internal static partial class MetaClientExtensions
{
    [GeneratedRegex("[^a-z0-9 ]")]
    private static partial Regex NormalizeTitleRegex();

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex ComparableTitleRegex();


    public static List<T>? ToNonEmpty<T>(this List<T>? lst) => lst == null || lst.Count == 0 ? null : lst;

    public static int? ToYear(this int? value) => value.HasValue && value.Value > 1900 ? value.Value : null;

    public static int? ToYear(this int value) => value > 1900 ? (int?)value : null;

    public static int? ToYear(this DateOnly? value) => value.HasValue ? value.Value.Year.ToYear() : null;

    public static int? ToYear(this string? value)
    {
        if (int.TryParse(value, out int ret))
            return ret.ToYear();
        return null;
    }

    public static int? ToNumericId(this int? value) => value.HasValue && value.Value > 0 ? value.Value : null;

    public static int? ToNumericId(this int value) => value > 0 ? value : null;

    public static string? ToNonEmpy(this string? value) => value.IsNullOrWhiteSpace() ? null : value;

    public static string ToQuery(this string title)
    {
        title = (title + string.Empty).Trim();
        title = title.ToLower();
        title = title.Replace(" & ", " and ");
        title = NormalizeTitleRegex().Replace(title, string.Empty);
        return title;
    }

    public static string ToComparable(this string? title, string? year)
    {
        if (!year.IsNullOrWhiteSpace())
            if (year.Length > 4)
                year = year[..4];

        int? yearVal = null;
        if (int.TryParse(year, out int val))
            yearVal = val;

        return title.ToComparable(yearVal);
    }

    public static string ToComparable(this string? title, DateOnly? dateOnly) => title.ToComparable(dateOnly?.Year ?? null);

    public static string ToComparable(this string? title, int? year)
    {
        title = (title + string.Empty).Trim();
        title = title.ToLower();
        title = title.Replace(" & ", " and ");
        title = ComparableTitleRegex().Replace(title, string.Empty);
        if (year != null)
            title += year.Value.ToString();
        return title;
    }

    public static string ToComparable(this string? title)
    {
        title = (title + string.Empty).Trim();
        title = title.ToLower();
        title = title.Replace(" & ", " and ");
        return ComparableTitleRegex().Replace(title, string.Empty);
    }

    public static List<string>? SplitOmdbString(this string? s)
    {
        if (s.IsNullOrWhiteSpace())
            return null;

        if (s.ICEquals("N/A"))
            return null;

        return s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(_ => _.Trim()).Distinct().Order().ToList().ToNonEmpty();
    }

    public static string? GetMovieExtraTitle(this string title)
    {
        try
        {
            title = title.Trim();

            var editions = new string[]
            {
                "(unrated)",
                "(unrated edition)",
                "(extreme unrated)",
            };
            foreach (string edition in editions)
                if (title.ICEndsWith(edition))
                    return title[(title.Length - edition.Length - 1)..];


            editions =
            [
                "(unrated director's cut)",
                "(unrated directors cut)"
            ];
            foreach (string edition in editions)
                if (title.ICEndsWith(edition))
                    return title[(title.Length - edition.Length - 1)..];

            editions =
            [
                "(director's cut)",
                "(directors cut)",
                "(extended cut)",
                "(extended edition)",
                "(extended version)",
                "(extended)",
                "(uncut)",
                "(special edition)",
                "(theatrical edition)",
                "(theatrical version)",
                "(theatrical release)",
                "(remastered)"
            ];
            foreach (string edition in editions)
                if (title.ICEndsWith(edition))
                    return title[(title.Length - edition.Length - 1)..];
        }
        catch { }

        return null;
    }

    public static (string Query, string ComparableTitle, string? ExtraTitle) SplitTitle(this string? title, int? year)
    {
        if (title.IsNullOrWhiteSpace())
            throw new Exception();

        string? extraTitle = title.GetMovieExtraTitle();
        if (!extraTitle.IsNullOrWhiteSpace())
            title = title[..^extraTitle.Length].Trim();

        string query = title.ToQuery();

        title = title.ToComparable(year);

        return (query, title, extraTitle);
    }


    public static Query Copy(this Query query)
    {
        var ret = new Query
        {
            ImdbId = query.ImdbId,
            TmdbId = query.TmdbId,
            TvdbId = query.TvdbId,
            Title = query.Title,
            Year = query.Year
        };
        ret.Cleanup();
        return ret;
    }

    public static void Cleanup(this Query query)
    {
        query.ImdbId = query.ImdbId.ToNonEmpy();
        query.TmdbId = query.TmdbId.ToNumericId();
        query.TvdbId = query.TvdbId.ToNumericId();
        query.Title = query.Title.ToNonEmpy();
        query.Year = query.Year.ToYear();
    }



    public static bool HasRating(this Movie movieMetadata) =>
        movieMetadata.MovieRating != null &&
        movieMetadata.MovieRating != MovieRatings.None &&
        movieMetadata.MovieRating != MovieRatings.Unrated;

    public static bool HasRating(this Series seriesMetadata) =>
        seriesMetadata.TVRating != null &&
        seriesMetadata.TVRating != TVRatings.None &&
        seriesMetadata.TVRating != TVRatings.NotRated;


    public static Movie ToMovie(this Query query) =>
        new()
        {
            ImdbId = query.ImdbId,
            TmdbId = query.TmdbId,
            TvdbId = query.TvdbId
        };

    public static Series ToSeries(this Query query) =>
        new()
        {
            ImdbId = query.ImdbId,
            TmdbId = query.TmdbId,
            TvdbId = query.TvdbId
        };

    public static void CopyToQuery(this Movie movie, Query query)
    {
        query.ImdbId ??= movie.ImdbId;
        query.TmdbId ??= movie.TmdbId;
        query.TvdbId ??= movie.TvdbId;
        query.Title ??= movie.Title;
        query.Year ??= movie.ReleaseDate?.Year;
    }

    public static void CopyToQuery(this Series series, Query query)
    {
        query.ImdbId ??= series.ImdbId;
        query.TmdbId ??= series.TmdbId;
        query.TvdbId ??= series.TvdbId;
        query.Title ??= series.Title;
        query.Year ??= series.Year;
    }

    public static bool Complete(this Query query)
    {
        query.Cleanup();
        if (query.ImdbId == null) return false;
        if (query.TmdbId == null) return false;
        if (query.TvdbId == null) return false;
        if (query.Title == null) return false;
        if (query.Year == null) return false;
        return true;
    }



    public static void Cleanup(this Movie movie)
    {
        movie.ImdbId = movie.ImdbId.ToNonEmpy();
        movie.TvdbId = movie.TvdbId.ToNumericId();
        movie.TmdbId = movie.TmdbId.ToNumericId();
        movie.Title = movie.Title.ToNonEmpy();
        movie.Genres = movie.Genres.ToNonEmpty();
        movie.Overview = movie.Overview.ToNonEmpy();
        movie.Cast = movie.Cast.ToNonEmpty();
        movie.Directors = movie.Directors.ToNonEmpty();
        movie.Producers = movie.Producers.ToNonEmpty();
        movie.Writers = movie.Writers.ToNonEmpty();

        if (!movie.HasRating())
            movie.MovieRating = null;

        if (movie.ReleaseDate.HasValue && movie.Year < 1901)
            movie.ReleaseDate = null;
    }

    public static bool CompleteMetadata(this Movie movie)
    {
        movie.Cleanup();
        if (movie.ImdbId == null) return false;
        if (movie.TmdbId == null) return false;
        if (movie.TvdbId == null) return false;
        if (movie.Title == null) return false;
        if (movie.Genres == null) return false;
        if (movie.Overview == null) return false;
        if (movie.ReleaseDate == null) return false;
        if (movie.Year < 1901) return false;
        if (!movie.HasRating()) return false;
        if (movie.Cast == null) return false;
        if (movie.Directors == null) return false;
        if (movie.Producers == null) return false;
        if (movie.Writers == null) return false;
        return true;
    }



    public static void Cleanup(this Series series)
    {
        series.ImdbId = series.ImdbId.ToNonEmpy();
        series.TmdbId = series.TmdbId.ToNumericId();
        series.TvdbId = series.TvdbId.ToNumericId();
        series.Title = series.Title.ToNonEmpy();
        series.Year = series.Year.ToYear();
        series.TvdbSlug = series.TvdbSlug.ToNonEmpy();
        series.Genres = series.Genres.ToNonEmpty();
        series.Overview = series.Overview.ToNonEmpy();

        if (!series.HasRating())
            series.TVRating = null;
    }

    public static bool CompleteMetadata(this Series series)
    {
        series.Cleanup();
        if (series.ImdbId == null) return false;
        if (series.TmdbId == null) return false;
        if (series.TvdbId == null) return false;
        if (series.Title == null) return false;
        if (series.Year == null) return false;
        if (series.TvdbSlug == null) return false;
        if (series.Genres == null) return false;
        if (series.Overview == null) return false;
        if (series.TVRating == null) return false;
        return true;
    }


    public static void Cleanup(this Episode episode)
    {
        episode.Cast = episode.Cast.ToNonEmpty();
        episode.Directors = episode.Directors.ToNonEmpty();
        episode.FirstAired = episode.FirstAired?.Year > 1900 ? episode.FirstAired : null;
        episode.ImdbId = episode.ImdbId.ToNonEmpy();
        episode.Overview = episode.Overview.ToNonEmpy();
        episode.Title = episode.Title.ToNonEmpy();
        episode.TmdbId = episode.TmdbId.ToNumericId();
        episode.TvdbId = episode.TvdbId.ToNumericId();
        episode.Writers = episode.Writers.ToNonEmpty();
    }

    public static bool CompleteMetadata(this Episode episode)
    {
        episode.Cleanup();
        if (episode.Cast == null) return false;
        if (episode.Directors == null) return false;
        if (episode.FirstAired == null) return false;
        if (episode.ImdbId == null) return false;
        if (episode.Overview == null) return false;
        //if (episode.Producers == null) return false;
        if (episode.Title == null) return false;
        if (episode.TmdbId == null) return false;
        if (episode.TvdbId == null) return false;
        if (episode.Writers == null) return false;
        return true;
    }


    public static bool Process(this TMDB.Models.Find.FindMovieResult data, Query query)
    {
        bool changed = false;
        try
        {
            query.TmdbId = data.Id.ToNumericId();
            changed |= query.TmdbId != null;

            if (query.Title == null)
            {
                query.Title = data.Title.ToNonEmpy();
                changed |= query.Title != null;
            }

            if (query.Year is null && data.ReleaseDate != null)
            {
                query.Year = data.ReleaseDate.ToYear();
                changed |= query.Year != null;
            }
        }
        catch { }
        return changed;
    }


    public static bool Process(this TMDB.Models.Find.FindTvResult data, Query query)
    {
        bool changed = false;
        try
        {
            query.TmdbId = data.Id.ToNumericId();
            changed |= query.TmdbId != null;

            if (query.Title == null)
            {
                query.Title = data.Name.ToNonEmpy();
                changed |= query.Title != null;
            }

            if (query.Year == null && data.FirstAirDate != null)
            {
                query.Year = data.FirstAirDate.ToYear();
                changed |= query.Year != null;
            }
        }
        catch { }
        return changed;
    }

    public static TVDBRemoteIds Process(this List<RemoteId>? remoteIds)
    {
        int? tmdbId = null;
        string? imdbId = null;

        try
        {
            foreach (var remoteId in remoteIds ?? [])
            {
                switch (remoteId.SourceName)
                {
                    case "TheMovieDB":
                        if (int.TryParse(remoteId.Id, out int ival))
                            tmdbId = ival.ToNumericId();
                        break;
                    case "IMDB":
                        imdbId ??= remoteId.Id.ToNonEmpy();
                        break;

                    default:
                        break;
                }
            }
        }
        catch { }

        return new TVDBRemoteIds { TmdbId = tmdbId, ImdbId = imdbId };
    }

}
