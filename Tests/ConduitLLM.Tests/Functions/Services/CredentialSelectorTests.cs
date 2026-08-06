using ConduitLLM.Functions.Entities;
using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Services;

namespace ConduitLLM.Tests.Functions.Services;

public class CredentialSelectorTests
{
    private const int ConfigId = 7;

    private static FunctionCredential Cred(int id, int? configId, bool primary = false, bool enabled = true) => new()
    {
        Id = id,
        ProviderType = FunctionProviderType.Mcp,
        FunctionConfigurationId = configId,
        IsPrimary = primary,
        IsEnabled = enabled,
        ApiKey = $"key-{id}"
    };

    [Fact]
    public void PrefersConfigScopedOverGlobal()
    {
        var creds = new[] { Cred(1, configId: null), Cred(2, configId: ConfigId) };

        var selected = CredentialSelector.SelectForConfiguration(creds, ConfigId);

        Assert.Equal(2, selected!.Id);
    }

    [Fact]
    public void FallsBackToGlobalWhenNoScopedCredential()
    {
        var creds = new[] { Cred(1, configId: null), Cred(2, configId: 999) };

        var selected = CredentialSelector.SelectForConfiguration(creds, ConfigId);

        Assert.Equal(1, selected!.Id);
    }

    [Fact]
    public void PrefersPrimaryWithinScope()
    {
        var creds = new[]
        {
            Cred(1, configId: ConfigId, primary: false),
            Cred(2, configId: ConfigId, primary: true)
        };

        var selected = CredentialSelector.SelectForConfiguration(creds, ConfigId);

        Assert.Equal(2, selected!.Id);
    }

    [Fact]
    public void IgnoresDisabledCredentials()
    {
        var creds = new[]
        {
            Cred(1, configId: ConfigId, enabled: false),
            Cred(2, configId: null, enabled: true)
        };

        var selected = CredentialSelector.SelectForConfiguration(creds, ConfigId);

        Assert.Equal(2, selected!.Id); // scoped one disabled => falls back to enabled global
    }

    [Fact]
    public void ReturnsNullWhenNoneEnabled()
    {
        var creds = new[] { Cred(1, configId: ConfigId, enabled: false) };

        Assert.Null(CredentialSelector.SelectForConfiguration(creds, ConfigId));
    }
}
