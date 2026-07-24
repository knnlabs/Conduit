namespace ConduitLLM.Providers.Configuration
{
    /// <summary>
    /// Describes how a structured provider setting value is applied to an outbound request.
    /// </summary>
    public enum ProviderSettingBinding
    {
        /// <summary>
        /// The value replaces a <c>{token}</c> placeholder in the provider base URL
        /// (for example Cloudflare's <c>.../accounts/{account_id}/ai/v1</c>).
        /// </summary>
        UrlPathToken = 0,

        /// <summary>
        /// The value is sent as an HTTP request header (for example <c>OpenAI-Organization</c>).
        /// Declared for future phases; header application is not wired in Phase 1.
        /// </summary>
        Header = 1,

        /// <summary>
        /// The value is sent as a query-string parameter (for example Azure's <c>api-version</c>).
        /// Declared for future phases; query application is not wired in Phase 1.
        /// </summary>
        QueryParam = 2,

        /// <summary>
        /// The value participates in request authentication/signing (for example an AWS region).
        /// Declared for future phases; auth-scope application is not wired in Phase 1.
        /// </summary>
        AuthScope = 3,
    }

    /// <summary>
    /// Declares a structured, provider-scoped configuration value that an operator supplies in
    /// addition to the API key (for example a Cloudflare account ID or an OpenAI organization).
    /// </summary>
    /// <remarks>
    /// These declarations are the single source of truth consumed by request-time resolution,
    /// validation, and the WebAdmin provider form. Non-secret values are stored in
    /// <c>Provider.Settings</c>; secret-valued settings are reserved for a later phase and must not
    /// be placed in that non-encrypted bag.
    /// </remarks>
    public record ProviderSettingDefinition
    {
        /// <summary>
        /// Stable machine key for the setting (for example <c>account_id</c>). Used as the storage
        /// key in <c>Provider.Settings</c> and, for <see cref="ProviderSettingBinding.UrlPathToken"/>,
        /// as the default placeholder token name when <see cref="BindingTarget"/> is not set.
        /// </summary>
        public required string Key { get; init; }

        /// <summary>
        /// Human-readable field label shown in the WebAdmin form (for example <c>Account ID</c>).
        /// </summary>
        public required string Label { get; init; }

        /// <summary>
        /// Optional help text describing where to find the value.
        /// </summary>
        public string? HelpText { get; init; }

        /// <summary>
        /// Whether the operator must supply this setting for the provider to function.
        /// </summary>
        public bool Required { get; init; }

        /// <summary>
        /// Whether the value is sensitive. Secret settings are reserved for a later phase and are not
        /// stored in the non-encrypted <c>Provider.Settings</c> bag.
        /// </summary>
        public bool Secret { get; init; }

        /// <summary>
        /// Optional .NET regular expression the value must match. Used for validation in both the
        /// backend and the WebAdmin form.
        /// </summary>
        public string? ValidationRegex { get; init; }

        /// <summary>
        /// How the value is applied to an outbound request.
        /// </summary>
        public required ProviderSettingBinding Binding { get; init; }

        /// <summary>
        /// The binding-specific target: the placeholder token name for
        /// <see cref="ProviderSettingBinding.UrlPathToken"/>, the header name for
        /// <see cref="ProviderSettingBinding.Header"/>, or the parameter name for
        /// <see cref="ProviderSettingBinding.QueryParam"/>. Defaults to <see cref="Key"/> when null.
        /// </summary>
        public string? BindingTarget { get; init; }

        /// <summary>
        /// The effective binding target, falling back to <see cref="Key"/> when
        /// <see cref="BindingTarget"/> is not explicitly set.
        /// </summary>
        public string EffectiveBindingTarget => string.IsNullOrWhiteSpace(BindingTarget) ? Key : BindingTarget;
    }
}
