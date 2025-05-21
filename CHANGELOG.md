# Changelog

## [Unreleased]

### Changed
- Migrated GlobalSettingService to always use the Admin API adapter implementation, regardless of CONDUIT_USE_ADMIN_API setting
- Migrated ProviderCredentialService to always use the Admin API adapter implementation 
- Migrated ModelCostService to always use the Admin API adapter implementation
- Migrated VirtualKeyService to always use the Admin API adapter implementation
- Noted that RequestLogService was already migrated to always use the Admin API adapter implementation
- Continuing migration plan to fully transition from direct database access to Admin API

### Deprecated
- Direct database access mode continues to be deprecated and will be removed after October 2025