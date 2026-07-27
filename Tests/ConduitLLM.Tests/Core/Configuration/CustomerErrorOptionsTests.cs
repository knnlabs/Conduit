using ConduitLLM.Core.Configuration;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Core.Configuration
{
    /// <summary>
    /// Tests mutate process environment variables, so they must not run in parallel
    /// with each other; the collection serializes them.
    /// </summary>
    [Collection("CustomerErrorEnvironment")]
    public class CustomerErrorOptionsTests
    {
        private readonly Mock<ILogger> _loggerMock = new();

        private CustomerErrorOptions RunWithMode(string? value)
        {
            var original = Environment.GetEnvironmentVariable(CustomerErrorOptions.ModeVariable);
            try
            {
                Environment.SetEnvironmentVariable(CustomerErrorOptions.ModeVariable, value);
                return CustomerErrorOptions.FromEnvironment(_loggerMock.Object);
            }
            finally
            {
                Environment.SetEnvironmentVariable(CustomerErrorOptions.ModeVariable, original);
            }
        }

        [Fact]
        public void FromEnvironment_NoVariable_DefaultsToExternal()
        {
            var options = RunWithMode(null);

            Assert.Equal(CustomerErrorMode.External, options.Mode);
        }

        [Fact]
        public void FromEnvironment_BlankVariable_DefaultsToExternal()
        {
            var options = RunWithMode("   ");

            Assert.Equal(CustomerErrorMode.External, options.Mode);
        }

        [Theory]
        [InlineData("internal", CustomerErrorMode.Internal)]
        [InlineData("Internal", CustomerErrorMode.Internal)]
        [InlineData("INTERNAL", CustomerErrorMode.Internal)]
        [InlineData(" Internal ", CustomerErrorMode.Internal)]
        [InlineData("external", CustomerErrorMode.External)]
        [InlineData("External", CustomerErrorMode.External)]
        public void FromEnvironment_ValidValueAnyCase_ParsesMode(string raw, CustomerErrorMode expected)
        {
            var options = RunWithMode(raw);

            Assert.Equal(expected, options.Mode);
        }

        [Fact]
        public void FromEnvironment_InvalidValue_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => RunWithMode("Public"));

            Assert.Contains("Public", ex.Message);
            Assert.Contains(CustomerErrorOptions.ModeVariable, ex.Message);
        }
    }

    [CollectionDefinition("CustomerErrorEnvironment")]
    public class CustomerErrorEnvironmentCollection
    {
    }
}
