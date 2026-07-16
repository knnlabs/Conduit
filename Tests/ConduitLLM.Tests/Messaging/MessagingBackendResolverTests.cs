using ConduitLLM.Configuration.Messaging;

using FluentAssertions;

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
        public void Resolve_KeyAbsent_DefaultsToMassTransit()
        {
            var result = MessagingBackendResolver.Resolve(BuildConfiguration(null));

            result.Should().Be(MessagingBackend.MassTransit);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Resolve_KeyEmpty_DefaultsToMassTransit(string value)
        {
            var result = MessagingBackendResolver.Resolve(BuildConfiguration(value));

            result.Should().Be(MessagingBackend.MassTransit);
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
        public void Resolve_MassTransitAnyCase_ReturnsMassTransit(string value)
        {
            var result = MessagingBackendResolver.Resolve(BuildConfiguration(value));

            result.Should().Be(MessagingBackend.MassTransit);
        }

        [Fact]
        public void Resolve_UnrecognizedValue_Throws()
        {
            var act = () => MessagingBackendResolver.Resolve(BuildConfiguration("Rebus"));

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*Rebus*")
                .WithMessage("*MassTransit, Wolverine*");
        }
    }
}
