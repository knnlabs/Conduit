namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Extension method for DateTime to Unix timestamp
    /// </summary>
    internal static class DateTimeExtensions
    {
        public static long ToUnixTimeSeconds(this DateTime dateTime)
        {
            return ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
        }
    }
}
