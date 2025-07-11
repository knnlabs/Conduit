namespace ConduitLLM.AdminClient.Constants;

/// <summary>
/// Date and time format constants for consistent API communication.
/// </summary>
public static class DateFormats
{
    /// <summary>
    /// Standard API date-time format (ISO 8601 with UTC timezone).
    /// </summary>
    public const string ApiDateTime = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>
    /// API date format (ISO 8601 date only).
    /// </summary>
    public const string ApiDate = "yyyy-MM-dd";

    /// <summary>
    /// API time format (24-hour format).
    /// </summary>
    public const string ApiTime = "HH:mm:ss";

    /// <summary>
    /// Extended API date-time format with milliseconds.
    /// </summary>
    public const string ApiDateTimeWithMilliseconds = "yyyy-MM-ddTHH:mm:ss.fffZ";
}