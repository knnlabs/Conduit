# Contributing to Conduit Admin Client

Thank you for your interest in contributing to the Conduit Admin Client library!

## Development Setup

1. Clone the repository:
```bash
git clone https://github.com/conduit/admin-client.git
cd admin-client
```

2. Install dependencies:
```bash
npm install
```

3. Build the project:
```bash
npm run build
```

4. Run tests:
```bash
npm test
```

## Development Workflow

### Running in Development Mode

```bash
npm run dev
```

This will watch for file changes and rebuild automatically.

### Testing

```bash
# Run all tests
npm test

# Run tests in watch mode
npm run test:watch

# Run tests with coverage
npm run test:coverage
```

### Linting and Formatting

```bash
# Run ESLint
npm run lint

# Format code with Prettier
npm run format
```

## Project Structure

```
src/
├── client/          # Main client and base classes
├── services/        # Service implementations
├── models/          # TypeScript interfaces and types
├── utils/           # Utility functions and helpers
└── constants.ts     # API endpoints and constants
```

## Adding New Features

### 1. Adding a New Service

1. Create the model types in `src/models/yourFeature.ts`
2. Create the service in `src/services/YourFeatureService.ts`
3. Add the service to `ConduitAdminClient` in `src/client/ConduitAdminClient.ts`
4. Export types and service in `src/index.ts`
5. Add tests in `tests/unit/services/YourFeatureService.test.ts`
6. Update documentation

### 2. Adding Stub Functions

When adding functionality that requires Admin API implementation:

1. Throw a `NotImplementedError` with a descriptive message
2. Document the stub in `docs/STUBS.md`
3. Include suggested API endpoint structure

Example:
```typescript
async bulkCreate(items: CreateItemDto[]): Promise<BulkCreateResponse> {
  throw new NotImplementedError(
    'bulkCreate requires Admin API endpoint implementation. ' +
    'Consider implementing POST /api/items/bulk'
  );
}
```

### 3. Adding New Error Types

1. Add the error class in `src/utils/errors.ts`
2. Export it in `src/index.ts`
3. Document usage in API documentation

## Testing Guidelines

### Unit Tests

- Test each service method
- Mock external dependencies
- Test error scenarios
- Achieve high code coverage

Example test:
```typescript
describe('VirtualKeyService', () => {
  it('should create a virtual key', async () => {
    const service = new VirtualKeyService(mockConfig);
    const mockResponse = { virtualKey: 'ck_test', keyInfo: {...} };
    
    jest.spyOn(service as any, 'post').mockResolvedValue(mockResponse);
    
    const result = await service.create({ keyName: 'Test' });
    expect(result).toEqual(mockResponse);
  });
});
```

### Integration Tests

For integration tests, create a separate test file that uses a real Conduit instance:

```typescript
// tests/integration/virtualKeys.integration.test.ts
describe('Virtual Keys Integration', () => {
  const client = new ConduitAdminClient({
    masterKey: process.env.TEST_MASTER_KEY!,
    adminApiUrl: process.env.TEST_ADMIN_API_URL!,
  });

  it('should create and retrieve a key', async () => {
    const created = await client.virtualKeys.create({...});
    const retrieved = await client.virtualKeys.get(created.keyInfo.id);
    expect(retrieved.id).toBe(created.keyInfo.id);
  });
});
```

## Code Style

- Use TypeScript for all code
- Follow existing patterns in the codebase
- Use meaningful variable and function names
- Add JSDoc comments for public APIs
- Keep functions small and focused

## Documentation

When adding new features:

1. Update `README.md` with usage examples
2. Add detailed API documentation in `docs/API.md`
3. Update examples in `examples/`
4. Document any breaking changes

## Pull Request Process

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass: `npm test`
6. Lint your code: `npm run lint`
7. Commit with a descriptive message
8. Push to your fork
9. Create a Pull Request

### PR Checklist

- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] Lint passes
- [ ] All tests pass
- [ ] Breaking changes documented
- [ ] Stub functions documented (if applicable)

## Release Process

1. Update version in `package.json`
2. Update `CHANGELOG.md`
3. Create a git tag: `git tag v1.0.0`
4. Push tag: `git push origin v1.0.0`
5. NPM publish will be handled by CI

## Questions?

If you have questions about contributing, please:

1. Check existing issues and PRs
2. Create a new issue for discussion
3. Join our Discord community

Thank you for contributing!