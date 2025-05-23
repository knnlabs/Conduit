# Legacy Mode Deprecation Timeline

This document outlines the timeline for deprecating direct database access mode in the Conduit LLM WebUI.

## Background

The Conduit LLM architecture has been migrated to use a microservice approach that separates the WebUI from direct database access. The Admin API now serves as the interface between the WebUI and the database, providing better security, scalability, and maintainability.

Currently, a legacy mode is supported by setting `CONDUIT_USE_ADMIN_API=false`, but this mode will be deprecated according to the timeline below.

## Deprecation Process

### Phase 1: Warning Period (May-July 2025)

- **May 2025**: Start displaying deprecation warnings in the WebUI when running in legacy mode
- **June 2025**: Release detailed migration guides and examples
- **July 2025**: Add more visible warnings and log messages about upcoming deprecation

### Phase 2: Feature Freeze (August-September 2025)

- **August 2025**: No new features will be added to legacy mode
- **September 2025**: Begin implementing code changes for removal
- **End of September 2025**: Release release candidate with legacy mode marked as obsolete

### Phase 3: Removal (October 2025)

- **October 1, 2025**: Feature branch opens for complete removal
- **October 15, 2025**: Pull request for removal created
- **October 31, 2025**: Final release with legacy mode support
- **November 1, 2025**: Legacy mode completely removed from codebase

## Communication Plan

To ensure users have ample time to migrate from legacy mode, the following communication steps will be taken:

1. **Documentation Updates**:
   - All documentation will be updated to emphasize the Admin API architecture
   - Migration guides will be published and maintained

2. **Release Notes**:
   - All release notes will mention the deprecation timeline
   - Specific migration steps will be included

3. **In-App Notifications**:
   - Warning banners will appear in the WebUI when running in legacy mode
   - Admin notifications will be sent to administrators

4. **GitHub Repository**:
   - Issues and milestones will track the deprecation process
   - Discussions will be monitored for user feedback

## Migration Support

To assist users in migrating from legacy mode to the Admin API architecture, the following support will be provided:

1. **Documentation**:
   - Step-by-step migration guides
   - Docker Compose and Kubernetes examples
   - Troubleshooting guides

2. **Migration Scripts**:
   - Scripts to verify Admin API connectivity
   - Health check tools

3. **Testing Guidelines**:
   - Test plans for verifying successful migration
   - Performance comparison guidelines

## After Removal

Once legacy mode is removed, the following benefits will be realized:

1. **Simplified Codebase**:
   - Removal of conditional code paths
   - Better maintainability
   - Cleaner architecture

2. **Improved Security**:
   - Database credentials only stored in Admin API
   - Reduced attack surface
   - Better authentication controls

3. **Enhanced Scalability**:
   - Independent scaling of WebUI and Admin API
   - Better resource utilization
   - Improved performance for large deployments

## Conclusion

This deprecation timeline provides a clear path for the removal of legacy mode, ensuring users have sufficient time to migrate to the new Admin API architecture. By following this schedule, we can maintain backward compatibility during the transition period while moving toward a more maintainable and secure architecture.