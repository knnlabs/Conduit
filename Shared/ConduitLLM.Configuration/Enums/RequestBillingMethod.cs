namespace ConduitLLM.Configuration.Enums
{
    /// <summary>
    /// How a request's cost was determined for billing.
    /// </summary>
    public enum RequestBillingMethod
    {
        /// <summary>
        /// Cost was computed from the configured ModelCost rates (the default/historical behavior).
        /// </summary>
        ModelCost = 0,

        /// <summary>
        /// Cost was billed from the provider-reported per-request cost (times the provider markup),
        /// bypassing ModelCost. Refunds for these requests are prorated from the charged amount.
        /// </summary>
        ProviderReportedCost = 1
    }
}
