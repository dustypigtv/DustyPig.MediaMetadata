using DustyPig.API.v3.MPAA;
using DustyPig.TVDB.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EpisodeAppend = DustyPig.TMDB.Models.TvEpisodes.AppendToResponse;
using MovieAppend = DustyPig.TMDB.Models.Movies.AppendToResponse;
using MovieReleaseTypes = DustyPig.TMDB.Models.Common.CommonReleaseTypes;
using TvAppend = DustyPig.TMDB.Models.TvSeries.AppendToResponse;

namespace DustyPig.MediaMetadata;


public class MetaClient(Configuration configuration, HttpClient? httpClient = null)
{
    private readonly ClientFactory _clientFactory = new(configuration, httpClient);



    #region Movies


    public async Task<Movie> GetMovieMetadata(Query query, CancellationToken cancellationToken = default)
    {
        string? edition = null;
        if (!query.Title.IsNullOrWhiteSpace())
            edition = query.Title.SplitTitle(query.Year ?? 1900).ExtraTitle;

        query = await GetMovieIds(query, cancellationToken).ConfigureAwait(false);
        var ret = query.ToMovie();
        ret.Edition = edition;


        if (ret.TmdbId.HasValue)
        {
            await TmdbMovieDetails(ret, false, cancellationToken).ConfigureAwait(false);
            if (ret.CompleteMetadata())
                return ret;
        }


        if (ret.TvdbId.HasValue)
        {
            await TvdbMovieDetails(ret, false, cancellationToken).ConfigureAwait(false);
            if (ret.CompleteMetadata())
                return ret;
        }

        if (ret.ImdbId != null)
            await ImdbMovieDetails(ret, false, cancellationToken).ConfigureAwait(false);

       
        return ret;
    }





    private async Task<Query> GetMovieIds(Query query, CancellationToken cancellationToken = default)
    {
        var ret = query.Copy();

        bool tmdbSearchByImdbId = false;
        bool tmdbSearchByTvdbId = false;
        bool tmdbSearchByTitle = false;
        bool tmdbDetails = false;

        bool tvdbSearchByImdb = false;
        bool tvdbSearchByTmdbId = false;
        bool tvdbSearchByTitle = false;
        bool tvdbDetails = false;

        bool imdbSearchByTitle = false;
        bool imdDetails = false;

        bool changed = true;
        while (changed)
        {
            changed = false;

            if (ret.Complete())
                break;

            // ***** TMDB *****
            if (ret.TmdbId == null && ret.ImdbId != null && tmdbSearchByImdbId == false)
            {
                tmdbSearchByImdbId = true;
                changed |= await TmdbMovieSearchByImdbId(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TmdbId == null && ret.TvdbId != null && tmdbSearchByTvdbId == false)
            {
                tmdbSearchByTvdbId = true;
                changed |= await TmdbMovieSearchByTvdbId(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TmdbId == null && ret.Title != null && ret.Year != null && tmdbSearchByTitle == false)
            {
                tmdbSearchByTitle = true;
                changed |= await TmdbMovieSearchByTitle(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TmdbId != null && tmdbDetails == false)
            {
                tmdbDetails = true;
                var movie = ret.ToMovie();
                changed |= await TmdbMovieDetails(movie, true, cancellationToken).ConfigureAwait(false);
                movie.CopyToQuery(ret);
                if (ret.Complete())
                    break;
            }


            // ***** TVDB *****
            if (ret.TvdbId == null && ret.ImdbId != null && tvdbSearchByImdb == false)
            {
                tvdbSearchByImdb = true;
                changed |= await TvdbMovieSearchByImdbId(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TvdbId == null && ret.TmdbId != null && tvdbSearchByTmdbId == false)
            {
                tvdbSearchByTmdbId = true;
                changed |= await TvdbMovieSearchByTmdbId(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TvdbId == null && ret.Title != null && ret.Year != null && tvdbSearchByTitle == false)
            {
                tvdbSearchByTitle = true;
                changed |= await TvdbMovieSearchByTitle(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TvdbId != null && tvdbDetails == false)
            {
                tvdbDetails = true;
                var movie = ret.ToMovie();
                changed |= await TvdbMovieDetails(movie, true, cancellationToken).ConfigureAwait(false);
                movie.CopyToQuery(ret);
                if (ret.Complete())
                    break;
            }




            // ***** OMDB *****
            if (ret.ImdbId == null && ret.Title != null && ret.Year != null && imdbSearchByTitle == false)
            {
                imdbSearchByTitle = true;
                changed |= await ImdbMovieSearchByTitle(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.ImdbId != null && imdDetails == false)
            {
                imdDetails = true;
                var movie = ret.ToMovie();
                changed |= await ImdbMovieDetails(movie, true, cancellationToken).ConfigureAwait(false);
                movie.CopyToQuery(ret);
                if (ret.Complete())
                    break;
            }
        }

        if (ret.TmdbId != null && !(tmdbSearchByTitle || tmdbDetails))
        {
            //Ensures Title and Year are from Tmdb, the movie authority
            var movie = ret.ToMovie();
            await TmdbMovieDetails(movie, true, cancellationToken).ConfigureAwait(false);
            movie.CopyToQuery(ret);
        }

        return ret;
    }





    private async Task<bool> TmdbMovieSearchByTitle(Query query, CancellationToken cancellationToken)
    {
        //Title search is for finding the TmdbId
        if (query.TmdbId != null)
            return false;

        //If no title or year, nothing to search with
        if (query.Title == null || query.Year == null)
            return false;

        bool changed = false;
        try
        {
            var parts = query.Title.SplitTitle(query.Year);

            var client = _clientFactory.GetTMDBClient();
            var response = await client.Endpoints.Search.MoviesAsync(parts.Query, year: query.Year, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            foreach (var item in response.Data!.Results)
            {
                string testTitle = item.Title.ToComparable(item.ReleaseDate);
                if (parts.ComparableTitle == testTitle)
                {
                    query.TmdbId = item.Id.ToNumericId();
                    changed |= query.TmdbId != null;

                    if (!item.Title.IsNullOrWhiteSpace())
                    {
                        query.Title = item.Title;
                        changed = true;

                        if (!query.Year.HasValue)
                            if (item.ReleaseDate.HasValue && item.ReleaseDate.Value.Year >= 1901)
                                query.Year = item.ReleaseDate?.Year;
                    }
                    break;
                }
            }
        }
        catch { }
        return changed;
    }


    private async Task<bool> TmdbMovieSearchByTvdbId(Query query, CancellationToken cancellationToken)
    {
        if (query.TvdbId == null || query.TmdbId.HasValue)
            return false;

        bool changed = false;
        try
        {
            var tmdbClient = _clientFactory.GetTMDBClient();
            var response = await tmdbClient.Endpoints.Find.ByIdAsync(query.TvdbId.ToString(), TMDB.Models.Find.Externalsource.TvdbId, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();
            return response.Data!.MovieResults.Where(_ => _.MediaType == TMDB.Models.Common.CommonMediaTypes.Movie).First().Process(query);
        }
        catch { }
        return changed;
    }


    private async Task<bool> TmdbMovieSearchByImdbId(Query query, CancellationToken cancellationToken)
    {
        if (query.ImdbId == null || query.TmdbId.HasValue)
            return false;

        bool changed = false;
        try
        {
            var tmdbClient = _clientFactory.GetTMDBClient();
            var response = await tmdbClient.Endpoints.Find.ByIdAsync(query.ImdbId, TMDB.Models.Find.Externalsource.ImdbId, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();
            return response.Data!.MovieResults.Where(_ => _.MediaType == TMDB.Models.Common.CommonMediaTypes.Movie).First().Process(query);
        }
        catch { }
        return changed;
    }


    private async Task<bool> TmdbMovieDetails(Movie movie, bool idsOnly, CancellationToken cancellationToken)
    {
        //Existing tmdb details overrites data

        if (movie.TmdbId == null)
            return false;


        bool changed = false;
        try
        {
            try
            {
                var tmdbClient = _clientFactory.GetTMDBClient();
                var response = await tmdbClient.Endpoints.Movies.GetDetailsAsync(movie.TmdbId.Value, MovieAppend.ExternalIds | MovieAppend.Credits | MovieAppend.Images | MovieAppend.ReleaseDates | MovieAppend.Translations, cancellationToken: cancellationToken).ConfigureAwait(false);
                response.ThrowIfError();

                response.Data!.ExternalIds ??= new();
                if (movie.TvdbId == null)
                {
                    movie.TvdbId = response.Data.ExternalIds.TvdbId.ToNumericId();
                    changed |= movie.TvdbId != null;
                }

                if (movie.ImdbId == null)
                {
                    movie.ImdbId = response.Data.ImdbId.ToNonEmpy();
                    changed |= movie.ImdbId != null;
                }

                string? title = Coalesce
                    (
                        response.Data.Translations?.Translations?.Where(_ => _.LanguageCode.ICEquals("en")).FirstOrDefault()?.Data.Title.ToNonEmpy(),
                        response.Data.Title.ToNonEmpy()
                    );
                if (title != null && movie.Title != title)
                {
                    changed = true;
                    movie.Title = title;
                }

                response.Data.ReleaseDates ??= new();
                response.Data.ReleaseDates.Results ??= [];
                DateOnly? releaseDate = response.Data.ReleaseDate;
                if (releaseDate == null)
                {
                    foreach (var countryCode in new string?[] { "US", null })
                    {
                        foreach (var languageCode in new string?[] { "en", null })
                        {
                            foreach (var releaseType in new MovieReleaseTypes?[] { MovieReleaseTypes.Theatrical, null })
                            {
                                IEnumerable<TMDB.Models.Movies.Releases> rdrQ = response.Data.ReleaseDates.Results;
                                if (countryCode != null)
                                    rdrQ = rdrQ.Where(r => r.CountryCode == countryCode);

                                foreach (var rdr in rdrQ)
                                {
                                    IEnumerable<TMDB.Models.Movies.Release> rQ = rdr.ReleaseDates;
                                    if (languageCode != null)
                                        rQ = rQ.Where(r => r.LanguageCode == languageCode);

                                    if (releaseType.HasValue)
                                        rQ = rQ.Where(_ => _.Type == releaseType);

                                    foreach (var r in rQ)
                                        if (r.ReleaseDate.HasValue && r.ReleaseDate.Value.Year > 1900)
                                        {
                                            releaseDate = r.ReleaseDate;
                                            break;
                                        }
                                    if (releaseDate != null)
                                        break;
                                }
                                if (releaseDate != null)
                                    break;
                            }
                            if (releaseDate != null)
                                break;
                        }
                        if (releaseDate != null)
                            break;
                    }
                }
                if (releaseDate != null)
                {
                    changed |= movie.ReleaseDate != releaseDate;
                    movie.ReleaseDate = releaseDate;
                }


                if (!idsOnly)
                {
                    movie.Overview = Coalesce
                        (
                            response.Data.Translations?.Translations?.Where(_ => _.LanguageCode.ICEquals("en")).FirstOrDefault()?.Data?.Overview.ToNonEmpy(),
                            response.Data.Overview.ToNonEmpy(),
                            movie.Overview
                        );
                    movie.Genres = Coalesce(response.Data.Genres?.Select(_ => _.Name).Distinct().Order().ToList().ToNonEmpty(), movie.Genres);

                    MovieRatings? movieRating = null;
                    foreach (var release in (response.Data.ReleaseDates?.Results ?? []).OrderBy(_ => !_.CountryCode.ICEquals("US")))
                    {
                        foreach (var candidateReleaseDate in release.ReleaseDates)
                        {
                            if (TryMapMovieRatings(release.CountryCode, candidateReleaseDate.Certification, out string? rating))
                            {
                                movieRating = rating.ToMovieRatings();
                                if (movieRating == MovieRatings.None || movieRating == MovieRatings.Unrated)
                                    movieRating = rating.ToTVRatings().ToMovieRatings();
                                if (movieRating == MovieRatings.None || movieRating == MovieRatings.Unrated)
                                    movieRating = null;
                            }
                            if (movieRating != null)
                                break;
                        }
                        if (movieRating != null)
                            break;
                    }
                    if (movieRating != null)
                        movie.MovieRating = movieRating;


                    response.Data.Credits ??= new TMDB.Models.Movies.Credits();
                    response.Data.Credits.Cast ??= [];
                    response.Data.Credits.Crew ??= [];

                    movie.Cast = Coalesce(response.Data.Credits.Cast.OrderBy(_ => _.Order).Select(_ => _.Name).ToList().ToNonEmpty(), movie.Cast);
                    movie.Directors = Coalesce(response.Data.Credits.Crew.Where(_ => _.Job.ICEquals("Director")).Select(_ => _.Name).ToList().ToNonEmpty(), movie.Directors);

                    var producers = response.Data.Credits.Crew.Where(_ => _.Job.ICEquals("Executive Producer")).Select(_ => _.Name).ToList();
                    if (producers.Count == 0)
                        producers.AddRange([.. response.Data.Credits.Crew.Where(_ => _.Job.ICEquals("Producer")).Select(_ => _.Name)]);
                    movie.Producers = Coalesce(producers.ToNonEmpty(), movie.Producers);


                    var writers = response.Data.Credits.Crew.Where(_ => _.Job.ICEquals("Screenplay")).Select(_ => _.Name).ToList();
                    if (writers.Count == 0)
                        writers.AddRange([.. response.Data.Credits.Crew.Where(_ => _.Job.ICEquals("Story")).Select(_ => _.Name)]);
                    movie.Writers = Coalesce(writers.ToNonEmpty(), movie.Writers);


                    response.Data.Images ??= new TMDB.Models.Common.CommonImages2();
                    response.Data.Images.Posters ??= [];
                    response.Data.Images.Backdrops ??= [];
                    bool posterWasSet = false;
                    bool backdropWasSet = false;
                    foreach (bool getMoreImages in new bool[] { false, true })
                    {
                        if (posterWasSet && backdropWasSet)
                            break;

                        if (getMoreImages)
                        {
                            try
                            {
                                if ((response.Data.PosterPath.IsNullOrWhiteSpace() && response.Data.Images.Posters.Count == 0)
                                || (response.Data.BackdropPath.IsNullOrWhiteSpace() && response.Data.Images.Backdrops.Count == 0))
                                {

                                    var imageResponse = await tmdbClient.Endpoints.Movies.GetImagesAsync(response.Data.Id, language: null, cancellationToken: cancellationToken).ConfigureAwait(false);
                                    response.Data.Images.Posters.AddRange(imageResponse.Data!.Posters);
                                    response.Data.Images.Backdrops.AddRange(imageResponse.Data.Backdrops);
                                }
                            }
                            catch { }
                        }

                        if (!posterWasSet)
                        {
                            string? url = response.Data.PosterPath.ToNonEmpy();
                            if (url == null)
                                try { url = response.Data.Images.Posters.First(item => item.LanguageCode == "en").FilePath.ToNonEmpy(); }
                                catch { }

                            if (url == null)
                                try { url = response.Data.Images.Posters.First().FilePath.ToNonEmpy(); }
                                catch { }

                            if (url != null)
                            {
                                movie.PosterUrl = TMDB.Utils.GetFullSizeImageUrl(url).ToNonEmpy();
                                posterWasSet = true;
                            }
                        }

                        if (!backdropWasSet)
                        {
                            string? url = response.Data.BackdropPath.ToNonEmpy();
                            if (url == null)
                                try { url = response.Data.Images.Backdrops.OrderByDescending(p => p.VoteAverage).First().FilePath.ToNonEmpy(); }
                                catch { }
                            if (url != null)
                            {
                                movie.BackdropUrl = TMDB.Utils.GetFullSizeImageUrl(url).ToNonEmpy();
                                backdropWasSet = true;
                            }
                        }
                    }
                }
            }
            catch { }
        }
        catch { }
        return changed;
    }





    private async Task<bool> TvdbMovieSearchByTitle(Query query, CancellationToken cancellationToken)
    {
        //Title search is for finding the TvdbId
        if (query.TvdbId != null)
            return false;

        //If no title or year, nothing to search with
        if (query.Title == null || query.Year == null)
            return false;

        bool changed = false;
        try
        {
            var parts = query.Title.SplitTitle(query.Year);

            var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
            var response = await tvdbClient.Search.SearchAsync(parts.Query, SearchTypes.Movie, query.Year, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();
            foreach (var item in response.Data)
            {
                string testTitle = item.Name.ToComparable(item.Year);
                if (testTitle == parts.ComparableTitle)
                {
                    query.TvdbId = item.TVDB_Id.ToNumericId();
                    if (query.TvdbId > 0)
                    {
                        changed = true;
                        var ids = item.RemoteIds.Process();
                        query.ImdbId ??= ids.ImdbId;
                        query.TmdbId ??= ids.TmdbId;
                    }
                    break;
                }
            }
        }
        catch { }
        return changed;
    }


    private async Task<bool> TvdbMovieSearchByImdbId(Query query, CancellationToken cancellationToken)
    {
        if (query.TvdbId.HasValue || query.ImdbId == null)
            return false;

        bool changed = false;
        try
        {
            var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
            var response = await tvdbClient.Search.SearchByRemoteIdAsync(query.ImdbId, cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            query.TvdbId = response.Data.Where(_ => _.Movie != null).First().Movie.Id.ToNumericId();
            changed = query.TvdbId != null;
        }
        catch { }
        return changed;
    }


    private async Task<bool> TvdbMovieSearchByTmdbId(Query query, CancellationToken cancellationToken)
    {
        if (query.TvdbId.HasValue || query.TmdbId == null)
            return false;

        bool changed = false;
        try
        {
            var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
            var response = await tvdbClient.Search.SearchByRemoteIdAsync(query.TmdbId.Value.ToString(), cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();
            if (query.Title == null)
            {
                query.TvdbId = response.Data.Where(_ => _.Movie != null).First().Movie.Id.ToNumericId();
                changed = query.TvdbId != null;
            }
            else
            {
                var parts = query.Title.SplitTitle(query.Year);
                foreach (var item in response.Data.Where(_ => _.Movie != null).Select(_ => _.Movie))
                {
                    string comp = item.Name.ToComparable(query.Year == null ? null : item.Year);
                    if (parts.ComparableTitle == comp)
                    {
                        query.TvdbId = item.Id.ToNumericId();
                        if (query.TvdbId != null)
                        {
                            changed = true;
                            query.Title ??= item.Name.ToNonEmpy();
                            break;
                        }
                    }
                }
            }
        }
        catch { }
        return changed;
    }


    private async Task<bool> TvdbMovieDetails(Movie movie, bool idsOnly, CancellationToken cancellationToken)
    {
        const int TVDB_IMAGE_TYPE_MOVIE_POSTER = 14;
        const int TVDB_IMAGE_TYPE_MOVIE_BACKDROP = 15;

        if (movie.TvdbId == null)
            return false;

        bool changed = false;
        try
        {
            var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
            var response = await tvdbClient.Movies.GetExtendedAsync(movie.TvdbId.Value, true, false, cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            var ids = response.Data.RemoteIds.Process();

            if (movie.TmdbId == null)
            {
                movie.TmdbId = ids.TmdbId.ToNumericId();
                changed |= movie.TmdbId.HasValue;
            }

            if (movie.ImdbId == null)
            {
                movie.ImdbId = ids.ImdbId.ToNonEmpy();
                changed |= movie.ImdbId != null;
            }

            if (movie.Title == null)
            {
                movie.Title = Coalesce(response.Data.Translations.NameTranslations.FirstOrDefault(_ => _.Language == "eng")?.Name, response.Data.Name).ToNonEmpy();
                changed |= movie.Title != null;
            }

            if (movie.ReleaseDate == null)
            {
                response.Data.Releases ??= [];
                response.Data.Releases.Sort((x, y) =>
                {
                    int ret = -x.Country.ICEquals("usa").CompareTo(y.Country.ICEquals("usa"));
                    if (ret == 0) ret = -x.Country.ICEquals("global").CompareTo(y.Country.ICEquals("global"));
                    if (ret == 0 && DateOnly.TryParse(x.Date, out DateOnly xDate) && DateOnly.TryParse(y.Date, out DateOnly yDate))
                        ret = x.Date.CompareTo(y.Date);

                    return ret;
                });
                var selectedRelease = response.Data.Releases.FirstOrDefault();
                if (selectedRelease != null && DateOnly.TryParse(selectedRelease.Date, out DateOnly selectedDate))
                {
                    movie.ReleaseDate = selectedDate;
                    changed = true;
                }
            }

            if (!idsOnly)
            {
                movie.Overview ??= response.Data.Translations.OverviewTranslations.FirstOrDefault(_ => _.Language == "eng")?.Overview.ToNonEmpy();
                movie.Genres ??= (response.Data.Genres ?? []).Select(_ => _.Name).Distinct().Order().ToList().ToNonEmpty();
                movie.Cast ??= response.Data.Characters.Where(_ => _.IsFeatured).Select(_ => _.PersonName).ToList().ToNonEmpty();

                if (movie.PosterUrl.IsNullOrWhiteSpace())
                {
                    string? url = response.Data.Artworks
                        .Where(_ => _.Language.ICEquals("eng"))
                        .FirstOrDefault(_ => _.Type == TVDB_IMAGE_TYPE_MOVIE_POSTER)?.Image.ToNonEmpy() ??
                        response.Data.Artworks
                        .FirstOrDefault(_ => _.Type == TVDB_IMAGE_TYPE_MOVIE_POSTER)?.Image.ToNonEmpy();

                    if (url != null)
                        movie.PosterUrl = url;
                }

                if (movie.BackdropUrl.IsNullOrWhiteSpace())
                {
                    string? url = response.Data.Artworks
                        .Where(_ => _.Language.ICEquals("eng"))
                        .FirstOrDefault(_ => _.Type == TVDB_IMAGE_TYPE_MOVIE_BACKDROP)?.Image.ToNonEmpy() ??
                        response.Data.Artworks
                        .FirstOrDefault(_ => _.Type == TVDB_IMAGE_TYPE_MOVIE_BACKDROP)?.Image.ToNonEmpy();

                    if (url != null)
                        movie.BackdropUrl = url;
                }

                if (!movie.HasRating())
                {
                    response.Data.ContentRatings ??= [];
                    foreach (var cr in response.Data.ContentRatings.OrderBy(_ => !_.Country.ICEquals("usa")))
                    {
                        if (TryMapMovieRatings(cr.Country[..2], cr.Name, out string? rated))
                        {
                            movie.MovieRating = rated.ToMovieRatings();
                            if (!movie.HasRating())
                                movie.MovieRating = rated.ToTVRatings().ToMovieRatings();
                            if (!movie.HasRating())
                                movie.MovieRating = null;
                        }
                        if (movie.HasRating())
                            break;
                    }
                }
            }
        }
        catch { }
        return changed;
    }





    private async Task<bool> ImdbMovieSearchByTitle(Query query, CancellationToken cancellationToken)
    {
        if (query.ImdbId != null)
            return false;

        if (query.Title == null || query.Year == null)
            return false;

        bool changed = false;
        try
        {
            var parts = query.Title.SplitTitle(query.Year);

            var omdbClient = _clientFactory.GetOMDbClient();
            var response = await omdbClient.SearchForMovieAsync(parts.Query, query.Year, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            foreach (var item in response.Data!.Search)
            {
                string testTitle = item.Title.ToComparable(item.Year);
                if (parts.ComparableTitle == testTitle)
                {
                    query.ImdbId = item.ImdbId.ToNonEmpy();
                    changed |= query.ImdbId != null;
                    break;
                }
            }
        }
        catch { }
        return changed;
    }


    private async Task<bool> ImdbMovieDetails(Movie movie, bool idsOnly, CancellationToken cancellationToken)
    {
        if (movie.ImdbId == null)
            return false;

        if (movie.Title != null && movie.Year.HasValue)
            return false;

        bool changed = false;
        try
        {
            var omdbClient = _clientFactory.GetOMDbClient();
            var response = await omdbClient.GetMovieByIdAsync(movie.ImdbId, true, cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            if (movie.Title == null)
            {
                movie.Title = response.Data!.Title.ToNonEmpy();
                changed |= movie.Title != null;
            }

            if (movie.ReleaseDate == null)
            {
                if (DateOnly.TryParse(response.Data!.Released, out DateOnly dt) && dt.Year > 1900)
                    movie.ReleaseDate = dt;
                changed |= movie.ReleaseDate != null;
            }

            if (!idsOnly)
            {
                movie.Genres = Coalesce(movie.Genres, response.Data!.Genre.SplitOmdbString());
                movie.Cast = Coalesce(movie.Cast, response.Data.Actors.SplitOmdbString());
                movie.Directors = Coalesce(movie.Directors, response.Data.Director.SplitOmdbString());
                movie.Writers = Coalesce(movie.Writers, response.Data.Writer.SplitOmdbString());

                if (!movie.HasRating())
                    movie.MovieRating = (response.Data.Rated + string.Empty).ToMovieRatings();

                if (response.Data.Poster != null)
                    movie.PosterUrl ??= response.Data.Poster;
            }
        }
        catch { }
        return changed;
    }


    #endregion





    #region Series



    public async Task<Series> GetSeriesMetadata(Query query, CancellationToken cancellationToken = default)
    {
        query = await GetSeriesIds(query, cancellationToken).ConfigureAwait(false);
        var ret = query.ToSeries();

        if (ret.TvdbId.HasValue)
        {
            await TvdbSeriesDetails(ret, false, cancellationToken).ConfigureAwait(false);
            if (ret.CompleteMetadata())
                return ret;
        }

        if (ret.TmdbId.HasValue)
        {
            await TmdbSeriesDetails(ret, false, cancellationToken).ConfigureAwait(false);
            if (ret.CompleteMetadata())
                return ret;
        }

        if (ret.ImdbId != null)
            await ImdbSeriesDetails(ret, false, cancellationToken).ConfigureAwait(false);

        return ret;
    }





    private async Task<Query> GetSeriesIds(Query query, CancellationToken cancellationToken = default)
    {
        var ret = query.Copy();

        bool tvdbSearchByImdb = false;
        bool tvdbSearchByTmdbId = false;
        bool tvdbSearchByTitle = false;
        bool tvdbDetails = false;

        bool tmdbSearchByImdbId = false;
        bool tmdbSearchByTvdbId = false;
        bool tmdbSearchByTitle = false;
        bool tmdbDetails = false;

        bool imdbSearchByTitle = false;
        bool imdbDetails = false;


        bool changed = true;
        while (changed)
        {
            changed = false;

            if (ret.Complete())
                break;

            // ***** TVDB *****
            if (ret.TvdbId == null && ret.ImdbId != null && tvdbSearchByImdb == false)
            {
                tvdbSearchByImdb = true;
                changed |= await TvdbSeriesSearchByImdbId(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TvdbId == null && ret.TmdbId != null && tvdbSearchByTmdbId == false)
            {
                tvdbSearchByTmdbId = true;
                changed |= await TvdbSeriesSearchByTmdbId(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TvdbId == null && ret.Title != null && ret.Year != null && tvdbSearchByTitle == false)
            {
                tvdbSearchByTitle = true;
                changed |= await TvdbSeriesSearchByTitle(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TvdbId == null && ret.Title != null && tvdbSearchByTitle == false)
            {
                tvdbSearchByTitle = true;
                changed |= await TvdbSeriesSearchByTitle(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TvdbId != null && tvdbDetails == false)
            {
                tvdbDetails = true;
                var series = ret.ToSeries();
                changed |= await TvdbSeriesDetails(series, true, cancellationToken).ConfigureAwait(false);
                series.CopyToQuery(ret);
                if (ret.Complete())
                    break;
            }



            // ***** TMDB *****
            if (ret.TmdbId == null && ret.ImdbId != null && tmdbSearchByImdbId == false)
            {
                tmdbSearchByImdbId = true;
                changed |= await TmdbSeriesSearchByImdbId(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TmdbId == null && ret.TvdbId != null && tmdbSearchByTvdbId == false)
            {
                tmdbSearchByTvdbId = true;
                changed |= await TmdbSeriesSearchByTvdb(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TmdbId == null && ret.Title != null && ret.Year != null && tmdbSearchByTitle == false)
            {
                tmdbSearchByTitle = true;
                changed |= await TmdbSeriesSearchByTitle(ret, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.TmdbId != null && tmdbDetails == false)
            {
                tmdbDetails = true;
                var series = ret.ToSeries();
                changed |= await TmdbSeriesDetails(series, true, cancellationToken).ConfigureAwait(false);
                series.CopyToQuery(ret);
                if (ret.Complete())
                    break;
            }


            // ***** OMDB *****
            if (ret.ImdbId == null && ret.Title != null && ret.Year != null && imdbSearchByTitle == false)
            {
                imdbSearchByTitle = true;
                changed |= await ImdbSeriesSearchByTitle(query, cancellationToken).ConfigureAwait(false);
                if (ret.Complete())
                    break;
            }

            if (ret.ImdbId != null && imdbDetails == false)
            {
                imdbDetails = true;
                var series = ret.ToSeries();
                changed |= await ImdbSeriesDetails(series, true, cancellationToken).ConfigureAwait(false);
                series.CopyToQuery(ret);
                if (ret.Complete())
                    break;
            }
        }


        if (ret.TvdbId != null && tvdbDetails == false)
        {
            var series = ret.ToSeries();
            await TvdbSeriesDetails(series, true, cancellationToken).ConfigureAwait(false);
            series.CopyToQuery(ret);
        }


        return ret;
    }





    private async Task<bool> TvdbSeriesSearchByTitle(Query query, CancellationToken cancellationToken)
    {
        if (query.TvdbId != null)
            return false;

        //if (query.Title == null || query.Year == null)
        if (query.Title == null)
            return false;

        bool changed = false;
        try
        {
            var parts = query.Title.SplitTitle(query.Year);

            var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
            var response = await tvdbClient.Search.SearchAsync(parts.Query, SearchTypes.Series, query.Year, language: "eng", cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();


            if (query.ImdbId != null || query.TmdbId != null)
            {
                foreach (var item in response.Data)
                {
                    var ids = item.RemoteIds.Process();
                    if (query.ImdbId != null && query.ImdbId == ids.ImdbId)
                    {
                        query.TvdbId = item.TVDB_Id.ToNumericId();
                        changed |= query.TvdbId.HasValue;
                        if (query.TvdbId.HasValue)
                        {
                            query.TmdbId ??= ids.TmdbId;
                            query.Title = item.Title.ToNonEmpy();
                            query.Year = item.Year.ToYear();
                            break;
                        }
                    }

                    if (query.TmdbId != null && query.TmdbId == ids.TmdbId)
                    {
                        query.TvdbId = item.TVDB_Id.ToNumericId();
                        changed |= query.TvdbId.HasValue;
                        if (query.TvdbId.HasValue)
                        {
                            query.ImdbId ??= ids.ImdbId;
                            query.Title = item.Name.ToNonEmpy();
                            query.Year = item.Year.ToYear();
                            break;
                        }
                    }
                }
            }

            if (query.TvdbId == null)
            {
                foreach (bool translate in new bool[] { false, true })
                {
                    foreach (bool skipYear in new bool[] { false, true })
                    {
                        foreach (var item in response.Data)
                        {
                            int? year = skipYear ? null : item.Year.ToYear();
                            string testTitle = item.Name.ToComparable(year);

                            if (translate)
                            {
                                item.Translations ??= [];
                                if (item.Translations.TryGetValue("eng", out string? translatedTestTitle))
                                    if (!translatedTestTitle.IsNullOrWhiteSpace())
                                        testTitle = translatedTestTitle.ToComparable(year);
                            }

                            if (parts.ComparableTitle == testTitle)
                            {
                                query.TvdbId = item.TVDB_Id.ToNumericId();
                                changed |= query.TvdbId.HasValue;
                                if (query.TvdbId.HasValue)
                                {
                                    var ids = item.RemoteIds.Process();
                                    query.ImdbId ??= ids.ImdbId;
                                    query.TmdbId ??= ids.TmdbId;

                                    query.Title = item.Name.ToNonEmpy();
                                    query.Year = item.Year.ToYear();
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return changed;
    }


    private async Task<bool> TvdbSeriesSearchByImdbId(Query query, CancellationToken cancellationToken)
    {
        if (query.TvdbId.HasValue || query.ImdbId == null)
            return false;

        bool changed = false;
        try
        {
            var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
            var response = await tvdbClient.Search.SearchByRemoteIdAsync(query.ImdbId, cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            var item = response.Data.Where(_ => _.Series != null).First().Series;
            query.TvdbId = item.Id.ToNumericId();
            changed = query.TvdbId.HasValue;
            if (changed)
            {
                query.Title ??= item.Name.ToNonEmpy();
                query.Year ??= item.Year.ToYear();
            }
        }
        catch { }
        return changed;
    }


    private async Task<bool> TvdbSeriesSearchByTmdbId(Query query, CancellationToken cancellationToken)
    {
        if (query.TvdbId.HasValue || query.TmdbId == null)
            return false;

        bool changed = false;
        try
        {
            var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
            var response = await tvdbClient.Search.SearchByRemoteIdAsync(query.TmdbId.Value.ToString(), cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();
            var item = response.Data.Where(_ => _.Series != null).First().Series;
            query.TvdbId = item.Id.ToNumericId();
            changed = query.TvdbId.HasValue;
            if (changed)
            {
                query.Title ??= item.Name.ToNonEmpy();
                query.Year ??= item.Year.ToYear();
            }
        }
        catch { }
        return changed;
    }


    private async Task<bool> TvdbSeriesDetails(Series series, bool idsOnly, CancellationToken cancellationToken)
    {
        //Existing tvdb data overrites data

        const int TVDB_IMAGE_TYPE_SERIES_POSTER = 2;
        const int TVDB_IMAGE_TYPE_SERIES_BACKDROP = 3;

        if (series.TvdbId == null)
            return false;

        bool changed = false;
        try
        {
            var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
            var response = await tvdbClient.Series.GetExtendedAsync(series.TvdbId.Value, Meta.Translations, false, cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            response.Data.Translations ??= new();
            response.Data.Translations.NameTranslations ??= [];
            response.Data.Translations.OverviewTranslations ??= [];

            var ids = response.Data.RemoteIds.Process();
            if (ids.ImdbId != null && series.ImdbId == null)
            {
                changed = true;
                series.ImdbId = ids.ImdbId;
            }

            if (ids.TmdbId != null && series.TmdbId == null)
            {
                changed = true;
                series.TmdbId = ids.TmdbId;
            }

            string? title = (response.Data.Translations.NameTranslations.FirstOrDefault(_ => _.Language == "eng")?.Name ?? response.Data.Name).ToNonEmpy();
            if (title != null && title != series.Title)
            {
                changed = true;
                series.Title = title;
            }

            string? overview = (response.Data.Translations.OverviewTranslations.FirstOrDefault(_ => _.Language == "eng")?.Overview ?? response.Data.Overview).ToNonEmpy();
            if (overview != null && series.Overview != overview)
            {
                changed = true;
                series.Overview = overview;
            }


            if (!idsOnly)
            {
                response.Data.Genres ??= [];
                response.Data.ContentRatings ??= [];
                response.Data.Artworks ??= [];

                series.TvdbSlug = Coalesce(response.Data.Slug.ToNonEmpy(), series.TvdbSlug);
                series.Genres = Coalesce(response.Data.Genres.Select(_ => _.Name).Distinct().Order().ToList().ToNonEmpty(), series.Genres);

                response.Data.ContentRatings ??= [];
                TVRatings? tvRatings = null;
                foreach (var cr in response.Data.ContentRatings.OrderBy(item => !item.Country.ICEquals("usa")))
                    if (TryMapTVRatings(cr.Country[..2], cr.Name, out string? rating))
                    {
                        tvRatings = rating.ToTVRatings();
                        if (tvRatings == TVRatings.None || tvRatings == TVRatings.NotRated)
                            tvRatings = rating.ToMovieRatings().ToTVRatings();
                        if (tvRatings == TVRatings.None || tvRatings == TVRatings.NotRated)
                            tvRatings = null;
                        if (tvRatings != null)
                            break;
                    }

                if (tvRatings != null)
                    series.TVRating = tvRatings;


                if (DateOnly.TryParse(response.Data.FirstAired, out var dt) && dt.Year > 1900)
                    series.Year = dt.Year;


                string? url = response.Data.Artworks
                    .Where(_ => _.Language.ICEquals("eng"))
                    .FirstOrDefault(_ => _.Type == TVDB_IMAGE_TYPE_SERIES_POSTER)?.Image ??
                    response.Data.Artworks
                    .FirstOrDefault(_ => _.Type == TVDB_IMAGE_TYPE_SERIES_POSTER)?.Image;

                if (url != null)
                    series.PosterUrl = url;

                url = response.Data.Artworks
                    .Where(_ => _.Language.ICEquals("eng"))
                    .FirstOrDefault(_ => _.Type == TVDB_IMAGE_TYPE_SERIES_BACKDROP)?.Image ??
                    response.Data.Artworks
                    .First(_ => _.Type == TVDB_IMAGE_TYPE_SERIES_BACKDROP)?.Image;

                if (url != null)
                    series.BackdropUrl = url;
            }
        }
        catch { }
        return changed;
    }





    private async Task<bool> TmdbSeriesSearchByTitle(Query query, CancellationToken cancellationToken)
    {
        if (query.TmdbId != null)
            return false;

        if (query.Title == null || query.Year == null)
            return false;

        bool changed = false;
        try
        {
            var parts = query.Title.SplitTitle(query.Year);

            var tmdbClient = _clientFactory.GetTMDBClient();
            var response = await tmdbClient.Endpoints.Search.TvSeriesAsync(parts.Query, year: query.Year, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            foreach (var item in response.Data!.Results.Where(_ => _.FirstAirDate != null))
            {
                var testTitle = item.Name.ToComparable(item.FirstAirDate!.Value.Year);
                if (testTitle == parts.ComparableTitle)
                {
                    query.TmdbId = item.Id.ToNumericId();
                    if (query.TmdbId != null)
                    {
                        changed = true;
                        query.Title ??= item.Name.ToNonEmpy();
                        query.Year ??= item.FirstAirDate.ToYear();
                        break;
                    }
                }
            }
        }
        catch { }
        return changed;
    }


    private async Task<bool> TmdbSeriesSearchByImdbId(Query query, CancellationToken cancellationToken)
    {
        if (query.ImdbId == null || query.TmdbId.HasValue)
            return false;

        bool changed = false;
        try
        {
            var tmdbClient = _clientFactory.GetTMDBClient();
            var response = await tmdbClient.Endpoints.Find.ByIdAsync(query.ImdbId, TMDB.Models.Find.Externalsource.ImdbId, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();
            return response.Data!.TvResults.Where(_ => _.MediaType == TMDB.Models.Common.CommonMediaTypes.TvSeries).First().Process(query);
        }
        catch { }
        return changed;
    }


    private async Task<bool> TmdbSeriesSearchByTvdb(Query query, CancellationToken cancellationToken)
    {
        if (query.TvdbId == null || query.TmdbId.HasValue)
            return false;

        bool changed = false;
        try
        {
            var tmdbClient = _clientFactory.GetTMDBClient();
            var response = await tmdbClient.Endpoints.Find.ByIdAsync(query.TvdbId.ToString(), TMDB.Models.Find.Externalsource.TvdbId, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();
            return response.Data!.MovieResults.Where(_ => _.MediaType == TMDB.Models.Common.CommonMediaTypes.TvSeries).First().Process(query);
        }
        catch { }
        return changed;
    }


    private async Task<bool> TmdbSeriesDetails(Series series, bool idsOnly, CancellationToken cancellationToken)
    {
        if (series.TmdbId == null)
            return false;

        bool changed = false;
        try
        {
            var tmdbClient = _clientFactory.GetTMDBClient();
            var response = await tmdbClient.Endpoints.TvSeries.GetDetailsAsync(series.TmdbId.Value, TvAppend.ContentRatings | TvAppend.Images | TvAppend.Translations, "en-US", cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            response.Data!.ExternalIds ??= new();
            if (series.ImdbId == null)
            {
                series.ImdbId = response.Data.ExternalIds.ImdbId.ToNonEmpy();
                changed |= series.ImdbId != null;
            }

            if (series.TvdbId == null)
            {
                series.TvdbId = response.Data.ExternalIds.TvdbId.ToNumericId();
                changed |= series.TvdbId.HasValue;
            }


            response.Data.Translations ??= new();
            response.Data.Translations.Translations ??= [];
            if (series.Title == null)
            {
                series.Title = Coalesce(response.Data.Translations.Translations.Where(_ => _.LanguageCode.ICEquals("en")).FirstOrDefault()?.Data.Name?.ToNonEmpy(), response.Data.Name.ToNonEmpy());
                changed = series.Title != null;
            }

            if (series.Year == null)
            {
                series.Year = response.Data.FirstAirDate?.Year;
                changed |= series.Year.HasValue;
            }


            if (!idsOnly)
            {
                series.Overview ??= Coalesce(response.Data.Translations.Translations.Where(_ => _.LanguageCode.ICEquals("en")).FirstOrDefault()?.Data.Overview?.ToNonEmpy(), response.Data.Overview.ToNonEmpy());

                if (!series.HasRating())
                {
                    response.Data.ContentRatings ??= new();
                    response.Data.ContentRatings.Results ??= [];
                    foreach (var contentRating in response.Data.ContentRatings.Results.OrderBy(item => !item.CountryCode.ICEquals("US")))
                        if (TryMapTVRatings(contentRating.CountryCode, contentRating.Rating, out string? rating))
                        {
                            series.TVRating = rating.ToTVRatings();
                            if (!series.HasRating())
                                series.TVRating = rating.ToMovieRatings().ToTVRatings();
                            if (!series.HasRating())
                                series.TVRating = null;
                            if (series.HasRating())
                                break;
                        }
                }


                series.Genres = Coalesce((response.Data.Genres ?? []).Select(_ => _.Name).Distinct().Order().ToList().ToNonEmpty(), series.Genres);

                if (series.PosterUrl.IsNullOrWhiteSpace() || series.BackdropUrl.IsNullOrWhiteSpace())
                {

                    //If no english images, check for any language
                    response.Data.Images ??= new TMDB.Models.Common.CommonImages2();
                    response.Data.Images.Posters ??= [];
                    response.Data.Images.Backdrops ??= [];

                    if ((response.Data.PosterPath.IsNullOrWhiteSpace() && response.Data.Images.Posters.Count == 0)
                        || (response.Data.BackdropPath.IsNullOrWhiteSpace() && response.Data.Images.Backdrops.Count == 0))
                    {
                        try
                        {
                            var imagesResponse = await tmdbClient.Endpoints.Movies.GetImagesAsync(series.TmdbId.Value, language: null, cancellationToken: cancellationToken).ConfigureAwait(false);
                            imagesResponse.Data ??= new TMDB.Models.Common.CommonImages2();

                            if (response.Data.Images.Posters.Count == 0)
                                response.Data.Images.Posters = imagesResponse.Data.Posters ?? [];

                            if (response.Data.Images.Backdrops.Count == 0)
                                response.Data.Images.Backdrops = imagesResponse.Data.Backdrops ?? [];
                        }
                        catch { }
                    }


                    if (series.PosterUrl.IsNullOrWhiteSpace())
                    {
                        string? url = response.Data.PosterPath.ToNonEmpy();
                        url ??= response.Data.Images.Posters.FirstOrDefault(item => item.LanguageCode == "en")?.FilePath.ToNonEmpy();
                        url ??= response.Data.Images.Posters.FirstOrDefault()?.FilePath.ToNonEmpy();
                        if (url != null)
                            series.PosterUrl = TMDB.Utils.GetFullSizeImageUrl(url).ToNonEmpy();
                    }

                    if (series.BackdropUrl.IsNullOrWhiteSpace())
                    {
                        string? url = response.Data.BackdropPath.ToNonEmpy();
                        url ??= response.Data.Images.Backdrops.OrderByDescending(p => p.VoteAverage).FirstOrDefault()?.FilePath.ToNonEmpy();

                        if (url != null)
                            series.BackdropUrl = TMDB.Utils.GetFullSizeImageUrl(url).ToNonEmpy();
                    }
                }
            }
        }
        catch { }
        return changed;
    }





    private async Task<bool> ImdbSeriesSearchByTitle(Query query, CancellationToken cancellationToken)
    {
        if (query.Title == null || query.Year == null)
            return false;

        bool changed = false;
        try
        {
            var parts = query.Title.SplitTitle(query.Year);

            var omdbClient = _clientFactory.GetOMDbClient();
            var response = await omdbClient.SearchForSeriesAsync(parts.Query, query.Year, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            foreach (var item in response.Data!.Search)
            {
                string testTitle = item.Title.ToComparable(item.Year);
                if (testTitle == parts.ComparableTitle)
                {
                    query.ImdbId = item.ImdbId.ToNonEmpy();
                    if (query.ImdbId != null)
                    {
                        changed = true;
                        break;
                    }
                }
            }
        }
        catch { }
        return changed;
    }


    private async Task<bool> ImdbSeriesDetails(Series series, bool idsOnly, CancellationToken cancellationToken)
    {
        if (series.ImdbId == null)
            return false;

        bool changed = false;
        try
        {
            var omdbClient = _clientFactory.GetOMDbClient();
            var response = await omdbClient.GetSeriesByIdAsync(series.ImdbId, true, cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            series.Title ??= response.Data!.Title;
            if (!response.Data!.Year.IsNullOrWhiteSpace())
                if (int.TryParse(response.Data.Year[..4], out int year))
                    series.Year ??= year;

            if (!idsOnly)
            {
                series.Overview ??= response.Data.Plot;
                series.PosterUrl ??= response.Data.Poster.ToNonEmpy();

                if (!series.HasRating())
                    series.TVRating = response.Data.Rated?.ToTVRatings();
                if (!series.HasRating())
                    series.TVRating = response.Data.Rated?.ToMovieRatings().ToTVRatings();
                if (!series.HasRating())
                    series.TVRating = null;

                series.Genres = Coalesce(series.Genres, response.Data.Genre.SplitOmdbString());
            }
        }
        catch { }
        return changed;
    }


    #endregion





    #region Episodes

    public async Task<Episode> GetEpisode(Query query, int season, int number, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query.TvdbId, $"{nameof(query)}.{nameof(query.TvdbId)}");
        ArgumentOutOfRangeException.ThrowIfLessThan(query.TvdbId.Value, 1, $"{nameof(query)}.{nameof(query.TvdbId)}");

        var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
        var eps = await tvdbClient.Series.GetEpisodesAsync(query.TvdbId.Value, SeasonTypes.Default, 0, season, number, cancellationToken: cancellationToken).ConfigureAwait(false);
        eps.ThrowIfError();
        var ep = eps.Data.Episodes.FirstOrDefault() ?? throw new Exception("Episode not found");
        return await GetEpisode(query, ep.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Episode> GetEpisode(Query query, int episodeTvdbId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(episodeTvdbId, 1, nameof(episodeTvdbId));


        var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
        var ret = await GetEpisode(tvdbClient, episodeTvdbId, cancellationToken).ConfigureAwait(false);

        string tvdbCompTitle = ret.Title.ToComparable();


        // Tvdb doesn't have cast/director/writer, so the episode metadata wont be complete yet
        try
        {
            if (query.TmdbId.HasValue)
            {
                var tmdbClient = _clientFactory.GetTMDBClient();
                tmdbClient.AutoThrowIfError = true;

                TMDB.Models.TvSeasons.Episode? selectedEpisode = null;

                if (ret.TmdbId == null && ret.ImdbId != null)
                {
                    try
                    {
                        var response = await tmdbClient.Endpoints.Find.ByIdAsync(ret.ImdbId, TMDB.Models.Find.Externalsource.ImdbId, cancellationToken: cancellationToken).ConfigureAwait(false);
                        ret.TmdbId = response.Data!.TvEpisodeResults
                            .Where(_ => _.Name.ToComparable() == tvdbCompTitle)
                            .FirstOrDefault()?.Id.ToNumericId();

                        if (ret.TmdbId.HasValue)
                            selectedEpisode = new TMDB.Models.TvSeasons.Episode
                            {
                                Id = ret.TmdbId.Value,
                                ShowId = query.TmdbId.Value,
                                SeasonNumber = ret.Season,
                                EpisodeNumber = ret.Number
                            };
                    }
                    catch { }
                }

                if (selectedEpisode == null)
                {
                    var tmdbEpisodes = new List<TMDB.Models.TvSeasons.Episode>();

                    //Try orig season first
                    var seasonResponse = await tmdbClient.Endpoints.TvSeasons.GetDetailsAsync(ret.Season, query.TmdbId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
                    tmdbEpisodes.AddRange(seasonResponse.Data!.Episodes);

                    //Try by id if exists
                    if (ret.TmdbId.HasValue)
                        selectedEpisode = tmdbEpisodes
                            .Where(_ => _.Id == ret.TmdbId.Value)
                            .FirstOrDefault();

                    //Try by most accurate decreasing to least
                    selectedEpisode ??= tmdbEpisodes
                        .Where(_ => _.SeasonNumber == ret.Season)
                        .Where(_ => _.EpisodeNumber == ret.Number)
                        .Where(_ => _.Name.ToComparable() == tvdbCompTitle)
                        .FirstOrDefault();

                    //Check all eps in same season
                    if (selectedEpisode == null)
                    {
                        foreach (var tmdbEp in tmdbEpisodes.Where(_ => _.SeasonNumber == ret.Season))
                        {
                            if (tmdbEp.Name.ToComparable() == tvdbCompTitle)
                            {
                                selectedEpisode = tmdbEp;
                                break;
                            }
                        }
                    }

                    //Get all other seasons
                    if (selectedEpisode == null)
                    {
                        var seriesResponse = await tmdbClient.Endpoints.TvSeries.GetDetailsAsync(query.TmdbId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);

                        foreach (var season in seriesResponse.Data!.Seasons.Where(_ => _.SeasonNumber != ret.Season))
                        {
                            seasonResponse = await tmdbClient.Endpoints.TvSeasons.GetDetailsAsync(season.SeasonNumber, query.TmdbId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
                            tmdbEpisodes.AddRange(seasonResponse.Data!.Episodes);

                            foreach (var tmdbEp in tmdbEpisodes)
                            {
                                if (tmdbEp.Name.ToComparable() == tvdbCompTitle)
                                {
                                    selectedEpisode = tmdbEp;
                                    break;
                                }
                            }

                            if (selectedEpisode != null)
                                break;
                        }
                    }
                }


                // If found, update ret
                if (selectedEpisode != null)
                {
                    var episodeResponse = await tmdbClient.Endpoints.TvEpisodes.GetDetailsAsync(selectedEpisode.EpisodeNumber, selectedEpisode.SeasonNumber, selectedEpisode.ShowId, EpisodeAppend.Credits | EpisodeAppend.Images | EpisodeAppend.ExternalIds | EpisodeAppend.Translations, cancellationToken: cancellationToken).ConfigureAwait(false);

                    ret.Cast ??= episodeResponse.Data!.Credits.Cast.OrderBy(_ => _.Order).Select(_ => _.Name).ToList().ToNonEmpty();
                    ret.Directors = episodeResponse.Data!.Crew.Where(_ => _.Job.ICEquals("Director")).Select(_ => _.Name).ToList().ToNonEmpty();
                    ret.FirstAired ??= episodeResponse.Data.AirDate;
                    ret.ImdbId ??= episodeResponse.Data.ExternalIds?.ImdbId.ToNonEmpy();

                    ret.Overview ??= Coalesce(episodeResponse.Data.Translations.Translations.Where(_ => _.LanguageCode.ICEquals("en")).Select(_ => _.Data?.Overview).FirstOrDefault()?.ToNonEmpy(), episodeResponse.Data.Overview.ToNonEmpy());
                    ret.Title ??= Coalesce(episodeResponse.Data.Translations.Translations.Where(_ => _.LanguageCode.ICEquals("en")).Select(_ => _.Data?.Overview).FirstOrDefault()?.ToNonEmpy(), episodeResponse.Data.Overview.ToNonEmpy());
                    ret.TmdbId ??= episodeResponse.Data.Id.ToNumericId();
                    ret.Writers = episodeResponse.Data.Crew.Where(_ => _.Job.ICEquals("Writer")).Select(_ => _.Name).ToList().ToNonEmpty();

                    if (ret.ScreenshotUrl.IsNullOrWhiteSpace() && !episodeResponse.Data.StillPath.IsNullOrWhiteSpace())
                        ret.ScreenshotUrl = TMDB.Utils.GetFullSizeImageUrl(episodeResponse.Data.StillPath).ToNonEmpy();
                }
            }
        }
        catch { }


        // May be complete, only bother omdb if needed
        if (!ret.CompleteMetadata())
        {
            try
            {
                var omdbClient = _clientFactory.GetOMDbClient();
                omdbClient.AutoThrowIfError = true;

                //Ensure we have the episode imdb
                if (ret.ImdbId == null && query.ImdbId != null)
                {
                    try
                    {
                        //Get the orig season
                        var seasonResponse = await omdbClient.GetSeasonAsync(query.ImdbId, ret.Season, cancellationToken).ConfigureAwait(false);
                        seasonResponse.Data!.Episodes ??= [];

                        //Try to find the matching episode
                        OMDb.Models.SeasonEpisodeItem? selectedItem = null;
                        foreach (var ep in seasonResponse.Data.Episodes)
                        {
                            if (int.TryParse(ep.Episode, out int epNum))
                                if (epNum == ret.Number)
                                    if (ep.Title.ToComparable() == tvdbCompTitle)
                                    {
                                        selectedItem = ep;
                                        break;
                                    }
                        }

                        if (selectedItem == null)
                            foreach (var ep in seasonResponse.Data.Episodes)
                            {
                                if (ep.Title.ToComparable() == tvdbCompTitle)
                                {
                                    selectedItem = ep;
                                    break;
                                }
                            }

                        //Get all the seasons
                        if (selectedItem == null)
                        {
                            var seriesResponse = await omdbClient.GetSeriesByIdAsync(query.ImdbId, false, cancellationToken).ConfigureAwait(false);
                            if (int.TryParse(seriesResponse.Data!.TotalSeasons, out int totalSeasons))
                            {
                                for (int imdbSeason = 0; imdbSeason < totalSeasons; imdbSeason++)
                                {
                                    if (imdbSeason != ret.Season)
                                    {
                                        try
                                        {
                                            seasonResponse = await omdbClient.GetSeasonAsync(query.ImdbId, imdbSeason, cancellationToken).ConfigureAwait(false);
                                            foreach (var ep in seasonResponse.Data!.Episodes ?? [])
                                            {
                                                if (int.TryParse(ep.Episode, out int epNum))
                                                    if (epNum == ret.Number)
                                                        if (ep.Title.ToComparable() == tvdbCompTitle)
                                                        {
                                                            selectedItem = ep;
                                                            break;
                                                        }
                                            }

                                            if (selectedItem == null)
                                                foreach (var ep in seasonResponse.Data.Episodes ?? [])
                                                {
                                                    if (ep.Title.ToComparable() == tvdbCompTitle)
                                                    {
                                                        selectedItem = ep;
                                                        break;
                                                    }
                                                }
                                        }
                                        catch { }
                                    }
                                    if (selectedItem != null)
                                        break;
                                }
                            }
                        }

                        ret.ImdbId = selectedItem?.ImdbId.ToNonEmpy();

                    }
                    catch { }
                }

                // Try to get the info
                if (ret.ImdbId != null)
                {
                    var imdbResponse = await omdbClient.GetEpisodeByIdAsync(ret.ImdbId, true, cancellationToken).ConfigureAwait(false);
                    ret.Cast ??= imdbResponse.Data!.Actors.SplitOmdbString();
                    ret.Directors ??= imdbResponse.Data!.Director.SplitOmdbString();

                    if (ret.FirstAired == null && DateOnly.TryParse(imdbResponse.Data!.Released, out DateOnly dt))
                        ret.FirstAired = dt;

                    ret.Overview ??= imdbResponse.Data!.Plot.ToNonEmpy();
                    ret.Title ??= imdbResponse.Data!.Title.ToNonEmpy();
                    ret.Writers ??= imdbResponse.Data!.Writer.SplitOmdbString();
                }
            }
            catch { }
        }


        return ret;
    }

    private static async Task<Episode> GetEpisode(TVDB.Client tvdbClient, int episodeTvdbId, CancellationToken cancellationToken)
    {
        const int TVDB_IMAGE_TYPE_EPISODE_SCREENSHOP_16_9 = 11;
        const int TVDB_IMAGE_TYPE_EPISODE_SCREENSHOT_4_3 = 12;

        var response = await tvdbClient.Episodes.GetExtendedAsync(episodeTvdbId, true, cancellationToken).ConfigureAwait(false);
        response.ThrowIfError();

        response.Data.Characters ??= [];
        var ids = response.Data.RemoteIds.Process();

        var ret = new Episode
        {
            Cast = (response.Data.Characters ?? []).Where(_ => _.IsFeatured).Select(_ => _.Name).ToList().ToNonEmpty(),
            Number = response.Data.Number ?? -1,
            ImdbId = ids.ImdbId,
            Overview = Coalesce(response.Data.Translations.OverviewTranslations.Where(_ => _.Language.ICEquals("eng")).FirstOrDefault()?.Overview.ToNonEmpy(), response.Data.Overview.ToNonEmpy()),
            Season = response.Data.SeasonNumber ?? -1,
            Title = Coalesce(response.Data.Translations.NameTranslations.Where(_ => _.Language.ICEquals("eng")).FirstOrDefault()?.Name.ToNonEmpy(), response.Data.Name.ToNonEmpy()),
            TmdbId = ids.TmdbId,
            TvdbId = response.Data.Id
        };

        if (DateOnly.TryParse(response.Data.Aired, out DateOnly dt))
            ret.FirstAired = dt;

        if (response.Data.ImageType == TVDB_IMAGE_TYPE_EPISODE_SCREENSHOP_16_9 || response.Data.ImageType == TVDB_IMAGE_TYPE_EPISODE_SCREENSHOT_4_3)
            if (!response.Data.Image.IsNullOrWhiteSpace())
                ret.ScreenshotUrl = response.Data.Image.ToNonEmpy();

        return ret;
    }

    /// <summary>
    /// This will try to get ALL episodes for the query. Warning: This can be slow!
    /// </summary>
    public async Task<List<Episode>> GetAllEpisodes(Query query, CancellationToken cancellationToken = default)
    {
        var tvdbClient = await _clientFactory.GetTVDBClient(cancellationToken).ConfigureAwait(false);
        List<int> episodeIds = [];
        int page = 0;
        while (true)
        {
            var response = await tvdbClient.Series.GetEpisodesAsync(query.TvdbId!.Value, SeasonTypes.Default, page++, cancellationToken: cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();
            if (response.Data == null || response.Data.Episodes == null || response.Data.Episodes.Count == 0)
                break;
            episodeIds.AddRange(response.Data.Episodes.Select(_ => _.Id));
        }

        var ret = new List<Episode>();

        foreach(var id in episodeIds)
        {
            var episode = await GetEpisode(query, id, cancellationToken).ConfigureAwait(false);
            ret.Add(episode);
        }

        
        return ret;
    }


    #endregion





    #region Helpers

    private static string? Coalesce(params string?[] items)
    {
        foreach (string? item in items)
            if (!item.IsNullOrWhiteSpace())
                return item;
        return null;
    }

    private static List<T>? Coalesce<T>(params List<T>?[] items)
    {
        foreach (var item in items)
            if (item != null && item.Count > 0)
                return item;
        return null;
    }

    private static bool TryMapMovieRatings(string country, string rating, [NotNullWhen(true)] out string? rated)
    {
        rated = null;

        if (country.IsNullOrWhiteSpace() || rating.IsNullOrWhiteSpace())
            return false;

        rated = RatingsUtils.MapMovieRatings(country, rating);
        if (!rated.IsNullOrWhiteSpace())
            return true;

        rated = RatingsUtils.MapTVRatings(country, rating);
        if (!rated.IsNullOrWhiteSpace())
            return true;

        return false;
    }

    private static bool TryMapTVRatings(string country, string rating, [NotNullWhen(true)] out string? rated)
    {
        rated = null;

        if (country.IsNullOrWhiteSpace() || rating.IsNullOrWhiteSpace())
            return false;

        rated = RatingsUtils.MapTVRatings(country, rating);
        if (!rated.IsNullOrWhiteSpace())
            return true;

        rated = RatingsUtils.MapMovieRatings(country, rating);
        if (!rated.IsNullOrWhiteSpace())
            return true;

        return false;
    }

    #endregion
}
