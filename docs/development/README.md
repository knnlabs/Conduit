# Development Documentation

This directory contains guides and best practices for developing with Conduit.

## Contents

- **[API Patterns Best Practices](./API-PATTERNS-BEST-PRACTICES.md)** - RESTful API design patterns and conventions
- **[SDK Migration Complete](./SDK-MIGRATION-COMPLETE.md)** - Documentation of completed SDK migration
- **[SDK Migration Guide](./sdk-migration-guide.md)** - Step-by-step migration instructions
- **[SDK Gaps](./sdk-gaps.md)** - Known limitations and missing features
- **[Next.js 15 Migration](./nextjs15-migration.md)** - WebUI framework upgrade guide

## Development Workflow

### Getting Started
1. Review the [API Patterns Best Practices](./API-PATTERNS-BEST-PRACTICES.md) before implementing new endpoints
2. Check [SDK Gaps](./sdk-gaps.md) for known limitations
3. Follow established patterns from existing code

### SDK Development
- TypeScript SDKs are in `/SDKs/Node/`
- Follow the patterns established in the migration guides
- Ensure backward compatibility for API changes

### Contributing
- Write clear, self-documenting code
- Update relevant documentation when making changes
- Follow the established naming conventions

## Related Documentation

- [Architecture Overview](../architecture-overview.md)
- [Clean Architecture Guide](../clean-architecture-guide.md)
- [API Reference](../api-reference/)