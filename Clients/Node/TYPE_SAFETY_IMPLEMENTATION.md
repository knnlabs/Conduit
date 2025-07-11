# Type Safety Implementation Strategy

## Overview
This document outlines the strategy for replacing all `any` and `unknown` types in the Conduit Node.js SDKs with proper TypeScript types generated from OpenAPI specifications.

## Current State
- **Core SDK**: ~50 occurrences of `any`/`unknown`
- **Admin SDK**: ~130 occurrences of `any`/`unknown`
- **Generated Types**: Available in `src/generated/` directories

## Implementation Phases

### Phase 1: Core Type Infrastructure
1. **Import Generated Types**
   - Add imports from `./generated/core-api` and `./generated/admin-api`
   - Create type aliases for commonly used types
   - Export generated types from SDK index files

2. **Base Client Refactoring**
   - Replace `unknown` in request/response methods with generics
   - Use generated request/response types
   - Example:
   ```typescript
   // Before
   protected async post<T>(url: string, data?: unknown): Promise<T>
   
   // After
   protected async post<TRequest, TResponse>(
     url: string, 
     data?: TRequest
   ): Promise<TResponse>
   ```

### Phase 2: Service Layer Type Safety
1. **Chat Service (Core SDK)**
   ```typescript
   // Before
   async createCompletion(request: unknown): Promise<any>
   
   // After
   import { components } from '../generated/core-api';
   async createCompletion(
     request: components['schemas']['ChatCompletionRequest']
   ): Promise<components['schemas']['ChatCompletionResponse']>
   ```

2. **Virtual Key Service (Admin SDK)**
   ```typescript
   // Before
   async create(data: any): Promise<any>
   
   // After
   import { components } from '../generated/admin-api';
   async create(
     data: components['schemas']['CreateVirtualKeyRequest']
   ): Promise<components['schemas']['CreateVirtualKeyResponse']>
   ```

### Phase 3: Model Refinement
1. **Replace Metadata Fields**
   ```typescript
   // Before
   metadata?: Record<string, any>
   
   // After
   metadata?: Record<string, string | number | boolean | null>
   ```

2. **SignalR Event Types**
   ```typescript
   // Before
   on(event: string, callback: (...args: any[]) => void)
   
   // After
   on<TEvent extends keyof SignalREvents>(
     event: TEvent,
     callback: (data: SignalREvents[TEvent]) => void
   )
   ```

### Phase 4: Error Handling
1. **Type Error Responses**
   ```typescript
   interface ConduitError {
     code: string;
     message: string;
     details?: Record<string, unknown>;
     statusCode: number;
   }
   ```

2. **Type Guards**
   ```typescript
   function isConduitError(error: unknown): error is ConduitError {
     return (
       typeof error === 'object' &&
       error !== null &&
       'code' in error &&
       'message' in error
     );
   }
   ```

## Breaking Changes
Since there are no consumers yet, we can make breaking changes:

1. **Strict Response Types**: All service methods will return typed responses
2. **No Implicit Any**: Remove all implicit any types
3. **Required Properties**: Make all required fields non-optional
4. **Enum Types**: Use string literal unions for known values

## Type Generation Workflow
1. **Development**: Use `npm run generate:from-files` with existing OpenAPI specs
2. **CI/CD**: Generate types during build process
3. **Docker**: Use `docker-compose -f docker-compose.codegen.yml up` for live generation

## Benefits
- **IntelliSense**: Full autocomplete in IDEs
- **Type Checking**: Catch errors at compile time
- **Documentation**: Types serve as inline documentation
- **Refactoring**: Safe refactoring with TypeScript
- **API Contract**: Clear contract between client and server

## Migration Examples

### Example 1: Virtual Key Creation
```typescript
// Before
const result = await adminClient.virtualKeys.create({
  keyName: "test", // Could pass wrong field names
  maxBudget: "100" // Could pass wrong types
} as any);

// After
const result = await adminClient.virtualKeys.create({
  keyName: "test",
  maxBudget: 100, // Type error if string
  budgetDuration: "Daily" // Autocomplete shows valid options
}); // result is typed as CreateVirtualKeyResponse
```

### Example 2: Chat Completion
```typescript
// Before
const response = await coreClient.chat.complete({
  model: "gpt-4",
  messages: [{role: "usr", content: "Hello"}] // Typo not caught
});

// After  
const response = await coreClient.chat.complete({
  model: "gpt-4",
  messages: [{
    role: "user", // Autocomplete prevents typos
    content: "Hello"
  }]
}); // response.choices[0].message is fully typed
```

## Next Steps
1. Run `npm run generate:from-files` in scripts directory
2. Start replacing types in Base clients
3. Update service methods one by one
4. Add type tests to ensure correctness
5. Update all examples with typed usage