# SDK Type Safety Release Notes

## 🎯 Major Release: Full Type Safety Implementation

### Conduit Admin SDK v2.0.0 & Core SDK v1.0.0

**Release Date**: [TBD]  
**Epic**: [SDK Type Safety Improvements #349](https://github.com/knnlabs/Conduit/issues/349)

---

## 🌟 Overview

We're excited to announce the release of fully type-safe Node.js SDKs for Conduit! This major update represents a complete architectural overhaul, bringing industrial-grade type safety, improved developer experience, and significant performance improvements.

### What's Changed

**🎯 Complete Type Safety**: All `any` and `unknown` types replaced with OpenAPI-generated TypeScript interfaces  
**🚀 Native Fetch**: Removed Axios dependency, implemented with native fetch API  
**📦 Smaller Bundles**: ~40KB reduction in bundle sizes (Admin: 226KB → 179KB, Core: 170KB → 134KB)  
**⚡ Better Performance**: Optimized HTTP client with connection pooling and retry logic  
**🛡️ Enhanced Error Handling**: Type-safe error classes with specific error types and type guards  
**📚 Improved DX**: Full IDE autocomplete, inline documentation, and compile-time error checking

---

## 📋 Version Information

| SDK | Previous Version | New Version | Breaking Changes |
|-----|------------------|-------------|------------------|
| **Admin SDK** | v1.0.1 | **v2.0.0** | ⚠️ **Yes** - Major changes |
| **Core SDK** | v0.2.0 | **v1.0.0** | ⚠️ **Yes** - Major changes |

---

## ✨ New Features

### 🎯 Full Type Safety

Every API call now has complete type checking from request to response:

```typescript
// Before: Loose typing, runtime errors
const response = await client.virtualKeys.create({
  keyName: "test",
  maxBudget: "100" // String accepted, could cause issues
} as any);

// After: Strict typing, compile-time safety
const response = await client.virtualKeys.create({
  keyName: "test",
  maxBudget: 100,   // Must be number
  budgetDuration: "Daily"  // Enum value with autocomplete
});
// response is fully typed as CreateVirtualKeyResponse
```

### 🚀 Native Fetch Implementation

Replaced Axios with optimized native fetch:

```typescript
// Enhanced configuration with native fetch
const client = new ConduitCoreClient({
  apiKey: 'key',
  timeout: 30000,
  maxRetries: 3,
  retryDelay: [1000, 2000, 4000],
  onRequest: (config) => console.log('Sending:', config),
  onResponse: (response) => console.log('Received:', response)
});
```

### 🛡️ Type-Safe Error Handling

Comprehensive error types with built-in type guards:

```typescript
try {
  await client.chat.create(request);
} catch (error) {
  if (client.isRateLimitError(error)) {
    console.log(`Rate limited. Retry after ${error.retryAfter}s`);
  } else if (client.isAuthError(error)) {
    console.log(`Auth failed: ${error.code}`);
  } else if (error instanceof ValidationError) {
    console.log(`Validation: ${error.details}`);
  }
}
```

### 📊 Enhanced Request Options

New capabilities for advanced use cases:

```typescript
const response = await client.chat.create(request, {
  timeout: 60000,
  correlationId: 'custom-trace-id',
  signal: abortController.signal,
  headers: { 'Custom-Header': 'value' }
});
```

### 🔧 Advanced Configuration

More flexible client configuration:

```typescript
const client = new ConduitAdminClient({
  masterKey: 'key',
  adminApiUrl: 'https://admin.example.com',
  options: {
    retries: {
      maxRetries: 3,
      retryDelay: 1000,
      retryCondition: (error) => error.statusCode >= 500
    },
    signalR: {
      enabled: true,
      autoConnect: true,
      reconnectDelay: [1000, 2000, 5000]
    },
    cache: cacheProvider,
    logger: customLogger
  }
});
```

---

## ⚠️ Breaking Changes

### Major Removals

#### React Query Support Removed
All React Query hooks and utilities have been removed to focus on server-side usage:

```typescript
// ❌ REMOVED - No longer available
import { 
  useVirtualKeys,
  useChatCompletion,
  ConduitAdminProvider 
} from '@knn_labs/conduit-admin-client';

// ✅ MIGRATION - Use direct service calls
import { ConduitAdminClient } from '@knn_labs/conduit-admin-client';
const client = new ConduitAdminClient(config);
const keys = await client.virtualKeys.list();
```

#### Axios HTTP Client Removed
Complete migration from Axios to native fetch:

```typescript
// ❌ REMOVED - Axios configurations
const client = new ConduitCoreClient({
  apiKey: 'key',
  axios: {
    timeout: 5000,
    interceptors: { /* ... */ }
  }
});

// ✅ MIGRATION - Native fetch configuration
const client = new ConduitCoreClient({
  apiKey: 'key',
  timeout: 5000,
  onRequest: (config) => { /* custom logic */ },
  onResponse: (response) => { /* custom logic */ }
});
```

### Service Changes

#### Removed Services
- **DatabaseBackupService** (Admin SDK): Use Admin API directly
- **ConnectionService** (Core SDK): Functionality moved to main client
- **HealthService** (Core SDK): Built into main client as `getHealth()`

#### Service Method Changes

Most core service methods remain compatible, but with stricter typing:

```typescript
// Before: Loose types
const response = await client.chat.create(request as any);

// After: Strict types (may reveal existing type issues)
const response = await client.chat.create(request); // Compile-time validation
```

### Configuration Changes

#### Client Initialization
Updated configuration interfaces with enhanced options:

```typescript
// Core SDK - baseUrl → baseURL
const client = new ConduitCoreClient({
  apiKey: 'key',
  baseURL: 'https://api.example.com', // Note: 'baseURL' not 'baseUrl'
  maxRetries: 3, // Number, not string
  debug: true
});
```

#### Retry Configuration
Enhanced retry configuration options:

```typescript
// Before: Simple number
retries: 3

// After: Enhanced configuration
retries: {
  maxRetries: 3,
  retryDelay: 1000,
  retryCondition: (error) => error.statusCode >= 500
}
```

---

## 📈 Performance Improvements

### Bundle Size Reductions
- **Admin SDK**: 226KB → 179KB (-47KB, -21%)
- **Core SDK**: 170KB → 134KB (-36KB, -21%)

### HTTP Performance
- Native fetch with optimized connection pooling
- Reduced memory footprint
- Better tree-shaking for smaller production bundles

### Type System Performance
- Faster TypeScript compilation
- Better IDE responsiveness
- Reduced type checking overhead

---

## 🚀 Migration Guide

### Quick Migration Steps

1. **Update Dependencies**
   ```bash
   npm install @knn_labs/conduit-admin-client@^2.0.0
   npm install @knn_labs/conduit-core-client@^1.0.0
   ```

2. **Replace React Query Usage**
   - Convert hooks to direct service calls
   - Implement custom state management

3. **Update Client Configuration**
   - Review configuration options
   - Update property names (e.g., `baseUrl` → `baseURL`)

4. **Update Error Handling**
   - Use new type guards and error classes
   - Take advantage of enhanced error information

5. **Run Type Checking**
   ```bash
   npx tsc --noEmit
   ```

### Detailed Migration Resources
- 📚 [Complete Migration Guide](./SDK-TYPE-SAFETY-MIGRATION-GUIDE.md)
- 💥 [Breaking Changes Documentation](./SDK-TYPE-SAFETY-BREAKING-CHANGES.md)
- 📖 [Updated API Reference](./API-Reference.md)

---

## 🔄 Migration Timeline

### Recommended Approach

**Phase 1: Preparation (1-2 days)**
- Review migration documentation
- Update development environment
- Create migration branch

**Phase 2: Core Changes (2-3 days)**
- Update client initialization
- Replace React Query hooks
- Update error handling

**Phase 3: Testing & Validation (2-3 days)**
- Comprehensive testing
- Performance validation
- Team training

**Phase 4: Deployment (1 day)**
- Production deployment
- Monitoring for issues

### Migration Support
- GitHub Issues for specific problems
- Examples in SDK repositories
- Community support in discussions

---

## 🎁 Benefits Summary

### For Developers
- ✅ **Catch Errors Early**: Type checking prevents runtime errors
- ✅ **Better IDE Experience**: Full autocomplete and inline documentation
- ✅ **Faster Development**: Less debugging, more confidence
- ✅ **Self-Documenting Code**: Types serve as living documentation

### For Applications
- ✅ **Reliability**: Fewer runtime errors and edge cases
- ✅ **Performance**: Smaller bundles, faster loading
- ✅ **Maintainability**: Easier refactoring and updates
- ✅ **Security**: Better input validation and error handling

### For Teams
- ✅ **Productivity**: Less time spent debugging type issues
- ✅ **Code Quality**: Consistent patterns and better practices
- ✅ **Onboarding**: New team members can leverage type hints
- ✅ **Scalability**: Easier to maintain large codebases

---

## 🔮 Future Roadmap

### Short Term (Next 2-4 weeks)
- React Query wrapper package (optional add-on)
- Additional type-safe utilities and helpers
- Performance optimizations

### Medium Term (1-3 months)
- WebSocket support for real-time features
- Advanced caching strategies
- Enhanced debugging tools

### Long Term (3-6 months)
- GraphQL support
- Advanced middleware system
- Multi-provider request orchestration

---

## 🙏 Acknowledgments

Special thanks to:
- The development team for the comprehensive type safety implementation
- Early adopters who provided feedback during development
- The TypeScript and OpenAPI communities for excellent tooling

---

## 📞 Support & Resources

### Documentation
- [Migration Guide](./SDK-TYPE-SAFETY-MIGRATION-GUIDE.md)
- [Breaking Changes](./SDK-TYPE-SAFETY-BREAKING-CHANGES.md)
- [API Reference](./API-Reference.md)

### Community
- [GitHub Issues](https://github.com/knnlabs/Conduit/issues)
- [Discussions](https://github.com/knnlabs/Conduit/discussions)

### Examples
- [Admin SDK Examples](../SDKs/Node/Admin/examples/)
- [Core SDK Examples](../SDKs/Node/Core/examples/)

---

## 🏷️ Release Tags

When ready to release:
- **Admin SDK**: `@knn_labs/conduit-admin-client@2.0.0`
- **Core SDK**: `@knn_labs/conduit-core-client@1.0.0`

Both packages are published to [npm registry](https://www.npmjs.com/search?q=%40knn_labs%2Fconduit) with full TypeScript declarations and comprehensive documentation.

---

*This release represents a significant milestone in Conduit's evolution toward a more reliable, type-safe, and developer-friendly platform. We're excited to see what you build with these improved SDKs!*