# Documentation Style Guide

This guide establishes standards for creating and maintaining Conduit documentation.

## File Organization

### Directory Structure
```
docs/
├── README.md                    # Main index
├── getting-started/            # New user guides
├── api-reference/              # API documentation
├── architecture/               # System design docs
├── deployment/                 # Operations guides
├── development/                # Developer guides
├── model-pricing/              # Pricing information
├── runbooks/                   # Operational procedures
└── archive/                    # Deprecated content
```

### File Naming Conventions

1. **Always use kebab-case** for file names:
   - ✅ `getting-started.md`
   - ✅ `api-reference.md`
   - ❌ `Getting-Started.md`
   - ❌ `API_REFERENCE.md`

2. **Exceptions** for established conventions:
   - `README.md` (always uppercase)
   - `CHANGELOG.md` (always uppercase)
   - `LICENSE` (no extension)

3. **Descriptive names** that indicate content:
   - ✅ `virtual-key-management.md`
   - ❌ `vkeys.md`

## Document Structure

### Required Front Matter

Every document should start with:

```markdown
# Document Title

*Last Updated: YYYY-MM-DD*

Brief description of what this document covers.

## Table of Contents
- [Section 1](#section-1)
- [Section 2](#section-2)
```

### Section Headers

Use hierarchical headers appropriately:
- `#` Document title (one per document)
- `##` Major sections
- `###` Subsections
- `####` Minor points (use sparingly)

### Content Guidelines

1. **Be concise**: Get to the point quickly
2. **Use examples**: Show, don't just tell
3. **Be practical**: Focus on real-world usage
4. **Stay current**: Update dates when modifying

## Writing Style

### Voice and Tone
- **Active voice**: "Configure the server" not "The server should be configured"
- **Direct**: "You must" not "It is required that you"
- **Professional but approachable**

### Technical Writing
- **Define acronyms** on first use: "Large Language Model (LLM)"
- **Use code blocks** for commands, configuration, and examples
- **Specify languages** in code blocks: ` ```bash `, ` ```typescript `
- **Include comments** in code examples

### Formatting

#### Code Examples
```typescript
// Good: Includes context and comments
const client = new ConduitClient({
  apiKey: 'your-api-key',    // Virtual key from admin panel
  baseURL: 'https://api.conduit.ai'
});
```

#### Commands
```bash
# Good: Shows full command with options
docker run -p 5000:5000 -e DATABASE_URL=postgres://... ghcr.io/knnlabs/conduit:latest

# Bad: Incomplete example
docker run conduit
```

#### Lists
- Use bullet points for unordered lists
- Use numbers for sequential steps
- Keep list items parallel in structure

## Cross-References

### Internal Links
- Use relative paths: `[API Reference](./api-reference.md)`
- Link to sections: `[Authentication](./api-reference.md#authentication)`
- Verify links work before committing

### External Links
- Use descriptive text: `[OpenAI Documentation](https://platform.openai.com/docs)`
- Not: `[Click here](https://...)`

## API Documentation

### Endpoint Documentation Format
```markdown
### Endpoint Name

Brief description of what the endpoint does.

**Endpoint**: `METHOD /path/to/endpoint`  
**Authentication**: Required/Optional  
**Rate Limit**: X requests per minute  

#### Request

```json
{
  "field": "description of field"
}
```

#### Response

```json
{
  "field": "description of field"  
}
```

#### Example

```bash
curl -X POST https://api.conduit.ai/v1/endpoint \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"field": "value"}'
```
```

## Maintenance

### When to Archive
- Document is no longer relevant
- Feature has been removed
- Information is outdated and won't be updated

### Archive Process
1. Move file to `archive/` directory
2. Add deprecation notice at top:
   ```markdown
   > **⚠️ DEPRECATED**: This document is archived as of YYYY-MM-DD.
   > See [New Document](../path/to/new.md) for current information.
   ```
3. Update any links pointing to the archived document

### Regular Reviews
- Review documentation quarterly
- Update "Last Updated" dates
- Check for broken links
- Archive outdated content

## Common Mistakes to Avoid

1. **Don't use internal terminology** without explanation
   - ❌ "Phase 2 features"
   - ✅ "Advanced pricing models (prompt caching, search units)"

2. **Don't duplicate content**
   - Create one authoritative source
   - Link to it from other documents

3. **Don't mix concerns**
   - API reference vs. conceptual guides
   - User guides vs. developer documentation

4. **Don't forget examples**
   - Every feature should have at least one example
   - Examples should be complete and runnable

## Templates

### New Feature Documentation
```markdown
# Feature Name

*Last Updated: YYYY-MM-DD*

Brief description of the feature and its purpose.

## Overview

What problem does this solve? Who is it for?

## Configuration

How to enable and configure the feature.

## Usage

### Basic Example
[Simple use case]

### Advanced Example
[Complex use case]

## Best Practices

- Recommendation 1
- Recommendation 2

## Troubleshooting

Common issues and solutions.

## Related Documentation

- [Link to related doc]
```

---

*This style guide is a living document. Propose changes via pull request.*