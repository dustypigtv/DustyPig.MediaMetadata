using System.Collections.Generic;

namespace DustyPig.MediaMetadata;

public class SeriesSearchResults
{
    public List<TVDB.Models.SearchResult>? TvdbResults { get; set; }

    public List<TMDB.Models.Common.CommonTvSeries1>? TmdbResults { get; set; }

    public List<OMDb.Models.SearchResultItem>? ImdbResults { get; set; }
}
