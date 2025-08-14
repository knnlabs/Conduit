# Changelog

All notable changes to the @conduit/core package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Video generation progress tracking with real-time updates via SignalR
- Unified `generateWithProgress()` method for video generation with callbacks
- Automatic fallback from SignalR to polling for progress updates
- Progress deduplication to prevent duplicate events
- Comprehensive video generation capabilities checking
- Support for cancelling video generation tasks
- React Query hooks for video generation with progress
- Enhanced error handling with retryable error support

### Changed
- Enhanced `VideosService` with optional SignalR dependencies
- Improved `pollTaskUntilCompletion()` with progress callbacks
- Added exponential backoff support for polling intervals

### Features
- **Video Generation Progress Tracking**
  - `generateWithProgress()` - Unified method with real-time progress
  - `VideoProgressTracker` - Dual-mode progress tracking (SignalR + polling)
  - Progress callbacks: `onProgress`, `onStarted`, `onCompleted`, `onFailed`
  - Automatic deduplication within 500ms windows
  - SignalR reconnection with exponential backoff
- **Core Features**
  - `ConduitCoreClient` - Main client class with SignalR support
  - `ChatService` - Chat completions with streaming support
  - `ModelsService` - Model listing and caching
  - `VideosService` - Video generation with progress tracking
  - Request validation
  - SSE streaming parser for real-time responses
  - Robust error handling with typed errors
  - Automatic retry logic with exponential backoff
  - Performance metrics tracking
  - Correlation ID support for request tracking

### Dependencies
- Added `@microsoft/signalr` for real-time communication
- Core dependency remains `axios` for HTTP requests

## [0.1.0] - TBD

- Initial public release