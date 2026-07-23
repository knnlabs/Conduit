using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Tests.Helpers;

internal sealed record ModelMappingSeed(
    int ModelId,
    int OpenAiProviderId,
    int SecondOpenAiProviderId,
    int GroqProviderId,
    int OpenAiAssociationId,
    int GroqAssociationId,
    int DisabledAssociationId,
    int IncompatibleAssociationId)
{
    public const string OpenAiModelId = "provider/canonical";
    public const string GroqModelId = "groq/canonical";
    public const string DisabledModelId = "provider/disabled";
    public const string IncompatibleModelId = "meta/other";
}

internal static class ModelMappingTestData
{
    public static ModelMappingSeed Seed(ConduitDbContext context)
    {
        var canonical = ModelTestHelper.CreateCompleteTestModel("canonical-model");
        var incompatible = ModelTestHelper.CreateCompleteTestModel("other-model");
        var openAi = new Provider
        {
            ProviderName = "OpenAI primary",
            ProviderType = ProviderType.OpenAI,
            IsEnabled = true
        };
        var secondOpenAi = new Provider
        {
            ProviderName = "OpenAI secondary",
            ProviderType = ProviderType.OpenAI,
            IsEnabled = true
        };
        var groq = new Provider
        {
            ProviderName = "Groq primary",
            ProviderType = ProviderType.Groq,
            IsEnabled = true
        };

        context.Models.AddRange(canonical, incompatible);
        context.Providers.AddRange(openAi, secondOpenAi, groq);
        context.SaveChanges();

        var openAiAssociation = Association(
            canonical.Id, ModelMappingSeed.OpenAiModelId, ProviderType.OpenAI);
        var groqAssociation = Association(
            canonical.Id, ModelMappingSeed.GroqModelId, ProviderType.Groq);
        var disabledAssociation = Association(
            canonical.Id, ModelMappingSeed.DisabledModelId, ProviderType.OpenAI, false);
        var incompatibleAssociation = Association(
            incompatible.Id, ModelMappingSeed.IncompatibleModelId, ProviderType.Meta);

        context.ModelProviderTypeAssociations.AddRange(
            openAiAssociation,
            groqAssociation,
            disabledAssociation,
            incompatibleAssociation);
        context.SaveChanges();

        return new ModelMappingSeed(
            canonical.Id,
            openAi.Id,
            secondOpenAi.Id,
            groq.Id,
            openAiAssociation.Id,
            groqAssociation.Id,
            disabledAssociation.Id,
            incompatibleAssociation.Id);
    }

    private static ModelProviderTypeAssociation Association(
        int modelId,
        string identifier,
        ProviderType provider,
        bool isEnabled = true) =>
        new()
        {
            ModelId = modelId,
            Identifier = identifier,
            Provider = provider,
            IsEnabled = isEnabled,
            IsPrimary = true
        };
}
