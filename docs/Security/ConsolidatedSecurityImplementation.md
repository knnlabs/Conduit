# ConduitLLM Consolidated Security Implementation Summary

This document provides a comprehensive overview of the security features, architecture, and configurations implemented across the ConduitLLM platform, including the Admin API, Core API (HTTP API), and WebUI services.

## I. Overall Security Philosophy & Shared Concepts

ConduitLLM employs a defense-in-depth strategy, applying multiple layers of security to protect its components and data. Key shared concepts include:

### A. Unified Security Approach
Where applicable, security components (like options classes, middleware structure, and service logic for IP filtering, rate limiting, and failed authentication tracking) are designed with a unified approach. This promotes consistency, maintainability, and shared understanding across the different services (Admin API, Core API, WebUI).

### B. Redis Integration for Shared Tracking
- **Distributed Tracking**: When `CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING` is enabled and a Redis instance (`REDIS_URL` or `CONDUIT_REDIS_CONNECTION_STRING`) is configured, security-related data is shared across all services and instances.
    - **Shared Ban List**: IPs banned due to excessive failed login attempts are recognized by all services.
    - **Distributed Counters**: Rate limit counters and failed login attempts can be tracked globally.
- **Key Structure**: Uses a compatible key structure (e.g., `ban:{ip}`, `failed_login:{ip}`, `rate_limit:{service-name}:{ip}`).
- **Fallback**: Services typically fall back to in-memory tracking if Redis is unavailable, ensuring continued operation (though without cross-instance sharing).

### C. Environment Variable Configuration
All critical security settings are configurable via environment variables, allowing for flexible deployment and adherence to 12-factor app principles. Common variables include:
- `CONDUIT_MASTER_KEY`: Master authentication key, primarily for Admin API and inter-service communication initiated by WebUI.
- `CONDUIT_WEBUI_AUTH_KEY`: Specific authentication key for accessing the WebUI.
- `CONDUIT_SECURITY_USE_DISTRIBUTED_TRACKING`: Enables/disables Redis for shared security state.
- `REDIS_URL` / `CONDUIT_REDIS_CONNECTION_STRING`: Specifies Redis connection.

### D. Security Headers
All services implement robust, configurable security headers (e.g., CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy) to protect against common web vulnerabilities. These are typically applied early in the request pipeline.

## II. Admin API Security

The Admin API is responsible for administrative functions and is secured with the following measures:

### A. Key Security Features

1.  **API Key Authentication**:
    *   Validates against the `CONDUIT_MASTER_KEY`.
    *   Supports multiple header names (e.g., `X-API-Key`, `Authorization`) for flexibility.
    *   Tracks failed authentication attempts.
2.  **IP Filtering**:
    *   Configurable whitelist/blacklist via environment variables (`CONDUIT_ADMIN_IP_FILTER_WHITELIST`, `CONDUIT_ADMIN_IP_FILTER_BLACKLIST`).
    *   Supports CIDR notation and private IP detection (`CONDUIT_ADMIN_IP_FILTER_ALLOW_PRIVATE`).
    *   Modes: "permissive" (allow by default, use blacklist) or "restrictive" (deny by default, use whitelist) via `CONDUIT_ADMIN_IP_FILTER_MODE`.
    *   Integrates with database-backed IP filter rules managed via the API itself.
3.  **Rate Limiting**:
    *   Per-IP request limits, configured by `CONDUIT_ADMIN_RATE_LIMIT_MAX_REQUESTS` and `CONDUIT_ADMIN_RATE_LIMIT_WINDOW_SECONDS`.
    *   Uses a sliding window algorithm.
    *   Excludes health and Swagger endpoints.
4.  **Failed Authentication Protection**:
    *   Automatic IP banning after `CONDUIT_ADMIN_MAX_FAILED_AUTH_ATTEMPTS`.
    *   Ban duration set by `CONDUIT_ADMIN_AUTH_BAN_DURATION_MINUTES`.
    *   Shared ban list via Redis if enabled.

### B. Implementation Details
- **Components**: Uses `SecurityOptions`, `SecurityService`, and `SecurityMiddleware` within the `ConduitLLM.Admin` project.
- **Documentation**: Detailed architecture in `ConduitLLM.Admin/docs/SECURITY-ARCHITECTURE.md`.

### C. Admin API Specific Environment Variables
- `CONDUIT_ADMIN_IP_FILTERING_ENABLED`
- `CONDUIT_ADMIN_IP_FILTER_MODE`
- `CONDUIT_ADMIN_IP_FILTER_WHITELIST` / `CONDUIT_ADMIN_IP_FILTER_BLACKLIST`
- `CONDUIT_ADMIN_IP_FILTER_ALLOW_PRIVATE`
- `CONDUIT_ADMIN_RATE_LIMITING_ENABLED`
- `CONDUIT_ADMIN_RATE_LIMIT_MAX_REQUESTS` / `CONDUIT_ADMIN_RATE_LIMIT_WINDOW_SECONDS`
- `CONDUIT_ADMIN_MAX_FAILED_AUTH_ATTEMPTS` / `CONDUIT_ADMIN_AUTH_BAN_DURATION_MINUTES`

## III. Core API (HTTP API) Security

The Core API provides LLM functionalities and has been significantly hardened.

### A. Critical Security Gap Fixed
Previously, Core API LLM endpoints were unprotected. **Virtual Key authentication is now strictly enforced.**

### B. Key Security Features

1.  **Virtual Key Authentication**:
    *   Validates Conduit-specific virtual keys (e.g., `condt_...`) from multiple headers (`Authorization: Bearer`, `api-key`, `X-API-Key`).
    *   Checks key status (enabled/disabled), expiration, budget limits, and allowed models.
    *   Uses `VirtualKeyAuthenticationMiddleware`.
2.  **IP-Based Brute Force Protection**:
    *   Tracks failed Virtual Key authentication attempts per IP **across ALL Virtual Keys**.
    *   Bans IP after `CONDUIT_CORE_MAX_FAILED_AUTH_ATTEMPTS` (default: 10).
    *   Ban duration set by `CONDUIT_CORE_AUTH_BAN_DURATION_MINUTES` (default: 30 mins).
    *   Shared ban list via Redis.
3.  **Rate Limiting (Two-Layer)**:
    *   **IP-based pre-authentication**: Limits requests per IP (e.g., `CONDUIT_CORE_RATE_LIMIT_MAX_REQUESTS` = 1000 req/min) before Virtual Key validation.
    *   **Virtual Key-based post-authentication**: Enforces RPM/RPD limits defined for each virtual key (if `CONDUIT_CORE_ENFORCE_VKEY_RATE_LIMITS` is true).
4.  **IP Filtering**:
    *   Similar to Admin API: database-driven rules with environment variable overrides (`CONDUIT_CORE_IP_FILTERING_ENABLED`, `CONDUIT_CORE_IP_FILTER_MODE`, etc.).
    *   CIDR support and private IP auto-allow option.

### C. Implementation Details
- **Components**: Uses `SecurityOptions`, `SecurityService`, `SecurityMiddleware`, and `VirtualKeyAuthenticationMiddleware` within the `ConduitLLM.Http` project.
- **Documentation**: Detailed architecture in `ConduitLLM.Http/docs/SECURITY-ARCHITECTURE.md`.

### D. Core API Specific Environment Variables
- `CONDUIT_CORE_IP_FILTERING_ENABLED`
- `CONDUIT_CORE_IP_FILTER_MODE`
- `CONDUIT_CORE_IP_FILTER_WHITELIST` / `CONDUIT_CORE_IP_FILTER_BLACKLIST`
- `CONDUIT_CORE_IP_FILTER_ALLOW_PRIVATE`
- `CONDUIT_CORE_RATE_LIMITING_ENABLED` (for IP-based pre-auth)
- `CONDUIT_CORE_RATE_LIMIT_MAX_REQUESTS` / `CONDUIT_CORE_RATE_LIMIT_WINDOW_SECONDS`
- `CONDUIT_CORE_MAX_FAILED_AUTH_ATTEMPTS` / `CONDUIT_CORE_AUTH_BAN_DURATION_MINUTES`
- `CONDUIT_CORE_ENFORCE_VKEY_RATE_LIMITS`, `CONDUIT_CORE_ENFORCE_VKEY_BUDGETS`, `CONDUIT_CORE_ENFORCE_VKEY_MODELS`

## IV. WebUI Security

The WebUI (admin dashboard) is protected by a similar suite of security measures.

### A. Key Security Features

1.  **Authentication**:
    *   Requires authentication using `CONDUIT_WEBUI_AUTH_KEY` (recommended) or fallback to `CONDUIT_MASTER_KEY`.
2.  **IP Filtering**:
    *   Environment-configurable whitelist/blacklist (`CONDUIT_IP_FILTERING_ENABLED`, `CONDUIT_IP_FILTER_MODE`, `CONDUIT_IP_FILTER_WHITELIST`, `CONDUIT_IP_FILTER_BLACKLIST`).
    *   Supports CIDR, private IP detection (`CONDUIT_IP_FILTER_ALLOW_PRIVATE`), and endpoint exclusions.
    *   Option to bypass filtering for admin UI paths (`CONDUIT_IP_FILTER_BYPASS_ADMIN_UI`).
3.  **Rate Limiting**:
    *   Per-IP request limiting (`CONDUIT_RATE_LIMITING_ENABLED`, `CONDUIT_RATE_LIMIT_MAX_REQUESTS`, `CONDUIT_RATE_LIMIT_WINDOW_SECONDS`).
    *   Sliding window algorithm, distributed support with Redis.
    *   Path exclusions for static assets, Blazor components, etc. (`CONDUIT_RATE_LIMIT_EXCLUDED_PATHS`).
4.  **Failed Login Protection**:
    *   Automatic IP banning after `CONDUIT_MAX_FAILED_ATTEMPTS`.
    *   Ban duration set by `CONDUIT_IP_BAN_DURATION_MINUTES`.
    *   Shared ban list via Redis.
5.  **Security Headers**:
    *   Comprehensive headers including X-Frame-Options, X-Content-Type-Options, X-XSS-Protection, Content-Security-Policy, Strict-Transport-Security, Referrer-Policy, Permissions-Policy. Configurable via `CONDUIT_SECURITY_HEADERS_*` variables.
6.  **Security Dashboard**:
    *   Located at `/security` in the WebUI.
    *   Provides real-time overview of security status, active filters, failed attempts, banned IPs, and current request IP classification.

### B. Implementation Details & Architecture Simplification
- The WebUI security architecture was simplified by consolidating multiple services (SecurityConfigurationService, IpAddressClassifier, FailedLoginTrackingService) and middleware (IpFilterMiddleware, RateLimitingMiddleware) into a unified `SecurityService` and `SecurityMiddleware`.
- Configuration is centralized in `SecurityOptions` class.
- **Documentation**:
    - `ConduitLLM.WebUI/docs/SECURITY-FEATURES.md` (Detailed features)
    - `ConduitLLM.WebUI/docs/SIMPLIFIED-SECURITY-ARCHITECTURE.md` (Overview of the refactored architecture)

### C. WebUI Specific Environment Variables (Primary - some overlap with general IP/Rate/Ban settings if shared prefix is used)
Many WebUI security settings use general prefixes like `CONDUIT_IP_FILTERING_ENABLED` if the intent is a global setting, or can be more specific if needed. The summaries often list them generally. Key ones specific or commonly highlighted for WebUI:
- `CONDUIT_WEBUI_AUTH_KEY`
- `CONDUIT_IP_FILTER_BYPASS_ADMIN_UI`
- `CONDUIT_RATE_LIMIT_EXCLUDED_PATHS` (often includes WebUI specific paths like `/_blazor`)
- Security Header variables like `CONDUIT_SECURITY_HEADERS_CSP`, `CONDUIT_SECURITY_HEADERS_X_FRAME_OPTIONS`.

*(Many IP Filtering, Rate Limiting, and Failed Login Protection variables listed in `SECURITY-ENHANCEMENTS-SUMMARY.md` apply here, often sharing names with Admin/Core API equivalents if a unified SecurityOptions pattern is fully adopted across services.)*

## V. Security Best Practices & Recommendations

- **HTTPS**: Always use HTTPS in production. Enable HSTS.
- **Strong Keys**: Use long, randomly generated keys for `CONDUIT_MASTER_KEY` and `CONDUIT_WEBUI_AUTH_KEY`.
- **Principle of Least Privilege**: Use restrictive IP filtering modes (whitelist) where possible.
- **Regular Monitoring**: Actively monitor the Security Dashboard (if WebUI is used) and logs for suspicious activity.
- **Key Rotation**: Periodically rotate API keys and master keys.
- **WAF/CDN**: For public-facing deployments, use a Web Application Firewall (e.g., Cloudflare) for DDoS protection, bot mitigation, and additional security layers.
- **Stay Updated**: Keep ConduitLLM and its dependencies updated to patch potential vulnerabilities.

This consolidated document aims to provide a clear and comprehensive understanding of the security posture of the ConduitLLM platform. For more granular details, refer to the specific `SECURITY-ARCHITECTURE.md` or `SECURITY-FEATURES.md` files within each service's `docs` directory.
