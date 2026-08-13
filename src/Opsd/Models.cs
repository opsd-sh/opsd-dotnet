namespace Opsd;

/// <summary>A response from an Opsd hello endpoint.</summary>
/// <param name="Message">The response message.</param>
public sealed record HelloResponse(string Message);

/// <summary>An Opsd test user.</summary>
/// <param name="Id">The user's numeric identifier.</param>
/// <param name="Name">The user's display name.</param>
/// <param name="Email">The user's email address.</param>
public sealed record User(int Id, string Name, string Email);

/// <summary>The fields used to create an Opsd test user.</summary>
/// <param name="Name">The user's display name.</param>
/// <param name="Email">The user's email address.</param>
public sealed record CreateUserRequest(string Name, string Email);

/// <summary>Structured details returned for an Opsd API error.</summary>
/// <param name="ProblemType">A URI identifying the problem type.</param>
/// <param name="Title">A short, human-readable summary.</param>
/// <param name="Status">The HTTP status associated with the problem.</param>
/// <param name="Detail">A human-readable explanation.</param>
/// <param name="Category">The Opsd problem category.</param>
public sealed record ProblemDetails(
    string ProblemType,
    string Title,
    int Status,
    string Detail,
    string Category)
{
    /// <inheritdoc />
    public override string ToString() => Detail;
}

