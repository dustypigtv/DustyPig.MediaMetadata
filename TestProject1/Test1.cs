using DustyPig.MediaMetadata;
using System.Diagnostics;

namespace TestProject1;

[TestClass]
public sealed class Test1
{
    MetaClient _metaClient = new MetaClient(new Configuration());

    [TestMethod]
    public async Task GetMovieMetadata()
    {
        var query = new Query { Title = "The Avengers", Year = 2012 };
        var response = await _metaClient.GetMovieMetadata(query);
        Assert.AreEqual(24428, response.TmdbId);
        Assert.AreEqual(31, response.TvdbId);
        Assert.AreEqual("tt0848228", response.ImdbId);
    }


    [TestMethod]
    public async Task GetSeriesMetadata()
    {
        var query = new Query { Title = "Buffy the Vampire Slayer" };
        var response = await _metaClient.GetSeriesMetadata(query);
        Assert.AreEqual(70327, response.TvdbId);
        Assert.AreEqual(95, response.TmdbId);
        Assert.AreEqual("tt0118276", response.ImdbId);
    }

    
    [TestMethod]
    public async Task GetAllEpisodes()
    {
        var query = new Query
        {
            Title = "Buffy the Vampire Slayer",
            ImdbId = "tt0118276",
            TmdbId = 95,
            TvdbId = 70327
        };

        var response = await _metaClient.GetAllEpisodes(query);

        var nonSpecials = response.Where(_ => _.Season > 0);
        Assert.AreEqual(144, nonSpecials.Count());

        var firstEp = nonSpecials.Where(_ => _.Season == 1).Where(_ => _.Number == 1).First();
        Assert.AreEqual(2, firstEp.TvdbId);
    }


    [TestMethod]
    public async Task GetEpisodeMetadata()
    {
        var query = new Query
        {
            Title = "Buffy the Vampire Slayer",
            ImdbId = "tt0118276",
            TmdbId = 95,
            TvdbId = 70327
        };

        var response = await _metaClient.GetEpisodeMetadata(query, 2);

        Assert.AreEqual(2, response.TvdbId);
        Assert.AreEqual(949413, response.TmdbId);
        Assert.AreEqual("tt0452716", response.ImdbId);
    }








}
