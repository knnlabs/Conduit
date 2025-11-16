namespace ConduitLLM.Http.Models
{
    // This file has been moved to ConduitLLM.Configuration.DTOs.SignalR.SystemNotifications
    // All types are now available via type forwarding
    // TODO: Remove this file in the next major version

    // Type forwarding for backward compatibility
    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.SystemNotification instead")]
    public abstract class SystemNotification : ConduitLLM.Configuration.DTOs.SignalR.SystemNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.ProviderHealthNotification instead")]
    public class ProviderHealthNotification : ConduitLLM.Configuration.DTOs.SignalR.ProviderHealthNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.RateLimitNotification instead")]
    public class RateLimitNotification : ConduitLLM.Configuration.DTOs.SignalR.RateLimitNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.SystemAnnouncementNotification instead")]
    public class SystemAnnouncementNotification : ConduitLLM.Configuration.DTOs.SignalR.SystemAnnouncementNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.ServiceDegradationNotification instead")]
    public class ServiceDegradationNotification : ConduitLLM.Configuration.DTOs.SignalR.ServiceDegradationNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.ServiceRestorationNotification instead")]
    public class ServiceRestorationNotification : ConduitLLM.Configuration.DTOs.SignalR.ServiceRestorationNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.ModelMappingNotification instead")]
    public class ModelMappingNotification : ConduitLLM.Configuration.DTOs.SignalR.ModelMappingNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.ModelCapabilitiesNotification instead")]
    public class ModelCapabilitiesNotification : ConduitLLM.Configuration.DTOs.SignalR.ModelCapabilitiesNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.ModelAvailabilityNotification instead")]
    public class ModelAvailabilityNotification : ConduitLLM.Configuration.DTOs.SignalR.ModelAvailabilityNotification { }
}