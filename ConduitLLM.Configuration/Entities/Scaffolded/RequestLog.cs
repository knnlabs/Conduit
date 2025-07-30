using System;
using System.Collections.Generic;

namespace ConduitLLM.Configuration.Entities.Scaffolded;

public partial class RequestLog
{
    public int Id { get; set; }

    public int VirtualKeyId { get; set; }

    public string ModelName { get; set; } = null!;

    public string RequestType { get; set; } = null!;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public decimal Cost { get; set; }

    public double ResponseTimeMs { get; set; }

    public DateTime Timestamp { get; set; }

    public string? UserId { get; set; }

    public string? ClientIp { get; set; }

    public string? RequestPath { get; set; }

    public int? StatusCode { get; set; }

    public virtual VirtualKey VirtualKey { get; set; } = null!;
}
