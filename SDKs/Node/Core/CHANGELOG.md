# Changelog

All notable changes to the @conduit/core package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial release of @conduit/core
- Full TypeScript support with complete type definitions
- OpenAI-compatible API interface
- Chat completions support (streaming and non-streaming)
- Function calling support
- Models listing and caching
- Robust error handling with typed errors
- Automatic retry logic with exponential backoff
- Performance metrics tracking
- Correlation ID support for request tracking
- Comprehensive examples and documentation

### Features
- `ConduitCoreClient` - Main client class
- `ChatService` - Chat completions with streaming support
- `ModelsService` - Model listing and caching
- Request validation
- SSE streaming parser for real-time responses
- Zero runtime dependencies (except axios)

## [0.1.0] - TBD

- Initial public release