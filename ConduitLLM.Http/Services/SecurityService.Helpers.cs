using System;

namespace ConduitLLM.Http.Services
{
    public partial class SecurityService
    {
        // Data structures for Redis storage (compatible with WebUI/Admin)
        private class FailedAuthData
        {
            public int Attempts { get; set; }
            public string Source { get; set; } = "";
            public DateTime LastAttempt { get; set; }
            public string LastAttemptedKey { get; set; } = "";
        }

        private class BannedIpInfo
        {
            public DateTime BannedUntil { get; set; }
            public int FailedAttempts { get; set; }
            public string Source { get; set; } = "";
            public string Reason { get; set; } = "";
            public string LastAttemptedKey { get; set; } = "";
        }

        private class RateLimitData
        {
            public int Count { get; set; }
            public string Source { get; set; } = "";
            public DateTime WindowStart { get; set; }
        }
    }

    // Extension method for DateTime to Unix timestamp
    internal static class DateTimeExtensions
    {
        public static long ToUnixTimeSeconds(this DateTime dateTime)
        {
            return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
        }
    }
}