using System;
using System.Collections.Generic;
using ConduitLLM.WebUI.DTOs;

namespace ConduitLLM.WebUI.Models
{
    public class AlertRuleViewModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Component { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int CooldownMinutes { get; set; } = 5;
        public DateTime? LastTriggered { get; set; }
    }

    public class AlertSuppressionViewModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AlertPattern { get; set; } = string.Empty;
        public string? Component { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AlertHistoryEntryViewModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AlertId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? User { get; set; }
        public string? Notes { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}