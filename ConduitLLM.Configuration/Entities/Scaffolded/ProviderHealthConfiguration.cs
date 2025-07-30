using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class ProviderHealthConfiguration
{
    public int Id { get; set; }

    public int ProviderType { get; set; }

    public bool MonitoringEnabled { get; set; }

    public int CheckIntervalMinutes { get; set; }

    public int TimeoutSeconds { get; set; }

    public int ConsecutiveFailuresThreshold { get; set; }

    public bool NotificationsEnabled { get; set; }

    public string? CustomEndpointUrl { get; set; }

    public DateTime? LastCheckedUtc { get; set; }
}
