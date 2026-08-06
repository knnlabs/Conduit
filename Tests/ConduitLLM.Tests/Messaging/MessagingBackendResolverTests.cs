using ConduitLLM.Configuration.Messaging;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace ConduitLLM.Tests.Messaging
{
    public class MessagingBackendResolverTests
    {
        private static IConfiguration BuildConfiguration(string? backendValue)
        {
            var values = new Dictionary<string, string?>();
            if (backendValue != null)
            {
                values[MessagingBackendResolver.ConfigurationKey] = backendValue;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        [Fact]
        public void Resolve_KeyAbsent_DefaultsToWolverine()
        {
            var result = MessagingBackendResolver.Resolve(BuildConfiguration(null));

            result.Should().Be(MessagingBackend.Wolverine);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Resolve_KeyEmpty_DefaultsToWolverine(string value)
        {
            var result = MessagingBackendResolver.Resolve(BuildConfiguration(value));

            result.Should().Be(MessagingBackend.Wolverine);
        }

        [Theory]
        [InlineData("Wolverine")]
        [InlineData("wolverine")]
        [InlineData("WOLVERINE")]
        [InlineData(" Wolverine ")]
        public void Resolve_WolverineAnyCase_ReturnsWolverine(string value)
        {
            var result = MessagingBackendResolver.Resolve(BuildConfiguration(value));

            result.Should().Be(MessagingBackend.Wolverine);
        }

        [Theory]
        [InlineData("MassTransit")]
        [InlineData("masstransit")]
        [InlineData(" MassTransit ")]
        public void Resolve_RemovedMassTransitBackend_ThrowsWithRemovalGuidance(string value)
        {
            var act = () => MessagingBackendResolver.Resolve(BuildConfiguration(value));

            // The removed rollback backend must fail the boot loudly with a clear pointer to
            // Wolverine (I3.1/#932) rather than silently defaulting.
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*removed in #932*")
                .WithMessage("*Wolverine*");
        }

        [Fact]
        public void Resolve_UnrecognizedValue_Throws()
        {
            var act = () => MessagingBackendResolver.Resolve(BuildConfiguration("Rebus"));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Rebus*")
                .WithMessage("*Wolverine*");
        }
    }
}
