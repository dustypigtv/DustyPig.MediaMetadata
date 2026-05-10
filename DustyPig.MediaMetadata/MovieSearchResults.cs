using System.Collections.Generic;

namespace DustyPig.MediaMetadata;

public class MovieSearchResults
{
    public List<TMDB.Models.Common.CommonMovie>? TmdbResults { get; set; }

    public List<TVDB.Models.SearchResult>? TvdbResults { get; set; }

    public List<OMDb.Models.SearchResultItem>? ImdbResults { get; set; }
}
