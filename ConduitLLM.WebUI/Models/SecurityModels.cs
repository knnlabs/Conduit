namespace ConduitLLM.WebUI.Models
{
    public class ThreatSource
    {
        public string IpAddress { get; set; } = string.Empty;
        public string? Location { get; set; }
        public int ThreatCount { get; set; }
        public string ThreatLevel { get; set; } = string.Empty;
        public List<string> ThreatTypes { get; set; } = new();
        public DateTime LastActivity { get; set; }
    }
}