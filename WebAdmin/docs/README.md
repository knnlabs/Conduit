# Conduit WebUI Documentation

## Overview

This directory contains comprehensive documentation for the Conduit WebUI, including architecture details, migration guides, and security considerations.

## Documentation Index

### 📐 [Architecture](./ARCHITECTURE.md)
Detailed overview of the WebUI architecture, including:
- Architecture evolution from proxy-based to direct SDK
- Component architecture and data flow
- Authentication and security architecture
- Deployment architecture with Docker

### 🔑 [Virtual Key Management](./VIRTUAL-KEY-MANAGEMENT.md)
Complete guide to WebUI virtual key system:
- How virtual keys work
- Automatic key creation and management
- Security model and best practices
- Key rotation and monitoring

### 🔄 [Migration Guide](./MIGRATION-GUIDE.md)
Step-by-step guide for migrating from API routes to SDK hooks:
- Before and after code examples
- Provider setup instructions
- Common patterns and best practices
- Rollback strategies

### 🔧 [Troubleshooting](./TROUBLESHOOTING.md)
Solutions to common issues:
- Authentication problems
- SDK hook issues
- Network and CORS errors
- Performance optimization
- Debugging tools and techniques

### 🔒 [Security Considerations](./SECURITY-CONSIDERATIONS.md)
Important security information:
- Authentication key separation
- Virtual key exposure risks
- Security layers and best practices
- Incident response procedures

### 🔐 [Security Authentication SDK](./SECURITY-AUTH-SDK.md)
Technical security implementation details:
- Authentication flow
- Session management
- Rate limiting
- Security headers

## Quick Links

### For Developers
- [Migration Guide](./MIGRATION-GUIDE.md) - Start here if migrating existing code
- [Architecture](./ARCHITECTURE.md) - Understand the system design
- [Troubleshooting](./TROUBLESHOOTING.md) - Common issues and solutions

### For Administrators
- [Virtual Key Management](./VIRTUAL-KEY-MANAGEMENT.md) - Managing API keys
- [Security Considerations](./SECURITY-CONSIDERATIONS.md) - Security best practices
- [Troubleshooting](./TROUBLESHOOTING.md) - Debugging authentication issues

### For Security Teams
- [Security Considerations](./SECURITY-CONSIDERATIONS.md) - Security overview
- [Security Authentication SDK](./SECURITY-AUTH-SDK.md) - Technical details
- [Virtual Key Management](./VIRTUAL-KEY-MANAGEMENT.md) - Key security model

## Getting Started

1. **New to WebUI?** Start with [Architecture](./ARCHITECTURE.md)
2. **Migrating code?** Read the [Migration Guide](./MIGRATION-GUIDE.md)
3. **Having issues?** Check [Troubleshooting](./TROUBLESHOOTING.md)
4. **Security concerns?** Review [Security Considerations](./SECURITY-CONSIDERATIONS.md)

## Contributing

When adding new documentation:
1. Use clear, descriptive filenames
2. Include a table of contents for long documents
3. Add code examples where appropriate
4. Update this index file
5. Test all code examples

## Additional Resources

- [WebUI README](../README.md) - Main project documentation
- [Conduit Documentation](https://github.com/knnlabs/Conduit/docs) - Platform documentation
- [SDK Documentation](https://www.npmjs.com/package/@knn_labs/conduit-core-client) - SDK reference