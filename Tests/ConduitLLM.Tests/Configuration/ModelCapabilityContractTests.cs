using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Models;
using ConduitLLM.Core.Models;
using ConduitLLM.Providers.Common.Models;

namespace ConduitLLM.Tests.Configuration;

public sealed class ModelCapabilityContractTests
{
    [Fact]
    public void ResolverAndProviderDiscovery_UseConfigurationCapabilityDto()
    {
        var resolveMethod = typeof(ModelCapabilityResolver).GetMethod(
            nameof(ModelCapabilityResolver.Resolve),
            [typeof(ConduitLLM.Configuration.Entities.Model),
             typeof(ConduitLLM.Configuration.Entities.ModelProviderTypeAssociation)]);
        var providerCapabilities = typeof(ExtendedModelInfo).GetProperty(
            nameof(ExtendedModelInfo.Capabilities));

        Assert.NotNull(resolveMethod);
        Assert.Equal(typeof(ModelCapabilitiesDto), resolveMethod!.ReturnType);
        Assert.Equal(typeof(ModelCapabilitiesDto), providerCapabilities!.PropertyType);
    }

    [Fact]
    public void RemovedLegacyCapabilityModels_DoNotRemainInProductAssemblies()
    {
        Assert.Null(typeof(ModelInfo).Assembly.GetType(
            "ConduitLLM.Core.Models.Configuration.ModelCapabilities"));
        Assert.Null(typeof(ExtendedModelInfo).Assembly.GetType(
            "ConduitLLM.Providers.Common.Models.ModelCapabilities"));
    }
}
