using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using FluentAssertions;

namespace ConduitLLM.Tests.Core.Services;

public class PromptCachingPolicyTests
{
    private static PromptCachingConfig Config(PromptCachingRule rule) => new()
    {
        SchemaVersion = 3,
        Enabled = true,
        Rules = [rule]
    };

    [Fact]
    public void Resolve_ClaudeAutomatic_ReturnsIntent()
    {
        var config = Config(new PromptCachingRule
        {
            Name = "Claude",
            Provider = "OpenRouter",
            ModelPattern = "anthropic/*",
            Strategy = PromptCachingStrategy.Automatic,
            Ttl = "1h"
        });

        var intent = PromptCachingPolicyResolver.Resolve(config, "OpenRouter", "anthropic/claude-sonnet-4");

        intent.Should().NotBeNull();
        intent!.Strategy.Should().Be(PromptCachingStrategy.Automatic);
        intent.Ttl.Should().Be("1h");
    }

    [Fact]
    public void Resolve_UnsupportedProvider_ReturnsNull()
    {
        var config = Config(new PromptCachingRule
        {
            Name = "Claude",
            Provider = "OpenRouter",
            ModelPattern = "anthropic/*",
            Strategy = PromptCachingStrategy.Automatic
        });

        PromptCachingPolicyResolver.Resolve(config, "Replicate", "anthropic/claude-sonnet-4").Should().BeNull();
    }

    [Fact]
    public void Validate_ExplicitQwenWildcard_IsRejected()
    {
        var config = Config(new PromptCachingRule
        {
            Name = "Qwen",
            Provider = "OpenRouter",
            ModelPattern = "qwen/*",
            Strategy = PromptCachingStrategy.Explicit,
            InjectionPoints = [new CacheInjectionPoint { Role = "system", Index = 0 }]
        });

        PromptCachingPolicyResolver.Validate(config).Should().ContainSingle(e => e.Contains("exact documented model"));
    }

    [Fact]
    public void Resolve_FirstMatchingRuleWins()
    {
        var config = new PromptCachingConfig
        {
            SchemaVersion = 3,
            Enabled = true,
            Rules =
            [
                new PromptCachingRule { Name = "First", Provider = "OpenRouter", ModelPattern = "anthropic/*", Strategy = PromptCachingStrategy.Automatic, Ttl = "5m" },
                new PromptCachingRule { Name = "Second", Provider = "OpenRouter", ModelPattern = "anthropic/*", Strategy = PromptCachingStrategy.Automatic, Ttl = "1h" }
            ]
        };

        PromptCachingPolicyResolver.Resolve(config, "OpenRouter", "anthropic/claude-sonnet-4")!.Ttl.Should().Be("5m");
    }
}
