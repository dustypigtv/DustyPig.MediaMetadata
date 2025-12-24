using DustyPig.API.v3.MPAA;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DustyPig.MediaMetadata;

public class IMDBScraper
{
    //Exist in IMDB and mapped in API
    private static readonly Dictionary<string, string> _countryCodes = new()
    {
        { "argentina", "AR" },
        { "australia", "AU" },
        { "brazil", "BR" },
        { "canada", "CA" },
        { "denmark", "DK" },
        { "france", "FR" },
        { "germany", "DE" },
        { "india", "IN" },
        { "italy", "IT" },
        { "south korea", "KR" },
        { "malaysia", "MY" },
        { "netherlands", "NL" },
        { "new zealand", "NZ" },
        { "norway", "NO" },
        { "philippines", "PH" },
        { "portugal", "PT" },
        { "singapore", "SG" },
        { "spain", "ES" },
        { "sweden", "SE" },
        { "thailand", "TH" },
        { "united kingdom", "GB" },
        { "united states", "US" },
    };

    
    public static async Task<MovieRatings> TryGetMovieRating(string movieImdbId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://www.imdb.com/title/{movieImdbId}/parentalguide";
            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(url, cancellationToken).ConfigureAwait(false);

            var ratingLIs = doc
                .DocumentNode
                .Descendants("li")
                .Where(item => item.Attributes.Any(attr => attr.Value == "certificates-item"))
                .OrderBy(item => !item.ChildNodes.First().InnerText.ICEquals("United States"));


            foreach (var li in ratingLIs)
            {
                var qSort = li
                    .ChildNodes[1]
                    .Descendants("a")
                    .OrderBy(item => item.InnerText.ToMovieRatings() == MovieRatings.None)
                    .ThenBy(item => item.InnerText.ToTVRatings() == TVRatings.None);

                foreach (var anchor in qSort)
                {
                    var mr = anchor.InnerText.ToMovieRatings();
                    if (mr != MovieRatings.None && mr != MovieRatings.Unrated)
                        return mr;

                    var tvr = anchor.InnerText.ToTVRatings();
                    if (tvr != TVRatings.None && tvr != TVRatings.NotRated)
                        return tvr.ToMovieRatings();
                }
            }



            foreach (var li in ratingLIs)
            {
                var country = li.ChildNodes.First().InnerText.ToLower();

                var qSort = li
                    .ChildNodes[1]
                    .Descendants("a");

                foreach (var anchor in qSort)
                {
                    if (_countryCodes.TryGetValue(country, out string? cc))
                    {
                        var mr = (RatingsUtils.MapMovieRatings(cc, anchor.InnerText) + string.Empty).ToMovieRatings();
                        if (mr != MovieRatings.None && mr != MovieRatings.Unrated)
                            return mr;

                        var tvr = (RatingsUtils.MapTVRatings(cc, anchor.InnerText) + string.Empty).ToTVRatings();
                        if (tvr != TVRatings.None && tvr != TVRatings.NotRated)
                            return tvr.ToMovieRatings();
                    }
                }
            }
        }
        catch { }

        return MovieRatings.None;
    }




    public static async Task<TVRatings> TryGetTVRating(string seriesImdbId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://www.imdb.com/title/{seriesImdbId}/parentalguide";
            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(url, cancellationToken).ConfigureAwait(false);

            var ratingLIs = doc
                .DocumentNode
                .Descendants("li")
                .Where(item => item.Attributes.Any(attr => attr.Value == "certificates-item"))
                .OrderBy(item => !item.ChildNodes.First().InnerText.ICEquals("United States"));


            foreach (var li in ratingLIs)
            {
                var qSort = li
                    .ChildNodes[1]
                    .Descendants("a")
                    .OrderBy(item => item.InnerText.ToMovieRatings() == MovieRatings.None)
                    .ThenBy(item => item.InnerText.ToTVRatings() == TVRatings.None);

                foreach (var anchor in qSort)
                {
                    var tvr = anchor.InnerText.ToTVRatings();
                    if (tvr != TVRatings.None && tvr != TVRatings.NotRated)
                        return tvr;

                    var mr = anchor.InnerText.ToMovieRatings();
                    if (mr != MovieRatings.None && mr != MovieRatings.Unrated)
                        return mr.ToTVRatings();
                }
            }



            foreach (var li in ratingLIs)
            {
                var country = li.ChildNodes.First().InnerText.ToLower();

                var qSort = li
                    .ChildNodes[1]
                    .Descendants("a");

                foreach (var anchor in qSort)
                {
                    if (_countryCodes.TryGetValue(country, out string? cc))
                    {
                        var tvr = (RatingsUtils.MapTVRatings(cc, anchor.InnerText) + string.Empty).ToTVRatings();
                        if (tvr != TVRatings.None && tvr != TVRatings.NotRated)
                            return tvr;

                        var mr = (RatingsUtils.MapMovieRatings(cc, anchor.InnerText) + string.Empty).ToMovieRatings();
                        if (mr != MovieRatings.None && mr != MovieRatings.Unrated)
                            return mr.ToTVRatings();
                    }
                }
            }
        }
        catch { }

        return TVRatings.None;
    }


    public static async Task<Episode> TryGetEpisodeInfo(string episodeImdbId, CancellationToken cancellationToken = default)
    {
        var ret = new Episode { ImdbId = episodeImdbId };

        try
        {
            var url = $"https://www.imdb.com/title/{episodeImdbId}/";
            var web = new HtmlWeb();
            var doc = await web.LoadFromWebAsync(url, cancellationToken).ConfigureAwait(false);

            //Title
            try
            {
                ret.Title = doc
                    .DocumentNode
                    .Descendants("span")
                    .Where(_ => _.HasClass("hero__primary-text"))
                    .First()
                    .InnerText;
            }
            catch { }

            //Date
            try
            {
                var dateStr = doc
                    .DocumentNode
                    .Descendants()
                    .Where(item => item.InnerText.StartsWith("Episode aired"))
                    .First()
                    .InnerText[14..];

                if (DateOnly.TryParse(dateStr, out DateOnly dt))
                    ret.FirstAired = dt;
            }
            catch { }


            //Plot
            try
            {
                ret.Overview = doc
                    .DocumentNode
                    .Descendants("p")
                    .Where(item => item.Attributes.Any(attr => attr.Name == "data-testid" && attr.Value == "plot"))
                    .First()
                    .InnerText;
            }
            catch { }


            //Directors
            try
            {
                ret.Directors = doc
                    .DocumentNode
                    .Descendants("div")
                    .Where(item => item.HasClass("sc-af040695-2"))
                    .First()
                    .Descendants("li")
                    .Where(item => item.FirstChild.InnerText == "Director")
                    .First()
                    .Descendants("a")
                    .Where(item => item.Attributes["href"].Value.StartsWith("/name/nm"))
                    .Select(item => item.InnerText)
                    .ToList();
            }
            catch { }

            //Writers
            try
            {
                ret.Writers = doc
                     .DocumentNode
                     .Descendants("div")
                     .Where(item => item.HasClass("sc-af040695-2"))
                     .First()
                     .Descendants("li")
                     .Where(item => item.FirstChild.InnerText == "Writers")
                     .First()
                     .Descendants("a")
                     .Where(item => item.Attributes["href"].Value.StartsWith("/name/nm"))
                     .Select(item => item.InnerText)
                     .ToList();
            }
            catch { }

            //Cast
            try
            {
                ret.Cast = doc
                    .DocumentNode
                    .Descendants("div")
                    .Where(item => item.HasClass("sc-af040695-2"))
                    .First()
                    .Descendants("li")
                    .Where(item => item.FirstChild.InnerText == "Stars")
                    .First()
                    .Descendants("a")
                    .Where(item => item.Attributes["href"].Value.StartsWith("/name/nm"))
                    .Select(item => item.InnerText)
                    .ToList();
            }
            catch { }

        }
        catch { }

        return ret;
    }

}
