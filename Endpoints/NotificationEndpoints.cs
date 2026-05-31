using BookSharingApp.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BookSharingApp.Endpoints
{
    public static class NotificationEndpoints
    {
        public static void MapNotificationEndpoints(this WebApplication app)
        {
            var notifications = app.MapGroup("/notifications").WithTags("Notifications").RequireAuthorization();

            // GET /notifications - Get all unread notifications
            notifications.MapGet("", async (HttpContext httpContext, INotificationService notificationService) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                var unreadNotifications = await notificationService.GetUnreadNotificationsAsync(currentUserId);

                var notificationsDto = unreadNotifications.Select(n => new
                {
                    n.Id,
                    n.NotificationType,
                    n.Message,
                    n.CreatedAt,
                    n.ShareId,
                    Share = n.Share != null ? new
                    {
                        n.Share.Id,
                        Book = new
                        {
                            n.Share.UserBook.Book.Title,
                            n.Share.UserBook.Book.Author
                        }
                    } : null,
                    CreatedBy = new
                    {
                        n.CreatedByUser.Id,
                        n.CreatedByUser.FirstName,
                        n.CreatedByUser.LastName
                    }
                });

                return Results.Ok(notificationsDto);
            })
            .WithName("GetUnreadNotifications")
            .WithOpenApi();

            // PATCH /notifications/{id}/read - Mark a single notification as read
            notifications.MapPatch("/{id:int}/read", async (int id,
                HttpContext httpContext, INotificationService notificationService) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                var found = await notificationService.MarkNotificationAsReadAsync(id, currentUserId);

                return found ? Results.NoContent() : Results.NotFound();
            })
            .WithName("MarkNotificationAsRead")
            .WithOpenApi();

            // PATCH /notifications/shares/{shareId}/read - Mark share notifications as read
            notifications.MapPatch("/shares/{shareId:int}/read", async (int shareId,
                HttpContext httpContext, INotificationService notificationService) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                await notificationService.MarkShareNotificationsAsReadAsync(shareId, currentUserId);

                return Results.NoContent();
            })
            .WithName("MarkShareNotificationsAsRead")
            .WithOpenApi();

            // PATCH /notifications/shares/{shareId}/chat/read - Mark chat notifications as read
            notifications.MapPatch("/shares/{shareId:int}/chat/read", async (int shareId,
                HttpContext httpContext, INotificationService notificationService) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                await notificationService.MarkShareChatNotificationsAsReadAsync(shareId, currentUserId);

                return Results.NoContent();
            })
            .WithName("MarkShareChatNotificationsAsRead")
            .WithOpenApi();
        }
    }
}
