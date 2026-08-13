# Opsd .NET library

`Opsd` is the .NET client library for the Opsd API. It provides a small,
typed wrapper around the current public endpoints, including the hello-world
sandbox route and the user endpoints.

## Installation

```console
dotnet add package Opsd
```

The package targets .NET 8 and later and has no runtime dependencies.

## Usage

The default client targets the production API at `https://api.opsd.sh/v1/`.
OAuth access tokens and API keys both use the HTTP Bearer scheme and are
redacted from object representations.

```csharp
using Opsd;

ApiCredential credential = new("secret");
using OpsdClient client = new(credential);
HelloResponse response = await client.HelloWorldAsync();

Console.WriteLine(response.Message);
```

For local development, tests, or non-production deployments, supply an
`OpsdClientOptions` instance with a different `BaseAddress`. Successful
responses are returned as typed records. Non-success responses throw
`ApiException` when the server returns problem details, or
`UnexpectedResponseException` for an unrecognized response.

## Development

```console
dotnet restore
dotnet format --verify-no-changes --no-restore
dotnet run --project tests/Opsd.Tests --configuration Release
dotnet pack src/Opsd/Opsd.csproj --configuration Release --no-restore
```

## License

Licensed under either the Apache License, Version 2.0 or the MIT license, at
your option.
