namespace Opsd;

/// <summary>Configuration for <see cref="OpsdClient"/>.</summary>
public sealed class OpsdClientOptions
{
    /// <summary>The production Opsd API base address.</summary>
    public static Uri ProductionBaseAddress { get; } = new("https://api.opsd.sh/v1/");

    /// <summary>The API base address.</summary>
    public Uri BaseAddress { get; init; } = ProductionBaseAddress;

    /// <summary>The request timeout.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// An optional HTTP handler, primarily for custom transports and testing.
    /// The client takes ownership of the handler.
    /// </summary>
    public HttpMessageHandler? HttpMessageHandler { get; init; }
}

