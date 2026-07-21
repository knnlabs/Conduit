using ConduitLLM.Admin.Auditing;
using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Core.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Admin.Endpoints;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/Notifications")
            .RequireAuthorization("MasterKeyPolicy")
            .AddEndpointFilter<OperationLoggingEndpointFilter>()
            .AddEndpointFilter<ValidationEndpointFilter>()
            .WithTags("Notifications");
        group.MapGet("/", GetAll).WithName("Notifications_GetAll")
            .Produces<IEnumerable<NotificationDto>>(StatusCodes.Status200OK);
        group.MapGet("/unread", GetUnread).WithName("Notifications_GetUnread")
            .Produces<IEnumerable<NotificationDto>>(StatusCodes.Status200OK);
        group.MapGet("/{id}", GetById).WithName("Notifications_GetById")
            .Produces<NotificationDto>(StatusCodes.Status200OK)
            .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapPost("/", Create).WithName("Notifications_Create")
            .Produces<NotificationDto>(StatusCodes.Status201Created)
            .Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest);
        group.MapPut("/{id}", Update).WithName("Notifications_Update")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponseDto>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapPost("/{id}/read", MarkAsRead).WithName("Notifications_MarkAsRead")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        group.MapPost("/mark-all-read", MarkAllAsRead).WithName("Notifications_MarkAllAsRead")
            .Produces<int>(StatusCodes.Status200OK);
        group.MapDelete("/{id}", Delete).WithName("Notifications_Delete")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponseDto>(StatusCodes.Status404NotFound);
        return app;
    }

    private static async Task<IResult> GetAll([FromServices] IAdminNotificationService service) =>
        Results.Ok(await service.GetAllNotificationsAsync());
    private static async Task<IResult> GetUnread([FromServices] IAdminNotificationService service) =>
        Results.Ok(await service.GetUnreadNotificationsAsync());
    private static async Task<IResult> GetById(int id, [FromServices] IAdminNotificationService service)
    {
        var value = await service.GetNotificationByIdAsync(id);
        return value is null ? AdminResults.NotFoundEntity("Notification", id) : Results.Ok(value);
    }
    private static async Task<IResult> Create(
        CreateNotificationDto notification,
        [FromServices] IAdminNotificationService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        var result = await service.CreateNotificationAsync(notification);
        AdminAudit.Log(context, Logger(loggerFactory), "Created", "Notification", result.Id,
            $"Type: {result.Type}, Message: {LoggingSanitizer.S(result.Message)}");
        return Results.Created($"/api/Notifications/{result.Id}", result);
    }
    private static async Task<IResult> Update(
        int id,
        UpdateNotificationDto notification,
        [FromServices] IAdminNotificationService service,
        HttpContext context,
        ILoggerFactory loggerFactory)
    {
        if (id != notification.Id) return AdminResults.BadRequest("ID in route must match ID in body");
        if (!await service.UpdateNotificationAsync(notification)) throw new KeyNotFoundException();
        AdminAudit.Log(context, Logger(loggerFactory), "Updated", "Notification", id,
            notification.Message is null ? null : $"Message: {LoggingSanitizer.S(notification.Message)}");
        return Results.NoContent();
    }
    private static async Task<IResult> MarkAsRead(
        int id, [FromServices] IAdminNotificationService service, HttpContext context, ILoggerFactory loggerFactory)
    {
        if (!await service.MarkNotificationAsReadAsync(id)) throw new KeyNotFoundException();
        AdminAudit.Log(context, Logger(loggerFactory), "MarkedAsRead", "Notification", id, "IsRead: true");
        return Results.NoContent();
    }
    private static async Task<IResult> MarkAllAsRead(
        [FromServices] IAdminNotificationService service, HttpContext context, ILoggerFactory loggerFactory)
    {
        var count = await service.MarkAllNotificationsAsReadAsync();
        AdminAudit.Log(context, Logger(loggerFactory), "MarkedAllAsRead", "Notification", detail: $"Count: {count}");
        return Results.Ok(count);
    }
    private static async Task<IResult> Delete(
        int id, [FromServices] IAdminNotificationService service, HttpContext context, ILoggerFactory loggerFactory)
    {
        if (!await service.DeleteNotificationAsync(id)) throw new KeyNotFoundException();
        AdminAudit.Log(context, Logger(loggerFactory), "Deleted", "Notification", id, $"Id: {id}");
        return Results.NoContent();
    }
    private static ILogger Logger(ILoggerFactory factory) =>
        factory.CreateLogger("ConduitLLM.Admin.Endpoints.Notifications");
}
