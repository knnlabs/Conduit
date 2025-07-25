# Entity Naming Conventions

This document provides naming standards and guidelines for entity classes in the Conduit codebase, particularly focusing on provider credential entities.

## Purpose

These guidelines ensure consistency across the codebase and help prepare for future refactoring phases while maintaining backwards compatibility.

## Naming Standards

### 1. URL Properties

**Preferred**: `BaseUrl`
**Avoid**: `ApiBase`, `ApiUrl`, `Url`

```csharp
// ✅ Correct
public string? BaseUrl { get; set; }

// ❌ Avoid
public string? ApiBase { get; set; }
public string? ApiUrl { get; set; }
```

**Rationale**: `BaseUrl` is descriptive and consistent across HTTP clients and API documentation.

### 2. Organization/Project Identifiers

**Preferred**: `Organization`
**Avoid**: `OrganizationId`, `OrgId`, `ProjectId`

```csharp
// ✅ Correct
public string? Organization { get; set; }

// ❌ Avoid
public string? OrganizationId { get; set; }
public string? OrgId { get; set; }
```

**Rationale**: Different providers use different terms (OpenAI uses "organization", Google uses "project"). The generic `Organization` field can accommodate both.

### 3. Provider Identification

**Preferred**: `ProviderType` enum
**Deprecated**: `ProviderName` string

```csharp
// ✅ Preferred - Type-safe and prevents typos
public ProviderType ProviderType { get; set; }

// ⚠️ Deprecated - String-based, error-prone
[Obsolete("Use ProviderType enum instead")]
public string ProviderName { get; set; }
```

**Rationale**: Enums provide type safety, prevent typos, enable IntelliSense, and make refactoring easier.

## Migration Path

### Phase 1 (Current - No Breaking Changes)
- Mark deprecated fields with `[Obsolete]` attributes
- Continue supporting both old and new fields
- Update documentation to reflect preferred approaches
- Use preferred naming in all new code

### Phase 2 (Next Release - Minor Breaking Changes)
- Remove unused fields
- Update DTOs to use preferred naming
- Migrate internal code to use new patterns

### Phase 3 (Major Version - Breaking Changes)
- Remove deprecated database columns
- Enforce enum usage everywhere
- Complete architectural cleanup

## Field-Specific Guidelines

### API Keys
- **Current**: Use `ProviderKeyCredentials` collection
- **Deprecated**: `ProviderCredential.ApiKey` (single key)
- **Migration**: Move from single key to multi-key architecture

### API Versions
- **Current**: Key-specific versions in `ProviderKeyCredential.ApiVersion`
- **Deprecated**: Provider-level `ProviderCredential.ApiVersion`
- **Rationale**: Different keys may need different API versions

### Account Grouping
- **Field**: `ProviderAccountGroup` (0-32)
- **Purpose**: Groups keys by external provider account for intelligent failover
- **Usage**: Keys with same group share quota limits and billing

## Examples

### Correct Entity Design (New Code)

```csharp
public class MyNewEntity
{
    public ProviderType ProviderType { get; set; }
    public string? BaseUrl { get; set; }
    public string? Organization { get; set; }
    // Use ProviderKeyCredentials collection for multi-key support
}
```

### Legacy Support (Maintaining Compatibility)

```csharp
public class ExistingEntity
{
    public ProviderType ProviderType { get; set; }
    
    [Obsolete("Use ProviderType enum instead")]
    public string ProviderName { get; set; }
    
    public string? BaseUrl { get; set; }
    
    [Obsolete("Use ProviderKeyCredential.ApiVersion for key-specific versions")]
    public string? ApiVersion { get; set; }
}
```

## Best Practices

1. **Always use the preferred naming in new code**
2. **Add `[Obsolete]` attributes with clear migration messages**
3. **Document the purpose of complex fields** (like `ProviderAccountGroup`)
4. **Prefer enums over strings** for fixed value sets
5. **Use nullable types** for optional fields
6. **Include XML documentation** for all public properties

## Related Documentation

- [XML Documentation Standards](../claude/xml-documentation-standards.md)
- [Entity Cleanup Epic #619](https://github.com/knnlabs/Conduit/issues/619)
- [Database Migration Guide](../claude/database-migration-guide.md)