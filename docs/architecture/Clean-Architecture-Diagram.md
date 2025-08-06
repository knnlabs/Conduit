# Clean Architecture Diagram

This document provides a visual representation of the Conduit LLM platform's clean architecture.

## System Architecture Overview

```
┌───────────────────────────────────────┐
│                                       │
│             End Users                 │
│                                       │
└───────────────┬───────────────────────┘
                │
                ▼
┌───────────────────────────────────────┐
│                                       │
│           Client Applications         │
│                                       │
└───────────────┬───────────────────────┘
                │
                ▼
┌───────────────────────────────────────┐
│                                       │
│        OpenAI-Compatible API          │
│        (ConduitLLM.Http)              │
│                                       │
└───────────────┬───────────────────────┘
                │
                ▼
┌───────────────┴───────────────────────┐
│                                       │
│            Core Logic                 │
│          (ConduitLLM.Core)            │
│                                       │
└───────────────┬───────────────────────┘
                │
                ▼
┌───────────────┴───────────────────────┐
│                                       │
│        Provider Implementations       │
│        (ConduitLLM.Providers)         │
│                                       │
└───────────────────────────────────────┘
```

## Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│  ┌─────────────────┐       ┌──────────────────┐                 │
│  │                 │       │                  │                 │
│  │  Admin Web UI   │       │  LLM HTTP API    │                 │
│  │  (Presentation) │       │  (Presentation)  │                 │
│  │                 │       │                  │                 │
│  └────────┬────────┘       └────────┬─────────┘                 │
│           │                         │                           │
│           ▼                         ▼                           │
│  ┌─────────────────┐       ┌──────────────────┐                 │
│  │                 │       │                  │                 │
│  │   Admin API     │       │    LLM Router    │                 │
│  │ (Application)   │       │  (Application)   │                 │
│  │                 │       │                  │                 │
│  └────────┬────────┘       └────────┬─────────┘                 │
│           │                         │                           │
│           │                         │                           │
│           ▼                         ▼                           │
│  ┌─────────────────────────────────────────────┐                │
│  │                                             │                │
│  │              Domain Layer                   │                │
│  │        (Entities, Interfaces, DTOs)         │                │
│  │                                             │                │
│  └────────────────────┬────────────────────────┘                │
│                       │                                         │
│                       ▼                                         │
│  ┌─────────────────────────────────────────────┐                │
│  │                                             │                │
│  │            Infrastructure Layer             │                │
│  │   (Repositories, External APIs, Database)   │                │
│  │                                             │                │
│  └─────────────────────────────────────────────┘                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Key Components and Dependencies

```
┌─────────────────────┐     ┌─────────────────────┐     ┌─────────────────────┐
│                     │     │                     │     │                     │
│    WebUI            │     │    Admin API        │     │    LLM HTTP API     │
│    (Blazor)         │     │    (ASP.NET)        │     │    (ASP.NET)        │
│                     │     │                     │     │                     │
└─────────┬───────────┘     └─────────┬───────────┘     └─────────┬───────────┘
          │                           │                           │
          │                           │                           │
          ▼                           │                           ▼
┌─────────────────────┐     │         │           ┌─────────────────────┐
│                     │     │         │           │                     │
│  AdminApiClient     │     │         │           │  LLM Router        │
│  (HTTP Client)      │◄────┘         └──────────►│  (Traffic Control) │
│                     │                           │                     │
└─────────────────────┘                           └─────────┬───────────┘
                                                            │
                                                            │
                                                            ▼
                                               ┌─────────────────────┐
                                               │                     │
                                               │  LLM Clients        │
                                               │  (Provider APIs)    │
                                               │                     │
                                               └─────────┬───────────┘
                                                         │
                                                         │
                                                         ▼
                                               ┌─────────────────────┐
                                               │                     │
                                               │  External LLM       │
                                               │  Provider APIs      │
                                               │                     │
                                               └─────────────────────┘
```

## Database Access Pattern

```
Current Architecture:
┌────────────┐      ┌────────────┐      ┌────────────┐      ┌────────────┐
│            │      │ Admin API  │      │            │      │            │
│   WebUI    │─────►│   Client   │─────►│ Admin API  │─────►│  Database  │
│            │      │            │      │            │      │            │
└────────────┘      └────────────┘      └────────────┘      └────────────┘
```

The WebUI follows clean architecture principles by:
- Using the Admin API Client SDK for all database operations
- No direct database access from the WebUI
- All business logic handled by the Admin API layer
- Type-safe communication through generated TypeScript clients

**Note**: Configuration Adapters still exist in the HTTP/Admin APIs for internal layer mapping between Core interfaces and Configuration services, but these are architectural adapters within the backend, not the WebUI access pattern that was migrated.

## Container Deployment Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Docker Host                             │
│                                                                 │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐          │
│  │             │    │             │    │             │          │
│  │   WebUI     │    │  Admin API  │    │  HTTP API   │          │
│  │  Container  │    │  Container  │    │  Container  │          │
│  │             │    │             │    │             │          │
│  └──────┬──────┘    └──────┬──────┘    └──────┬──────┘          │
│         │                  │                  │                 │
│         └──────────┬───────┴──────────┬───────┘                 │
│                    │                  │                         │
│           ┌────────┴────────┐ ┌───────┴────────┐                │
│           │                 │ │                │                │
│           │   PostgreSQL    │ │     Redis      │                │
│           │   Container     │ │   Container    │                │
│           │                 │ │                │                │
│           └─────────────────┘ └────────────────┘                │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```