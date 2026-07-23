using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Models;

namespace ConduitLLM.Tests.Configuration;

public sealed class ModelCapabilityResolverTests
{
    [Fact]
    public void Resolve_DistinguishesVideoInputFromVideoGeneration()
    {
        var model = new Model
        {
            SupportsVideoGeneration = false,
            InputModalitiesJson = ModelModalities.Serialize(["text", "video"]),
            OutputModalitiesJson = ModelModalities.Serialize(["text"])
        };

        var capabilities = ModelCapabilityResolver.Resolve(model);

        Assert.True(capabilities.SupportsVideoInput);
        Assert.True(capabilities.SupportsVideoUnderstanding);
        Assert.False(capabilities.SupportsVideoGeneration);
    }

    [Fact]
    public void Resolve_PreservesUnknownInsteadOfTreatingItAsUnsupported()
    {
        var model = new Model { CapabilitySource = ModelCapabilitySource.Unknown };

        var capabilities = ModelCapabilityResolver.Resolve(model);

        Assert.Null(capabilities.InputModalities);
        Assert.Null(capabilities.OutputModalities);
        Assert.Equal(ModelCapabilitySource.Unknown, capabilities.Source);
    }

    [Fact]
    public void Resolve_ProviderOverrideWinsWithoutChangingCanonicalModel()
    {
        var model = new Model
        {
            SupportsChat = true,
            InputModalitiesJson = ModelModalities.Serialize(["text"]),
            OutputModalitiesJson = ModelModalities.Serialize(["text"]),
            CapabilitySource = ModelCapabilitySource.Curated
        };
        var association = new ModelProviderTypeAssociation
        {
            InputModalitiesJson = ModelModalities.Serialize(["text", "video"]),
            CapabilitySource = ModelCapabilitySource.ProviderApi
        };

        var capabilities = ModelCapabilityResolver.Resolve(model, association);

        Assert.True(capabilities.SupportsVideoInput);
        Assert.Equal(ModelCapabilitySource.ProviderApi, capabilities.Source);
        Assert.Equal(["text"], ModelModalities.Parse(model.InputModalitiesJson));
    }

    [Fact]
    public void Resolve_DerivesLegacyVisionAliasFromEffectiveImageInput()
    {
        var model = new Model
        {
            SupportsVision = false,
            InputModalitiesJson = ModelModalities.Serialize(["text"]),
            OutputModalitiesJson = ModelModalities.Serialize(["text"])
        };
        var association = new ModelProviderTypeAssociation
        {
            InputModalitiesJson = ModelModalities.Serialize(["text", "image"])
        };

        var capabilities = ModelCapabilityResolver.Resolve(model, association);

        Assert.True(capabilities.SupportsImageInput);
        Assert.True(capabilities.SupportsVision);
    }
}
