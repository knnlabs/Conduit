using System.Text.Json;

using ConduitLLM.Configuration.Services;

namespace ConduitLLM.Tests.Configuration.Services;

public sealed class ConduitValidationResultTests
{
    [Fact]
    public void Failure_UsesStructuredErrors()
    {
        var result = ConduitValidationResult.Failure("Required", "name")
            .AddWarning("Deprecated");

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("name", error.Field);
        Assert.Equal("Required", error.Message);
        Assert.Equal("Deprecated", Assert.Single(result.Warnings));
    }

    [Fact]
    public void Result_RoundTripsReadOnlyCollections()
    {
        var original = ConduitValidationResult.Failure("Required", "name")
            .AddWarning("Deprecated");

        var json = JsonSerializer.Serialize(original);
        var result = JsonSerializer.Deserialize<ConduitValidationResult>(json);

        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Equal("Required", Assert.Single(result.Errors).Message);
        Assert.Equal("Deprecated", Assert.Single(result.Warnings));
    }
}
