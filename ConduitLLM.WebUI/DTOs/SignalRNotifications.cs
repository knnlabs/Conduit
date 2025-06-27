using System;

namespace ConduitLLM.WebUI.DTOs
{
    // This file has been moved to ConduitLLM.Configuration.DTOs.SignalR.SpendNotifications
    // All types are now available via type forwarding
    // TODO: Remove this file in the next major version

    // Type forwarding for backward compatibility
    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.SpendUpdateNotification instead")]
    public class SpendUpdateNotification : ConduitLLM.Configuration.DTOs.SignalR.SpendUpdateNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.BudgetAlertNotification instead")]
    public class BudgetAlertNotification : ConduitLLM.Configuration.DTOs.SignalR.BudgetAlertNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.SpendSummaryNotification instead")]
    public class SpendSummaryNotification : ConduitLLM.Configuration.DTOs.SignalR.SpendSummaryNotification { }

    [Obsolete("Use ConduitLLM.Configuration.DTOs.SignalR.UnusualSpendingNotification instead")]
    public class UnusualSpendingNotification : ConduitLLM.Configuration.DTOs.SignalR.UnusualSpendingNotification { }
}