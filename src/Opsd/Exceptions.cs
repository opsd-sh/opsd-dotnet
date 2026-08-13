using System.Net;

namespace Opsd;

/// <summary>Base class for errors raised by the Opsd client.</summary>
public class OpsdException : Exception
{
    /// <summary>Creates an Opsd exception.</summary>
    protected OpsdException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an Opsd exception with an underlying cause.</summary>
    protected OpsdException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when an API credential is empty or malformed.</summary>
public sealed class InvalidApiCredentialException : OpsdException
{
    /// <summary>Creates the exception.</summary>
    public InvalidApiCredentialException()
        : base("invalid API credential")
    {
    }
}

/// <summary>Raised when a client base URL is not valid.</summary>
public sealed class InvalidBaseUrlException : OpsdException
{
    /// <summary>Creates the exception.</summary>
    public InvalidBaseUrlException(string baseUrl, string message)
        : base($"invalid base URL `{baseUrl}`: {message}")
    {
        BaseUrl = baseUrl;
    }

    /// <summary>The rejected base URL.</summary>
    public string BaseUrl { get; }
}

/// <summary>Raised when an HTTP request fails before a response is received.</summary>
public sealed class TransportException : OpsdException
{
    /// <summary>Creates the exception.</summary>
    public TransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Raised when the server returns structured problem details.</summary>
public sealed class ApiException : OpsdException
{
    /// <summary>Creates the exception.</summary>
    public ApiException(HttpStatusCode statusCode, ProblemDetails problem)
        : base($"API request failed: {problem}")
    {
        StatusCode = statusCode;
        Problem = problem;
    }

    /// <summary>The HTTP response status.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The structured API problem.</summary>
    public ProblemDetails Problem { get; }
}

/// <summary>Raised when an HTTP response cannot be decoded as expected.</summary>
public sealed class UnexpectedResponseException : OpsdException
{
    /// <summary>Creates the exception.</summary>
    public UnexpectedResponseException(HttpStatusCode statusCode, string body)
        : base($"API request returned an unexpected response: {body}")
    {
        StatusCode = statusCode;
        Body = body;
    }

    /// <summary>Creates the exception with the decoding failure.</summary>
    public UnexpectedResponseException(HttpStatusCode statusCode, string body, Exception innerException)
        : base($"API request returned an unexpected response: {body}", innerException)
    {
        StatusCode = statusCode;
        Body = body;
    }

    /// <summary>The HTTP response status.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The unrecognized response body.</summary>
    public string Body { get; }
}
