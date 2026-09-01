using System.Collections.Generic;

namespace DustyPig.MediaMetadata;

public class SeriesSearchResults
{
    public List<TVDB.Models.SearchResult>? TvdbResults { get; set; }

    public List<TMDB.Models.Common.TvSeries>? TmdbResults { get; set; }

    public List<OMDb.Models.SearchResultItem>? ImdbResults { get; set; }
}
