using System;

namespace DustyPig.MediaMetadata;

public class Configuration
{
    public event EventHandler? TVDBTokenChanged;

    public bool ApiClientsAutoThrowOnError { get; set; } = true;
    public bool ApiClientsIncludeRawContentResponse { get; set; }
    public string? TMDBApiKey { get; set; }
    public string? OmdbApiKey { get; set; }


    public string? TVDBApiKey { get; set; }
    public string? TVDBApiPin { get; set; }

    /// <summary>
    /// This value will be updated by the <see cref="MetaClient"/>, and <see cref="TVDBTokenChanged"/> will be raised
    /// </summary>
    public string? TVDBToken { get; set; }

    /// <summary>
    /// This value will be updated by the <see cref="MetaClient"/>, and <see cref="TVDBTokenChanged"/> will be raised
    /// </summary>
    public DateTime TVDBTokenExpiresUTC { get; set; }


    internal void SetTVDBToken(string token, DateTime expires)
    {
        if (TVDBToken != token || TVDBTokenExpiresUTC != expires)
        {
            TVDBToken = token;
            TVDBTokenExpiresUTC = expires;
            TVDBTokenChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}