import { FetchBaseApiClient } from '../client/FetchBaseApiClient';
import { ApiClientConfig } from '../client/types';
import {
  NotificationType,
  NotificationSeverity
} from '../models/system';
import type {
  NotificationDto,
  CreateNotificationDto,
  UpdateNotificationDto,
  NotificationFilters,
  NotificationSummary,
  NotificationBulkResponse,
  NotificationStatistics
} from '../models/system';

/**
 * Service for managing notifications through the Admin API
 */
export class NotificationsService extends FetchBaseApiClient {
  private readonly baseEndpoint = '/api/notifications';

  constructor(config: ApiClientConfig) {
    super(config);
  }

  /**
   * Retrieves all notifications ordered by creation date (descending)
   * 
   * @returns Promise<NotificationDto[]> A list of all notifications
   */
  async getAllNotifications(): Promise<NotificationDto[]> {
    return this.get<NotificationDto[]>(this.baseEndpoint);
  }

  /**
   * Retrieves only unread notifications
   * 
   * @returns Promise<NotificationDto[]> A list of unread notifications
   */
  async getUnreadNotifications(): Promise<NotificationDto[]> {
    return this.get<NotificationDto[]>(`${this.baseEndpoint}/unread`);
  }

  /**
   * Retrieves a specific notification by ID
   * 
   * @param notificationId - The ID of the notification to retrieve
   * @returns Promise<NotificationDto | null> The notification if found, null otherwise
   */
  async getNotificationById(notificationId: number): Promise<NotificationDto | null> {
    if (notificationId <= 0) {
      throw new Error('Notification ID must be greater than 0');
    }

    try {
      return await this.get<NotificationDto>(`${this.baseEndpoint}/${notificationId}`);
    } catch (error) {
      // Return null for 404 errors (notification not found)
      if (error instanceof Error && error.message.includes('404')) {
        return null;
      }
      throw error;
    }
  }

  /**
   * Creates a new notification
   * 
   * @param request - The notification creation request
   * @returns Promise<NotificationDto> The created notification
   */
  async createNotification(request: CreateNotificationDto): Promise<NotificationDto> {
    if (!request) {
      throw new Error('Request cannot be null');
    }

    if (!request.message || request.message.trim().length === 0) {
      throw new Error('Message is required');
    }

    if (request.message.length > 500) {
      throw new Error('Message cannot exceed 500 characters');
    }

    return this.post<NotificationDto>(this.baseEndpoint, request);
  }

  /**
   * Updates an existing notification
   * 
   * @param notificationId - The ID of the notification to update
   * @param request - The notification update request
   * @returns Promise<NotificationDto> The updated notification
   */
  async updateNotification(
    notificationId: number,
    request: UpdateNotificationDto
  ): Promise<NotificationDto> {
    if (notificationId <= 0) {
      throw new Error('Notification ID must be greater than 0');
    }
    if (!request) {
      throw new Error('Request cannot be null');
    }

    if (request.message && request.message.length > 500) {
      throw new Error('Message cannot exceed 500 characters');
    }

    return this.put<NotificationDto>(`${this.baseEndpoint}/${notificationId}`, request);
  }

  /**
   * Marks a specific notification as read
   * 
   * @param notificationId - The ID of the notification to mark as read
   */
  async markAsRead(notificationId: number): Promise<void> {
    if (notificationId <= 0) {
      throw new Error('Notification ID must be greater than 0');
    }

    await this.post(`${this.baseEndpoint}/${notificationId}/read`);
  }

  /**
   * Marks all notifications as read
   * 
   * @returns Promise<number> The number of notifications that were marked as read
   */
  async markAllAsRead(): Promise<number> {
    return this.post<number>(`${this.baseEndpoint}/mark-all-read`);
  }

  /**
   * Deletes a notification
   * 
   * @param notificationId - The ID of the notification to delete
   */
  async deleteNotification(notificationId: number): Promise<void> {
    if (notificationId <= 0) {
      throw new Error('Notification ID must be greater than 0');
    }

    await this.delete(`${this.baseEndpoint}/${notificationId}`);
  }

  /**
   * Gets notifications by type
   * 
   * @param type - The notification type to filter by
   * @returns Promise<NotificationDto[]> Notifications of the specified type
   */
  async getNotificationsByType(type: NotificationType): Promise<NotificationDto[]> {
    const allNotifications = await this.getAllNotifications();
    return allNotifications.filter(n => n.type === type);
  }

  /**
   * Gets notifications by severity
   * 
   * @param severity - The notification severity to filter by
   * @returns Promise<NotificationDto[]> Notifications of the specified severity
   */
  async getNotificationsBySeverity(severity: NotificationSeverity): Promise<NotificationDto[]> {
    const allNotifications = await this.getAllNotifications();
    return allNotifications.filter(n => n.severity === severity);
  }

  /**
   * Gets notifications for a specific virtual key
   * 
   * @param virtualKeyId - The virtual key ID to filter by
   * @returns Promise<NotificationDto[]> Notifications associated with the specified virtual key
   */
  async getNotificationsForVirtualKey(virtualKeyId: number): Promise<NotificationDto[]> {
    if (virtualKeyId <= 0) {
      throw new Error('Virtual key ID must be greater than 0');
    }

    const allNotifications = await this.getAllNotifications();
    return allNotifications.filter(n => n.virtualKeyId === virtualKeyId);
  }

  /**
   * Gets notifications created within a specific date range
   * 
   * @param startDate - The start date (inclusive)
   * @param endDate - The end date (inclusive)
   * @returns Promise<NotificationDto[]> Notifications created within the specified date range
   */
  async getNotificationsByDateRange(
    startDate: Date,
    endDate: Date
  ): Promise<NotificationDto[]> {
    if (startDate > endDate) {
      throw new Error('Start date cannot be greater than end date');
    }

    const allNotifications = await this.getAllNotifications();
    return allNotifications.filter(n => {
      const notificationDate = new Date(n.createdAt);
      return notificationDate >= startDate && notificationDate <= endDate;
    });
  }

  /**
   * Gets notification statistics including counts by type, severity, and read status
   * 
   * @returns Promise<NotificationStatistics> Notification statistics summary
   */
  async getNotificationStatistics(): Promise<NotificationStatistics> {
    const allNotifications = await this.getAllNotifications();
    
    const total = allNotifications.length;
    const unread = allNotifications.filter(n => !n.isRead).length;
    const read = allNotifications.filter(n => n.isRead).length;

    // Group by type
    const byType: Record<string, number> = {};
    allNotifications.forEach(n => {
      const typeKey = NotificationType[n.type];
      byType[typeKey] = (byType[typeKey] || 0) + 1;
    });

    // Group by severity
    const bySeverity: Record<string, number> = {};
    allNotifications.forEach(n => {
      const severityKey = NotificationSeverity[n.severity];
      bySeverity[severityKey] = (bySeverity[severityKey] || 0) + 1;
    });

    // Calculate recent activity
    const now = new Date();
    const oneHourAgo = new Date(now.getTime() - (60 * 60 * 1000));
    const oneDayAgo = new Date(now.getTime() - (24 * 60 * 60 * 1000));
    const oneWeekAgo = new Date(now.getTime() - (7 * 24 * 60 * 60 * 1000));

    const recent = {
      lastHour: allNotifications.filter(n => new Date(n.createdAt) > oneHourAgo).length,
      last24Hours: allNotifications.filter(n => new Date(n.createdAt) > oneDayAgo).length,
      lastWeek: allNotifications.filter(n => new Date(n.createdAt) > oneWeekAgo).length
    };

    return {
      total,
      unread,
      read,
      byType,
      bySeverity,
      recent
    };
  }

  /**
   * Gets the count of unread notifications
   * 
   * @returns Promise<number> The number of unread notifications
   */
  async getUnreadCount(): Promise<number> {
    const unreadNotifications = await this.getUnreadNotifications();
    return unreadNotifications.length;
  }

  /**
   * Checks if there are any unread notifications
   * 
   * @returns Promise<boolean> True if there are unread notifications, false otherwise
   */
  async hasUnreadNotifications(): Promise<boolean> {
    try {
      const count = await this.getUnreadCount();
      return count > 0;
    } catch {
      return false;
    }
  }

  /**
   * Marks multiple notifications as read by their IDs
   * 
   * @param notificationIds - The IDs of notifications to mark as read
   * @returns Promise<NotificationBulkResponse> The bulk operation response
   */
  async markMultipleAsRead(notificationIds: number[]): Promise<NotificationBulkResponse> {
    if (!notificationIds || notificationIds.length === 0) {
      return {
        successCount: 0,
        totalCount: 0,
        failedIds: [],
        errors: []
      };
    }

    const totalCount = notificationIds.length;
    let successCount = 0;
    const failedIds: number[] = [];
    const errors: string[] = [];

    for (const id of notificationIds) {
      try {
        await this.markAsRead(id);
        successCount++;
      } catch (error) {
        failedIds.push(id);
        errors.push(`Failed to mark notification ${id} as read: ${error instanceof Error ? error.message : 'Unknown error'}`);
      }
    }

    return {
      successCount,
      totalCount,
      failedIds,
      errors
    };
  }

  /**
   * Deletes multiple notifications by their IDs
   * 
   * @param notificationIds - The IDs of notifications to delete
   * @returns Promise<NotificationBulkResponse> The bulk operation response
   */
  async deleteMultiple(notificationIds: number[]): Promise<NotificationBulkResponse> {
    if (!notificationIds || notificationIds.length === 0) {
      return {
        successCount: 0,
        totalCount: 0,
        failedIds: [],
        errors: []
      };
    }

    const totalCount = notificationIds.length;
    let successCount = 0;
    const failedIds: number[] = [];
    const errors: string[] = [];

    for (const id of notificationIds) {
      try {
        await this.deleteNotification(id);
        successCount++;
      } catch (error) {
        failedIds.push(id);
        errors.push(`Failed to delete notification ${id}: ${error instanceof Error ? error.message : 'Unknown error'}`);
      }
    }

    return {
      successCount,
      totalCount,
      failedIds,
      errors
    };
  }

  /**
   * Gets a filtered list of notifications based on the provided filters
   * 
   * @param filters - The filters to apply
   * @returns Promise<NotificationDto[]> Filtered list of notifications
   */
  async getFilteredNotifications(filters: NotificationFilters): Promise<NotificationDto[]> {
    let notifications = await this.getAllNotifications();

    // Apply filters
    if (filters.type !== undefined) {
      notifications = notifications.filter(n => n.type === filters.type);
    }

    if (filters.severity !== undefined) {
      notifications = notifications.filter(n => n.severity === filters.severity);
    }

    if (filters.isRead !== undefined) {
      notifications = notifications.filter(n => n.isRead === filters.isRead);
    }

    if (filters.virtualKeyId !== undefined) {
      notifications = notifications.filter(n => n.virtualKeyId === filters.virtualKeyId);
    }

    if (filters.startDate) {
      notifications = notifications.filter(n => new Date(n.createdAt) >= filters.startDate!);
    }

    if (filters.endDate) {
      notifications = notifications.filter(n => new Date(n.createdAt) <= filters.endDate!);
    }

    // Apply sorting
    if (filters.sortBy) {
      const sortDirection = filters.sortDirection === 'asc' ? 1 : -1;
      notifications.sort((a, b) => {
        const aValue = (a as any)[filters.sortBy!];
        const bValue = (b as any)[filters.sortBy!];
        
        if (aValue < bValue) return -1 * sortDirection;
        if (aValue > bValue) return 1 * sortDirection;
        return 0;
      });
    }

    // Apply pagination
    if (filters.page !== undefined && filters.pageSize !== undefined) {
      const startIndex = (filters.page - 1) * filters.pageSize;
      const endIndex = startIndex + filters.pageSize;
      notifications = notifications.slice(startIndex, endIndex);
    }

    return notifications;
  }

  /**
   * Gets a summary of notification data with key metrics
   * 
   * @returns Promise<NotificationSummary> Notification summary object
   */
  async getNotificationSummary(): Promise<NotificationSummary> {
    const allNotifications = await this.getAllNotifications();
    
    const totalNotifications = allNotifications.length;
    const unreadNotifications = allNotifications.filter(n => !n.isRead).length;
    const readNotifications = allNotifications.filter(n => n.isRead).length;

    // Group by type
    const notificationsByType: Record<NotificationType, number> = {} as Record<NotificationType, number>;
    Object.values(NotificationType)
      .filter(value => typeof value === 'number')
      .forEach(type => {
        notificationsByType[type as NotificationType] = 0;
      });

    allNotifications.forEach(n => {
      notificationsByType[n.type] = (notificationsByType[n.type] || 0) + 1;
    });

    // Group by severity
    const notificationsBySeverity: Record<NotificationSeverity, number> = {} as Record<NotificationSeverity, number>;
    Object.values(NotificationSeverity)
      .filter(value => typeof value === 'number')
      .forEach(severity => {
        notificationsBySeverity[severity as NotificationSeverity] = 0;
      });

    allNotifications.forEach(n => {
      notificationsBySeverity[n.severity] = (notificationsBySeverity[n.severity] || 0) + 1;
    });

    // Find most recent and oldest unread notifications
    const sortedNotifications = allNotifications.sort((a, b) => 
      new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    );
    
    const mostRecentNotification = sortedNotifications[0];
    const unreadNotificationsSorted = allNotifications
      .filter(n => !n.isRead)
      .sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
    const oldestUnreadNotification = unreadNotificationsSorted[0];

    return {
      totalNotifications,
      unreadNotifications,
      readNotifications,
      notificationsByType,
      notificationsBySeverity,
      mostRecentNotification,
      oldestUnreadNotification
    };
  }
}