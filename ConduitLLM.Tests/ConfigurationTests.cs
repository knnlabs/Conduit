using System.Collections.Generic;

using ConduitLLM.Configuration;

using Microsoft.Extensions.Configuration;

using Xunit;

namespace ConduitLLM.Tests;

public class ConfigurationTests
{
    [Fact]
    public void Bind_ConduitSettings_FromConfiguration_PopulatesCorrectly()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Conduit:DefaultTimeoutSeconds"] = "60",
            ["Conduit:DefaultRetries"] = "3",
            ["Conduit:ModelMappings:0:ModelAlias"] = "gpt-4-alias",
            ["Conduit:ModelMappings:0:ProviderName"] = "OpenAI",
            ["Conduit:ModelMappings:0:DeploymentName"] = "gpt-4",
            ["Conduit:ModelMappings:1:ModelAlias"] = "claude-3-opus-alias",
            ["Conduit:ModelMappings:1:ProviderName"] = "Anthropic",
            ["Conduit:ModelMappings:1:DeploymentName"] = "claude-3-opus-20240229",
            ["Conduit:ProviderCredentials:0:ProviderName"] = "OpenAI",
            ["Conduit:ProviderCredentials:0:ApiKey"] = "sk-openai-key",
            ["Conduit:ProviderCredentials:0:ApiBase"] = "https://api.openai.com/v1",
            ["Conduit:ProviderCredentials:1:ProviderName"] = "Anthropic",
            ["Conduit:ProviderCredentials:1:ApiKey"] = "sk-anthropic-key",
            // ApiBase might not be applicable or named differently for Anthropic
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var settings = new ConduitSettings();

        // Act
        configuration.GetSection("Conduit").Bind(settings);

        // Assert
        Assert.Equal(60, settings.DefaultTimeoutSeconds);
        Assert.Equal(3, settings.DefaultRetries);

        Assert.NotNull(settings.ModelMappings);
        Assert.Equal(2, settings.ModelMappings.Count);
        Assert.Equal("gpt-4-alias", settings.ModelMappings[0].ModelAlias);
        Assert.Equal("OpenAI", settings.ModelMappings[0].ProviderName);
        Assert.Equal("gpt-4", settings.ModelMappings[0].DeploymentName);
        Assert.Equal("claude-3-opus-alias", settings.ModelMappings[1].ModelAlias);
        Assert.Equal("Anthropic", settings.ModelMappings[1].ProviderName);
        Assert.Equal("claude-3-opus-20240229", settings.ModelMappings[1].DeploymentName);

        Assert.NotNull(settings.ProviderCredentials);
        Assert.Equal(2, settings.ProviderCredentials.Count);
        Assert.Equal("OpenAI", settings.ProviderCredentials[0].ProviderName);
        Assert.Equal("sk-openai-key", settings.ProviderCredentials[0].ApiKey);
        Assert.Equal("https://api.openai.com/v1", settings.ProviderCredentials[0].ApiBase);
        Assert.Equal("Anthropic", settings.ProviderCredentials[1].ProviderName);
        Assert.Equal("sk-anthropic-key", settings.ProviderCredentials[1].ApiKey);
        Assert.Null(settings.ProviderCredentials[1].ApiBase); // Assuming null if not provided
    }

    [Fact]
    public void Bind_ConduitSettings_EmptyConfiguration_DefaultsCorrectly()
    {
        // Arrange
        var configData = new Dictionary<string, string?>(); // Empty config

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var settings = new ConduitSettings();

        // Act
        configuration.GetSection("Conduit").Bind(settings);

        // Assert
        Assert.Null(settings.DefaultTimeoutSeconds);
        Assert.Null(settings.DefaultRetries);
        Assert.NotNull(settings.ModelMappings);
        Assert.Empty(settings.ModelMappings); // Defaults to empty list
        Assert.NotNull(settings.ProviderCredentials);
        Assert.Empty(settings.ProviderCredentials); // Defaults to empty list
    }

    [Fact]
    public void Bind_ConduitSettings_PartialConfiguration_PopulatesAvailable()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            ["Conduit:DefaultTimeoutSeconds"] = "120",
            // DefaultRetries is missing
            ["Conduit:ModelMappings:0:ModelAlias"] = "gemini-pro-alias",
            ["Conduit:ModelMappings:0:ProviderName"] = "Gemini",
            ["Conduit:ModelMappings:0:DeploymentName"] = "gemini-pro",
            // ProviderCredentials are missing
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var settings = new ConduitSettings();

        // Act
        configuration.GetSection("Conduit").Bind(settings);

        // Assert
        Assert.Equal(120, settings.DefaultTimeoutSeconds);
        Assert.Null(settings.DefaultRetries); // Should be null as it wasn't provided

        Assert.NotNull(settings.ModelMappings);
        Assert.Single(settings.ModelMappings);
        Assert.Equal("gemini-pro-alias", settings.ModelMappings[0].ModelAlias);
        Assert.Equal("Gemini", settings.ModelMappings[0].ProviderName);
        Assert.Equal("gemini-pro", settings.ModelMappings[0].DeploymentName);

        Assert.NotNull(settings.ProviderCredentials);
        Assert.Empty(settings.ProviderCredentials); // Should be empty list
    }
}
