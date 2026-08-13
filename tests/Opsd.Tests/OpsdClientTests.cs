using System.Net;
using System.Text;
using System.Text.Json;

namespace Opsd.Tests;

public sealed class OpsdClientTests
{
    [Fact]
    public void ClientDefaultsToProductionAndRedactsCredential()
    {
        using OpsdClient client = new(new ApiCredential("secret-access-token"));

        Assert.Equal(new Uri("https://api.opsd.sh/v1/"), client.BaseAddress);
        Assert.DoesNotContain("secret-access-token", client.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ClientNormalizesAnExistingBasePath()
    {
        using OpsdClient client = CreateClient(
            _ => JsonResponse(new { message = "hello" }),
            new Uri("https://example.test/v1"));

        Assert.Equal(new Uri("https://example.test/v1/"), client.BaseAddress);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://example.test/v1")]
    [InlineData("https://example.test/v1?tenant=one")]
    public void InvalidBaseAddressesAreRejected(string baseAddress)
    {
        UriKind kind = baseAddress.StartsWith("relative", StringComparison.Ordinal)
            ? UriKind.Relative
            : UriKind.Absolute;
        OpsdClientOptions options = new()
        {
            BaseAddress = new Uri(baseAddress, kind),
        };

        Assert.Throws<InvalidBaseUrlException>(() =>
            new OpsdClient(new ApiCredential("secret"), options));
    }

    [Fact]
    public async Task HelloWorldAuthenticatesAndDecodesResponse()
    {
        using OpsdClient client = CreateClient(request =>
        {
            Assert.Equal("https://example.test/v1/hello/world", request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("secret", request.Headers.Authorization?.Parameter);
            Assert.Equal(
                "application/json, application/problem+json",
                string.Join(", ", request.Headers.Accept.Select(value => value.MediaType)));
            return JsonResponse(new { message = "hello" });
        });

        HelloResponse response = await client.HelloWorldAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new HelloResponse("hello"), response);
    }

    [Fact]
    public async Task MethodsUseExpectedPathsAndModels()
    {
        List<(HttpMethod Method, string Path)> requests = [];
        string? requestBody = null;
        using OpsdClient client = CreateClient(request =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            if (request.RequestUri.AbsolutePath.EndsWith("/hello/application", StringComparison.Ordinal))
            {
                return JsonResponse(new { message = "application" });
            }

            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(new[] { new { id = 1, name = "Ada", email = "ada@example.test" } });
            }

            requestBody = request.Content!
                .ReadAsStringAsync(TestContext.Current.CancellationToken)
                .GetAwaiter()
                .GetResult();
            return JsonResponse(
                new { id = 2, name = "Grace", email = "grace@example.test" },
                HttpStatusCode.Created);
        });

        Assert.Equal(
            new HelloResponse("application"),
            await client.HelloApplicationAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            [new User(1, "Ada", "ada@example.test")],
            await client.ListUsersAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            new User(2, "Grace", "grace@example.test"),
            await client.CreateUserAsync(
                new CreateUserRequest("Grace", "grace@example.test"),
                TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                (HttpMethod.Get, "/v1/hello/application"),
                (HttpMethod.Get, "/v1/test/users"),
                (HttpMethod.Post, "/v1/test/users"),
            ],
            requests);
        Assert.Equal(
            "{\"name\":\"Grace\",\"email\":\"grace@example.test\"}",
            requestBody);
    }

    [Fact]
    public async Task ApiErrorsExposeProblemDetails()
    {
        using OpsdClient client = CreateClient(_ => JsonResponse(
            new
            {
                type = "https://api.opsd.sh/problems/not-found",
                title = "Not Found",
                status = 404,
                detail = "no route found",
                category = "request",
            },
            HttpStatusCode.NotFound));

        ApiException exception = await Assert.ThrowsAsync<ApiException>(() =>
            client.HelloWorldAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal("https://api.opsd.sh/problems/not-found", exception.Problem.ProblemType);
        Assert.Equal("no route found", exception.Problem.Detail);
    }

    [Fact]
    public async Task UnrecognizedResponsesArePreserved()
    {
        using OpsdClient client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("bad gateway", Encoding.UTF8, "text/plain"),
        });

        UnexpectedResponseException exception =
            await Assert.ThrowsAsync<UnexpectedResponseException>(() =>
                client.HelloWorldAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Equal("bad gateway", exception.Body);
    }

    [Fact]
    public async Task TransportFailuresAreWrapped()
    {
        using OpsdClient client = CreateClient(_ => throw new HttpRequestException("connection refused"));

        TransportException exception =
            await Assert.ThrowsAsync<TransportException>(() =>
                client.HelloWorldAsync(TestContext.Current.CancellationToken));

        Assert.Contains("connection refused", exception.Message, StringComparison.Ordinal);
    }

    private static OpsdClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> response,
        Uri? baseAddress = null) =>
        new(
            new ApiCredential("secret"),
            new OpsdClientOptions
            {
                BaseAddress = baseAddress ?? new Uri("https://example.test/v1/"),
                HttpMessageHandler = new StubHttpMessageHandler(response),
            });

    private static HttpResponseMessage JsonResponse(object value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json"),
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }
}
