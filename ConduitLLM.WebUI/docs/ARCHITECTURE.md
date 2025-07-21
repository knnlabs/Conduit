# Conduit WebUI Architecture

## Overview

The Conduit WebUI has been migrated from a proxy-based architecture to direct SDK usage, significantly simplifying the codebase and improving performance.

## Architecture Evolution

### Previous Architecture (Proxy-Based)
```mermaid
graph LR
    A[Browser] -->|React Query| B[WebUI Hooks]
    B -->|HTTP| C[Next.js API Routes]
    C -->|SDK Client| D[Conduit APIs]
    
    subgraph "Client-Side"
        A
        B
    end
    
    subgraph "Server-Side"
        C
    end
    
    subgraph "External"
        D[Core API<br/>Admin API]
    end
```

### Current Architecture (Direct SDK)
```mermaid
graph LR
    A[Browser] -->|SDK React Query| B[Conduit APIs]
    
    subgraph "Client-Side"
        C[Core SDK Hooks]
        D[Admin SDK Hooks]
        E[Virtual Key]
    end
    
    subgraph "Server-Side (Auth Only)"
        F[Auth Endpoints]
        G[Virtual Key Management]
    end
    
    A --> C
    A --> D
    C -->|Virtual Key| B
    D -->|Master Key| B
    F --> G
    G -->|Creates| E
```

## Component Architecture

```mermaid
graph TB
    A[App Layout] --> B[ConduitProviders]
    B --> C[QueryProvider]
    B --> D[ConduitProvider<br/>Core SDK]
    B --> E[ConduitAdminProvider<br/>Admin SDK]
    
    D --> F[Core Operations<br/>- Chat<br/>- Images<br/>- Video<br/>- Audio]
    E --> G[Admin Operations<br/>- Providers<br/>- Virtual Keys<br/>- Settings]
    
    C --> H[React Query<br/>- Caching<br/>- Mutations<br/>- Optimistic Updates]
    
    I[AuthProvider] --> J[Session Management]
    J --> K[Virtual Key Storage]
    K --> D
```

## Authentication Flow

```mermaid
sequenceDiagram
    participant U as User
    participant B as Browser
    participant A as Auth API
    participant S as SDK
    participant C as Conduit API
    
    U->>B: Enter WebUI Auth Key
    B->>A: POST /api/auth/validate
    A->>A: Validate CONDUIT_WEBUI_AUTH_KEY
    A->>C: Check/Create Virtual Key
    C->>A: Return Virtual Key
    A->>B: Session + Virtual Key
    B->>B: Store in Auth Store
    B->>S: Initialize SDK with Virtual Key
    S->>C: Direct API Calls
    C->>S: API Responses
    S->>B: Update UI
```

## Data Flow

```mermaid
graph LR
    A[Component] --> B{Hook Type}
    B -->|Query| C[useQuery Hook]
    B -->|Mutation| D[useMutation Hook]
    
    C --> E[SDK Query]
    D --> F[SDK Mutation]
    
    E --> G[Conduit API]
    F --> G
    
    G --> H[Response]
    H --> I[React Query Cache]
    I --> A
    
    J[Optimistic Updates] --> I
    K[Cache Invalidation] --> I
```

## Key Components

### 1. SDK Providers
Located in `/lib/providers/ConduitProviders.tsx`
- Wraps the application with SDK contexts
- Provides virtual key to Core SDK
- Provides master key to Admin SDK (server-side)

### 2. Authentication
- `/stores/useAuthStore.ts` - Manages auth state and virtual key
- `/lib/auth/` - Authentication utilities and validation
- `/app/api/auth/` - Minimal auth endpoints

### 3. SDK Hooks
- Core SDK: Chat, Images, Video, Audio operations
- Admin SDK: Providers, Virtual Keys, Model Mappings

### 4. Real-time Updates
- SignalR integration for live updates
- Navigation state, generation progress
- Virtual key spend tracking

## Security Architecture

```mermaid
graph TB
    A[WebUI Auth Key<br/>Server-Only] --> B[Admin Login]
    B --> C[Create/Get Virtual Key]
    C --> D[Virtual Key<br/>Client-Side]
    
    D --> E[Core SDK Operations]
    E --> F[Rate Limited<br/>100 req/min]
    
    G[Master Key<br/>Server-Only] --> H[Admin SDK Operations]
    
    I[Security Headers] --> J[CSP<br/>X-Frame-Options<br/>X-Content-Type-Options]
    
    K[Session Management] --> L[24hr Expiry<br/>Auto Refresh<br/>Secure Cookies]
```

## Deployment Architecture

```mermaid
graph TB
    A[Docker Compose] --> B[WebUI Container<br/>Port 3000]
    A --> C[Core API Container<br/>Port 5000]
    A --> D[Admin API Container<br/>Port 5002]
    A --> E[Redis<br/>Session Storage]
    
    B --> F[Next.js Server]
    F --> G[Static Assets]
    F --> H[API Routes<br/>Auth Only]
    
    I[Browser] --> B
    I --> C
    I --> D
    
    B -.->|Internal| C
    B -.->|Internal| D
```

## Benefits of New Architecture

1. **Performance**
   - Eliminated proxy layer reduces latency
   - Direct API calls from browser
   - Better caching with React Query

2. **Simplicity**
   - 80% reduction in API route code
   - Direct SDK usage as designed
   - Less code to maintain

3. **Developer Experience**
   - Better TypeScript support
   - Easier debugging
   - Standard React Query patterns

4. **Security**
   - Virtual keys scoped for WebUI
   - Rate limiting per key
   - Automatic key management