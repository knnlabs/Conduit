using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using FluentAssertions;

namespace ConduitLLM.Tests.Core.Services;

public sealed class PromptCachingV3PolicyTests
{
    [Fact]
    public void Migrate_V2PreservesOrderAndConvertsStrategies()
    {
#pragma warning disable CS0618
        var config = new PromptCachingConfig
        {
            SchemaVersion = 2, Enabled = true,
            Rules =
            [
                new PromptCachingRule { Name = "one", Provider = "OpenRouter", ModelPattern = "anthropic/*", Strategy = PromptCachingStrategy.OpenRouterAutomatic },
                new PromptCachingRule { Name = "two", Provider = "OpenRouter", ModelPattern = "anthropic/*", Strategy = PromptCachingStrategy.OpenRouterExplicit,
                    InjectionPoints = [new CacheInjectionPoint { Role = "system", Index = 0 }] }
            ]
        };
#pragma warning restore CS0618

        var migrated = PromptCachingPolicyResolver.Migrate(config);

        migrated.SchemaVersion.Should().Be(3);
        migrated.Rules.Select(rule => rule.Name).Should().ContainInOrder("one", "two");
        migrated.Rules.Select(rule => rule.Strategy).Should().ContainInOrder(PromptCachingStrategy.Automatic, PromptCachingStrategy.Explicit);
    }

    [Theory]
    [InlineData("gpt-5.6")]
    [InlineData("gpt-5.7-mini")]
    [InlineData("gpt-6")]
    public void OpenAIExplicit_SupportedFor56OrLater(string model)
    {
        var config = Config(PromptCachingStrategy.Explicit, model, "30m",
            [new CacheInjectionPoint { Role = "developer", Index = 0 }]);
        PromptCachingPolicyResolver.Resolve(config, "OpenAI", model, "opaque").Should().NotBeNull();
    }

    [Fact]
    public void OpenAIExplicit_OlderModelRejected()
    {
        var config = Config(PromptCachingStrategy.Explicit, "gpt-5.5", "30m",
            [new CacheInjectionPoint { Role = "system", Index = 0 }]);
        PromptCachingPolicyResolver.Validate(config).Should().Contain(error => error.Contains("GPT-5.6"));
    }

    [Fact]
    public void Groq_IsObservableButHasNoManagedControls()
    {
        PromptCachingProviderAdapters.Capabilities.Should().Contain(capability =>
            capability.Provider == "Groq" && capability.ProviderManaged && capability.Strategies.Count == 0);
    }

    private static PromptCachingConfig Config(PromptCachingStrategy strategy, string model, string ttl,
        List<CacheInjectionPoint> points) => new()
    {
        SchemaVersion = 3, Enabled = true,
        Rules = [new PromptCachingRule { Name = "rule", Provider = "OpenAI", ModelPattern = model,
            Strategy = strategy, Ttl = ttl, InjectionPoints = points }]
    };
}
