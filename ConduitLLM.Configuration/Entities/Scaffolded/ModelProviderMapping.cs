using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class ModelProviderMapping
{
    public int Id { get; set; }

    public string ModelAlias { get; set; } = null!;

    public string ProviderModelName { get; set; } = null!;

    public int ProviderCredentialId { get; set; }

    public bool IsEnabled { get; set; }

    public int? MaxContextTokens { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool SupportsVision { get; set; }

    public bool SupportsAudioTranscription { get; set; }

    public bool SupportsTextToSpeech { get; set; }

    public bool SupportsRealtimeAudio { get; set; }

    public bool SupportsImageGeneration { get; set; }

    public bool SupportsVideoGeneration { get; set; }

    public bool SupportsEmbeddings { get; set; }

    public bool SupportsFunctionCalling { get; set; }

    public bool SupportsStreaming { get; set; }

    public string? TokenizerType { get; set; }

    public string? SupportedVoices { get; set; }

    public string? SupportedLanguages { get; set; }

    public string? SupportedFormats { get; set; }

    public bool IsDefault { get; set; }

    public string? DefaultCapabilityType { get; set; }

    public bool SupportsChat { get; set; }

    public virtual ProviderCredential ProviderCredential { get; set; } = null!;
}
