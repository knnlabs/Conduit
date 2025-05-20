# DTO Standardization Plan

## Completed Tasks

### WebUI Project Fixes
- Created WebUI.DTOs.ModelProviderMapping wrapper class to resolve ambiguous references
- Fixed ModelProviderMappingServiceAdapter to handle different DTO formats
- Implemented missing methods in AdminApiClient:
  - Model provider mapping methods
  - Router configuration methods
  - Database backup methods
- Fixed DatabaseBackupServiceAdapter to handle new API structure
- Fixed RouterServiceAdapter with proper conversions between FallbackConfiguration formats

### Admin Project Fixes
- Added missing extension methods:
  - Created CoreExtensions.cs to add AddCoreServices
  - Created ConfigurationExtensions.cs to add AddConfigurationServices
- Created DatabaseExtensions.cs to provide GetDatabase() method for IConfigurationDbContext
- Created ModelProviderMappingRepositoryExtensions.cs to handle repository method naming differences
- Created FallbackConfigurationRepositoryExtensions.cs to fix FallbackConfiguration conversions
- Created ProviderHealthExtensions.cs to handle UpdateProviderHealthConfigurationDto issues
- Fixed property reference issues in AdminRoutingService, AdminSystemInfoService, and AdminProviderHealthService
- Used namespace aliases to resolve ambiguous extension method calls

## Pending Tasks

### Tests Project
- Fix test cases to work with the new DTO structure
- Update mock setups to use correct DTO types
- Fix any ambiguous references similar to WebUI and Admin projects

## Implementation Patterns

### Type Conversion
- Create wrapper classes that inherit from or compose other DTO classes
- Implement explicit conversion methods (ToEntity(), ToDto())
- Use namespace aliases to disambiguate references 
- Use reflection for safely accessing properties across different types

### Extension Methods
- Create extension methods to add missing functionality to interfaces
- Use extension methods to bridge between different method names with similar functionality
- Use strongly-typed extension methods to avoid ambiguous method calls

### Error Handling
- Implement proper logging for API communication errors
- Use appropriate fallback mechanisms
- Handle HTTP-specific error conditions

### Service Adapters
- Implement interfaces while delegating to either repositories or API clients
- Use proper type conversion between different DTO formats
- Maintain backward compatibility where needed