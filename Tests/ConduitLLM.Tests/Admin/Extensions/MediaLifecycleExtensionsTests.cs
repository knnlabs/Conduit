using ConduitLLM.Admin.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Options;
using ConduitLLM.Core.Services;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Tests.Admin.Extensions;

[Trait("Category", "Unit")]
[Trait("Component", "MediaLifecycle")]
public sealed class MediaLifecycleExtensionsTests
{
    [Fact]
    public void AddMediaLifecycleServices_WithDocumentedS3Variables_UsesCanonicalEndpoint()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["CONDUIT_MEDIA_STORAGE_TYPE"] = "S3",
            ["CONDUIT_S3_ENDPOINT"] = "https://canonical.example",
            ["CONDUIT_S3_SERVICE_URL"] = "https://legacy.example",
            ["CONDUIT_S3_ACCESS_KEY_ID"] = "access",
            ["CONDUIT_S3_SECRET_ACCESS_KEY"] = "secret",
            ["CONDUIT_S3_BUCKET_NAME"] = "media"
        });
        var services = new ServiceCollection();

        services.AddMediaLifecycleServices(configuration);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IMediaStorageService) &&
            descriptor.ImplementationType == typeof(S3MediaStorageService));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<S3StorageOptions>>().Value.ServiceUrl
            .Should().Be("https://canonical.example");
    }

    [Fact]
    public void AddMediaLifecycleServices_WithLegacyServiceUrl_StillSelectsS3()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["CONDUIT_S3_SERVICE_URL"] = "https://legacy.example"
        });
        var services = new ServiceCollection();

        services.AddMediaLifecycleServices(configuration);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IMediaStorageService) &&
            descriptor.ImplementationType == typeof(S3MediaStorageService));
    }

    private static IConfiguration BuildConfiguration(
        IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
