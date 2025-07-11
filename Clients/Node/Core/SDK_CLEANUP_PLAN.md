# SDK Cleanup Plan

## Objective
Restore SDKs to a working state by removing runtime validation and unnecessary complexity while keeping useful features.

## What to REMOVE
1. **All Zod validation schemas** (`/validation/schemas.ts`)
2. **ServiceBase class** - services will extend client directly
3. **Error Recovery Manager** and all strategy classes
4. **ValidationOptions and related types**
5. **All validation-specific tests**
6. **validateResponse function and all calls to it**

## What to KEEP
1. **Circuit Breaker** - Simple, useful for preventing cascading failures
2. **ResponseParser** - Solves real response type handling
3. **Basic retry logic** - Simplified, no strategy pattern
4. **Error types** - AuthError, RateLimitError, NetworkError, etc.
5. **Streaming support** - createWebStream utilities
6. **ExtendedRequestInit type** - For responseType option

## Implementation Steps

### Step 1: Remove Validation Layer
- Delete `/validation/schemas.ts`
- Remove all imports of validation schemas
- Remove all `validateResponse` calls
- Delete ServiceBase class

### Step 2: Simplify Services
- Make services extend FetchBasedClient directly
- Remove validation from all service methods
- Keep typed method signatures

Example:
```typescript
export class FetchChatService extends FetchBasedClient {
  async create(request: ChatCompletionRequest): Promise<ChatCompletionResponse> {
    return this.post('/v1/chat/completions', request);
  }
}
```

### Step 3: Simplify Error Recovery
- Remove ErrorRecoveryManager
- Remove all strategy classes
- Keep simple retry logic in FetchBasedClient
- Keep circuit breaker as-is

### Step 4: Fix Tests
- Remove validation-specific tests
- Update remaining tests to match simplified implementation
- Ensure all tests pass

### Step 5: Verify Both SDKs
- Run `npm run build` in Core SDK
- Run `npm run build` in Admin SDK
- Run `npm test` in both SDKs
- Ensure no TypeScript errors

## What NOT to Do
- Don't add new features
- Don't create new abstractions
- Don't refactor beyond what's necessary
- Don't try to make it "perfect"

## Success Criteria
✅ Both SDKs build without errors
✅ All tests pass
✅ No runtime validation code remains
✅ Circuit breaker still works
✅ Retry logic still works
✅ Services are simple and extend client directly

## Rollback Plan
If anything goes wrong:
1. All changes in ONE commit
2. Easy to `git reset --hard`
3. Validation code saved in separate file (not deleted)