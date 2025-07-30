using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class AudioProviderConfig
{
    public int Id { get; set; }

    public int ProviderCredentialId { get; set; }

    public bool TranscriptionEnabled { get; set; }

    public string? DefaultTranscriptionModel { get; set; }

    public bool TextToSpeechEnabled { get; set; }

    public string? DefaultTtsmodel { get; set; }

    public string? DefaultTtsvoice { get; set; }

    public bool RealtimeEnabled { get; set; }

    public string? DefaultRealtimeModel { get; set; }

    public string? RealtimeEndpoint { get; set; }

    public string? CustomSettings { get; set; }

    public int RoutingPriority { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ProviderCredential ProviderCredential { get; set; } = null!;
}
