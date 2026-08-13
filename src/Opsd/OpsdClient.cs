using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Opsd;

/// <summary>An asynchronous client for the public Opsd API.</summary>
public sealed class OpsdClient : IDisposable
{
    private const string Accept = "application/json, application/problem+json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    /// <summary>Creates a client for the production Opsd API.</summary>
    public OpsdClient(ApiCredential credential)
        : this(credential, new OpsdClientOptions())
    {
    }

    /// <summary>Creates a client with explicit options.</summary>
    public OpsdClient(ApiCredential credential, OpsdClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(options);

        BaseAddress = NormalizeBaseAddress(options.BaseAddress);
        _httpClient = options.HttpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(options.HttpMessageHandler, disposeHandler: true);
        _httpClient.Timeout = options.Timeout;
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd(Accept);
        credential.Apply(_httpClient.DefaultRequestHeaders);
    }

    /// <summary>The normalized API base address.</summary>
    public Uri BaseAddress { get; }

    /// <summary>Calls the unauthenticated hello-world sandbox route.</summary>
    public async Task<HelloResponse> HelloWorldAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "hello/world", null, cancellationToken)
            .ConfigureAwait(false);
        return await DecodeAsync(response, DecodeHelloResponse, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Calls the authenticated application hello route.</summary>
    public async Task<HelloResponse> HelloApplicationAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            "hello/application",
            null,
            cancellationToken).ConfigureAwait(false);
        return await DecodeAsync(response, DecodeHelloResponse, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists test users.</summary>
    public async Task<IReadOnlyList<User>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "test/users", null, cancellationToken)
            .ConfigureAwait(false);
        return await DecodeAsync(response, DecodeUsers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a test user.</summary>
    public async Task<User> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using JsonContent content = JsonContent.Create(request, options: JsonOptions);
        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Post,
            "test/users",
            content,
            cancellationToken).ConfigureAwait(false);
        return await DecodeAsync(response, DecodeUser, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _httpClient.Dispose();

    /// <inheritdoc />
    public override string ToString() =>
        $"OpsdClient(BaseAddress={BaseAddress}, Credential=[REDACTED])";

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(method, new Uri(BaseAddress, path))
        {
            Content = content,
        };

        try
        {
            return await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TransportException("request failed: the request timed out", new TimeoutException());
        }
        catch (HttpRequestException exception)
        {
            throw new TransportException($"request failed: {exception.Message}", exception);
        }
    }

    private static async Task<T> DecodeAsync<T>(
        HttpResponseMessage response,
        Func<JsonElement, T> decoder,
        CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            throw new UnexpectedResponseException(response.StatusCode, body, exception);
        }

        using (document)
        {
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    return decoder(document.RootElement);
                }
                catch (Exception exception) when (exception is JsonException or InvalidOperationException)
                {
                    throw new UnexpectedResponseException(response.StatusCode, body, exception);
                }
            }

            try
            {
                throw new ApiException(response.StatusCode, DecodeProblemDetails(document.RootElement));
            }
            catch (ApiException)
            {
                throw;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                throw new UnexpectedResponseException(response.StatusCode, body, exception);
            }
        }
    }

    private static HelloResponse DecodeHelloResponse(JsonElement value) =>
        new(GetRequiredString(value, "message"));

    private static IReadOnlyList<User> DecodeUsers(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.Array)
        {
            throw new JsonException("expected a JSON array");
        }

        return value.EnumerateArray().Select(DecodeUser).ToArray();
    }

    private static User DecodeUser(JsonElement value) =>
        new(
            GetRequiredInt32(value, "id"),
            GetRequiredString(value, "name"),
            GetRequiredString(value, "email"));

    private static ProblemDetails DecodeProblemDetails(JsonElement value) =>
        new(
            GetRequiredString(value, "type"),
            GetRequiredString(value, "title"),
            GetRequiredInt32(value, "status"),
            GetRequiredString(value, "detail"),
            GetRequiredString(value, "category"));

    private static string GetRequiredString(JsonElement value, string propertyName)
    {
        if (value.ValueKind is not JsonValueKind.Object ||
            !value.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind is not JsonValueKind.String)
        {
            throw new JsonException($"expected `{propertyName}` to be a string");
        }

        return property.GetString()!;
    }

    private static int GetRequiredInt32(JsonElement value, string propertyName)
    {
        if (value.ValueKind is not JsonValueKind.Object ||
            !value.TryGetProperty(propertyName, out JsonElement property) ||
            !property.TryGetInt32(out int result))
        {
            throw new JsonException($"expected `{propertyName}` to be an integer");
        }

        return result;
    }

    private static Uri NormalizeBaseAddress(Uri baseAddress)
    {
        if (baseAddress is null ||
            !baseAddress.IsAbsoluteUri ||
            (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidBaseUrlException(
                baseAddress?.ToString() ?? string.Empty,
                "expected an absolute HTTP or HTTPS URL");
        }

        if (!string.IsNullOrEmpty(baseAddress.Query) || !string.IsNullOrEmpty(baseAddress.Fragment))
        {
            throw new InvalidBaseUrlException(
                baseAddress.ToString(),
                "query strings and fragments are not allowed");
        }

        UriBuilder builder = new(baseAddress);
        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += "/";
        }

        return builder.Uri;
    }
}
