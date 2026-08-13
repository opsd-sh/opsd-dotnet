using System.Net.Http.Headers;

namespace Opsd;

/// <summary>A secret used to authenticate requests to the public Opsd API.</summary>
public sealed class ApiCredential
{
    private readonly AuthenticationHeaderValue _authorization;

    /// <summary>Creates a validated bearer credential.</summary>
    /// <param name="secret">An OAuth access token or API key.</param>
    /// <exception cref="InvalidApiCredentialException">
    /// Thrown when <paramref name="secret"/> cannot be used in an HTTP header.
    /// </exception>
    public ApiCredential(string secret)
    {
        if (string.IsNullOrEmpty(secret) || !IsVisibleAscii(secret))
        {
            throw new InvalidApiCredentialException();
        }

        _authorization = new AuthenticationHeaderValue("Bearer", secret);
    }

    internal void Apply(HttpRequestHeaders headers)
    {
        headers.Authorization = _authorization;
    }

    /// <inheritdoc />
    public override string ToString() => "ApiCredential([REDACTED])";

    private static bool IsVisibleAscii(string value)
    {
        foreach (char character in value)
        {
            if (character is < '\u0021' or > '\u007e')
            {
                return false;
            }
        }

        return true;
    }
}

