namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// Status of message acknowledgment
    /// </summary>
    public enum AcknowledgmentStatus
    {
        /// <summary>
        /// Message is pending acknowledgment
        /// </summary>
        Pending,

        /// <summary>
        /// Message was successfully acknowledged
        /// </summary>
        Acknowledged,

        /// <summary>
        /// Message was negatively acknowledged (NACK)
        /// </summary>
        NegativelyAcknowledged,

        /// <summary>
        /// Message acknowledgment timed out
        /// </summary>
        TimedOut,

        /// <summary>
        /// Message acknowledgment failed after all retries
        /// </summary>
        Failed,

        /// <summary>
        /// Message was expired before delivery
        /// </summary>
        Expired
    }
}