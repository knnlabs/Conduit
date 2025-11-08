# Development Documentation

This directory contains technical guides and best practices for developing with Conduit.

## Contents

- **[API Patterns Best Practices](./API-PATTERNS-BEST-PRACTICES.md)** - RESTful API design patterns and conventions for Next.js/SDK integration
- **[LLM Client Factory Guide](./llm-client-factory-guide.md)** - Critical guide on which factory to use when developing LLM providers
- **[Provider API Research](./provider-api-research.md)** - Comprehensive research on model discovery and metadata APIs for all providers
- **[SDK Gaps](./sdk-gaps.md)** - Known SDK limitations and genuinely missing features

## Development Workflow

### Getting Started
1. **Start development environment**: Always use `./scripts/start-dev.sh` (see [CLAUDE.md](../../CLAUDE.md#development-workflow---critical))
2. **Review API patterns**: Check [API Patterns Best Practices](./API-PATTERNS-BEST-PRACTICES.md) before implementing new endpoints
3. **Check SDK coverage**: Review [SDK Gaps](./sdk-gaps.md) for known limitations
4. **Follow established patterns**: Reference existing code in the codebase

### Adding New LLM Providers
1. Review [LLM Client Factory Guide](./llm-client-factory-guide.md) to understand factory architecture
2. Check [Provider API Research](./provider-api-research.md) for provider-specific API details
3. Update `DatabaseAwareLLMClientFactory` with your new provider client
4. Add fallback models and patterns to `ProviderFallbackModels`

### SDK Development
- TypeScript SDKs are in `/SDKs/Node/` (Admin, Core, Common)
- Build with `npm run build` in each SDK directory
- Follow patterns from [API Patterns Best Practices](./API-PATTERNS-BEST-PRACTICES.md)
- Ensure backward compatibility for API changes

### Contributing
- Write clear, self-documenting code with XML documentation
- **Always verify builds**: Run `npm run lint` and `npm run type-check` for WebUI changes
- Update relevant documentation when making changes
- Follow naming conventions from [CLAUDE.md](../../CLAUDE.md#code-style-guidelines)

## Related Documentation

- **[CLAUDE.md](../../CLAUDE.md)** - Primary development workflow, Docker setup, build verification
- **[Architecture Overview](../architecture/)** - System design and patterns
- **[Core API Guide](../api-guides/core-api-guide.md)** - Complete API documentation and SDK overview
- **[Archived Development Docs](../archive/)** - Historical guides (CodeQL lessons learned, etc.)