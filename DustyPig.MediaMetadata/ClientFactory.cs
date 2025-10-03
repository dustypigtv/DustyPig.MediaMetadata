using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DustyPig.MediaMetadata;

internal class ClientFactory(Configuration configuration, HttpClient? httpClient = null)
{
    // Single internal HttpClient shared by all api cients
    private static readonly HttpClient _sharedClient = new();


    /// <summary>
    /// Returns a configured <see cref="TMDB.Client"/> client.
    /// </summary>
    public TMDB.Client GetTMDBClient()
    {
        ThrowIfAnyUnset("TMDB API key must be set in Configuration", configuration.TMDBApiKey);
        var ret = new TMDB.Client(httpClient ?? _sharedClient)
        {
            IncludeRawContentInResponse = configuration.ApiClientsIncludeRawContentResponse,
            AutoThrowIfError = configuration.ApiClientsAutoThrowOnError
        };
        ret.SetAuth(TMDB.Client.AuthTypes.APIKey, configuration.TMDBApiKey);
        return ret;
    }


    /// <summary>
    /// Returns an authenticated TVDB client. If the <see cref="Configuration"/> token is not set or is expired, 
    /// the client will authenticate and update the token in the <see cref="Configuration"/>
    /// </summary>
    public async Task<TVDB.Client> GetTVDBClient(CancellationToken cancellationToken = default)
    {
        ThrowIfAnyUnset("TVDB API key and pin must be set in Configuration", configuration.TVDBApiKey, configuration.TVDBApiPin);

        TVDB.Client ret = new(httpClient ?? _sharedClient)
        {
            IncludeRawContentInResponse = configuration.ApiClientsIncludeRawContentResponse,
            AutoThrowIfError = configuration.ApiClientsAutoThrowOnError
        };

        ret.Login.SetAuthToken(configuration.TVDBToken);

        if (string.IsNullOrWhiteSpace(configuration.TVDBToken) || DateTime.Now > configuration.TVDBTokenExpiresUTC)
        {
            var credentials = new TVDB.Models.Credentials
            {
                Apikey = configuration.TVDBApiKey,
                Pin = configuration.TVDBApiPin
            };
            var response = await ret.Login.LoginAsync(credentials, cancellationToken).ConfigureAwait(false);
            response.ThrowIfError();

            //Documentation says token is good for 1 month. 30 days? I know from practice that 29 days always works fine.
            configuration.SetTVDBToken(response.Data.Token, DateTime.UtcNow.AddDays(29));
        }

        return ret;
    }


    /// <summary>
    /// Returns a configured <see cref="OMDb.Client"/>
    /// </summary>
    public OMDb.Client GetOMDbClient()
    {
        ThrowIfAnyUnset("OMDb API key must be set in Configuration", configuration.OmdbApiKey);
        var ret = new OMDb.Client(httpClient ?? _sharedClient)
        {
            IncludeRawContentInResponse = configuration.ApiClientsIncludeRawContentResponse,
            ApiKey = configuration.OmdbApiKey,
            AutoThrowIfError = configuration.ApiClientsAutoThrowOnError
        };
        return ret;
    }


    private static void ThrowIfAnyUnset(string msg, params string?[] values)
    {
        foreach (var v in values)
            if (v.IsNullOrWhiteSpace())
                throw new InvalidOperationException(msg);
    }
}
