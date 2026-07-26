using ConduitLLM.Core.Utilities;

using FluentAssertions;

namespace ConduitLLM.Tests.Core.Utilities
{
    /// <summary>
    /// Unit tests for the canonical video resolution normalization. This is the single
    /// normalization shared by the Gateway endpoints, the video orchestrator and the cost
    /// calculator, so its outputs are billing-visible pricing keys.
    /// </summary>
    [Trait("Category", "Unit")]
    public class VideoUtilsResolutionTests
    {
        [Theory]
        [InlineData("1920x1080", "1080p")]
        [InlineData("1280x720", "720p")]
        [InlineData("854x480", "480p")]
        [InlineData("2560x1440", "1440p")]
        [InlineData("7680x4320", "4320p")]
        [InlineData("1024x768", "768p")]
        public void NormalizeResolution_DimensionString_ReturnsExactHeightKey(string input, string expected)
        {
            VideoUtils.NormalizeResolution(input).Should().Be(expected);
        }

        [Theory]
        [InlineData("3840x2160")]
        [InlineData("4096x2160")]
        public void NormalizeResolution_4kDimensions_ReturnsCanonical4kKey(string input)
        {
            // "4k" is the key format the WebAdmin pricing editors offer; producing "2160p"
            // here would miss the configured rate and trigger conservative fallback pricing.
            VideoUtils.NormalizeResolution(input).Should().Be("4k");
        }

        [Theory]
        [InlineData("1080p", "1080p")]
        [InlineData("1080P", "1080p")]
        [InlineData("720p", "720p")]
        [InlineData("4k", "4k")]
        [InlineData("4K", "4k")]
        public void NormalizeResolution_AlreadyNormalized_PassesThroughLowercased(string input, string expected)
        {
            VideoUtils.NormalizeResolution(input).Should().Be(expected);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void NormalizeResolution_NullOrEmpty_ReturnsInput(string? input)
        {
            VideoUtils.NormalizeResolution(input!).Should().Be(input);
        }

        [Fact]
        public void NormalizeResolution_UnparseableValue_PassesThroughLowercased()
        {
            VideoUtils.NormalizeResolution("Widescreen").Should().Be("widescreen");
        }
    }
}
