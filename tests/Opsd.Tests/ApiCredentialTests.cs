namespace Opsd.Tests;

public sealed class ApiCredentialTests
{
    [Fact]
    public void CredentialIsValidatedAndRedacted()
    {
        ApiCredential credential = new("opsd_key_secret");

        Assert.Equal("ApiCredential([REDACTED])", credential.ToString());
        Assert.DoesNotContain("opsd_key_secret", credential.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("contains\nnewline")]
    [InlineData("contains\0null")]
    [InlineData("\u007f")]
    [InlineData("£")]
    public void MalformedCredentialsAreRejected(string secret)
    {
        Assert.Throws<InvalidApiCredentialException>(() => new ApiCredential(secret));
    }

    [Fact]
    public void NullCredentialIsRejected()
    {
        Assert.Throws<InvalidApiCredentialException>(() => new ApiCredential(null!));
    }
}

