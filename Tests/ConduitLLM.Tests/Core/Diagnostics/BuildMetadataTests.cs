using ConduitLLM.Core.Diagnostics;

using AwesomeAssertions;

namespace ConduitLLM.Tests.Core.Diagnostics;

public class BuildMetadataTests
{
    [Fact]
    public void FromAssembly_ReturnsVersionAndSafeBuildFallbacks()
    {
        var metadata = BuildMetadata.FromAssembly(typeof(BuildMetadata).Assembly);

        metadata.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+");
        metadata.CommitSha.Should().NotBeNullOrWhiteSpace();
        metadata.BuildTimestamp.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void FromAssembly_UsesSafeFallbacksWhenConduitMetadataIsAbsent()
    {
        var metadata = BuildMetadata.FromAssembly(typeof(string).Assembly);

        metadata.CommitSha.Should().Be("dev");
        metadata.BuildTimestamp.Should().Be("unknown");
    }
}
