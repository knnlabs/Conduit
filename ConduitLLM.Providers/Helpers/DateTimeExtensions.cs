namespace ConduitLLM.Providers.Helpers
{
    /// <summary>
    /// Extensions for DateTime to provide Unix timestamp functionality.
    /// </summary>
    public static class DateTimeExtensions
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Converts a DateTime to Unix timestamp (seconds since Unix epoch).
        /// </summary>
        /// <param name="dateTime">The DateTime to convert.</param>
        /// <returns>Number of seconds since January 1, 1970, 00:00:00 UTC.</returns>
        public static long ToUnixTimeSeconds(this DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalSeconds;
        }

        /// <summary>
        /// Converts a DateTime to Unix timestamp (milliseconds since Unix epoch).
        /// </summary>
        /// <param name="dateTime">The DateTime to convert.</param>
        /// <returns>Number of milliseconds since January 1, 1970, 00:00:00 UTC.</returns>
        public static long ToUnixTimeMilliseconds(this DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - UnixEpoch).TotalMilliseconds;
        }
    }
}
