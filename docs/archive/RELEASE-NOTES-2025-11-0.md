# Release Notes - Conduit v2025.11.0

**Release Date: November 1, 2025**

This release marks a significant milestone for Conduit with the complete removal of direct database access mode from the WebUI component. This architectural change improves security, maintainability, and scalability.

## BREAKING CHANGES

> 🚨 **IMPORTANT**: This release removes the legacy direct database access mode completely. Users must migrate to the Admin API architecture before upgrading.

* Removed support for direct database access in WebUI
* Removed `CONDUIT_USE_ADMIN_API` environment variable
* Removed `CONDUIT_DISABLE_DIRECT_DB_ACCESS` environment variable
* WebUI no longer requires database credentials or connection strings

## Migration Guide

If you are currently using direct database access mode (`CONDUIT_USE_ADMIN_API=false`), you **MUST** migrate to the Admin API architecture before upgrading to this version.

Please follow the detailed migration guide: [Admin API Migration Guide](admin-api-migration-guide.md)

### Migration Checklist

1. Ensure you have updated to the latest v2025.10.x version first
2. Configure the Admin API service with proper database credentials
3. Update your WebUI configuration to use the Admin API:
   ```
   CONDUIT_ADMIN_API_BASE_URL=http://your-admin-api:8080
   CONDUIT_MASTER_KEY=your-master-key
   ```
4. Remove any database connection strings from WebUI environment variables
5. Test thoroughly before upgrading to v2025.11.0

## New Features

* Enhanced Admin API performance with optimized caching
* Improved health checks for Admin API connectivity
* New Admin API metrics dashboard in System Info page

## Improvements

* Simplified deployment architecture
* Reduced memory footprint for WebUI
* Improved startup time for WebUI
* Enhanced security through proper separation of concerns
* Better error handling and diagnostics for API communication
* Comprehensive documentation for Admin API architecture

## Bug Fixes

* Fixed issues with virtual key validation in high-latency environments
* Resolved race conditions in provider health monitoring
* Fixed cache invalidation issues in model provider mappings

## Removed Features

* Direct database access mode (`CONDUIT_USE_ADMIN_API=false`)
* Legacy service implementations in WebUI
* Database context registration in WebUI
* Entity Framework dependencies in WebUI

## Technical Changes

* Removed ~10,000 lines of legacy code
* Eliminated Entity Framework dependencies from WebUI
* Simplified Program.cs configuration
* Enhanced API client with caching and resilience
* Updated Docker and Kubernetes examples for new architecture

## Security Improvements

* WebUI no longer requires database credentials
* Stricter validation in Admin API endpoints
* Comprehensive input sanitization
* Improved authentication and authorization controls

## Documentation Updates

* New deployment architecture diagrams
* Updated environment variable documentation
* Enhanced troubleshooting guides
* New performance tuning recommendations

## Upgrading

Before upgrading to v2025.11.0, please ensure you have:

1. Read the migration guide thoroughly
2. Tested your deployment with Admin API mode
3. Backed up your database
4. Updated your deployment scripts and environment variables

## Support Resources

* [Migration Guide](admin-api-migration-guide.md)
* [Admin API Documentation](Admin-API.md)
* [Troubleshooting Guide](troubleshooting.md)
* [Discord Community](https://discord.gg/conduitllm)
* [GitHub Issues](https://github.com/knnlabs/Conduit/issues)

## Contributors

Thank you to all the contributors who helped make this release possible!

[List of contributors]

## Feedback

We value your feedback! Please share your experience with this release through:

* GitHub Discussions
* Discord Community
* Issues for bug reports

---

🔍 **Looking Ahead**: Our next release will focus on performance optimizations and new provider integrations.