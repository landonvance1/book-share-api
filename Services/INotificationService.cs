using BookSharingApp.Models;

namespace BookSharingApp.Services
{
    public interface INotificationService
    {
        // Create notification
        Task CreateShareNotificationAsync(int shareId, string notificationType, string message, string createdByUserId);

        // Get notifications
        Task<List<Notification>> GetUnreadNotificationsAsync(string userId);

        // Mark notifications as read
        Task MarkShareNotificationsAsReadAsync(int shareId, string userId);
        Task MarkShareChatNotificationsAsReadAsync(int shareId, string userId);

        /// <summary>
        /// Marks all unread notifications for a specific share and user as read, regardless of notification type.
        /// Used when a user archives a share to clean up their notification list.
        /// </summary>
        /// <param name="shareId">The ID of the share.</param>
        /// <param name="userId">The ID of the user whose notifications should be marked as read.</param>
        Task MarkAllShareNotificationsAsReadForUserAsync(int shareId, string userId);

        /// <summary>
        /// Marks a single notification as read by ID. Returns false if the notification is not found
        /// or does not belong to the given user.
        /// </summary>
        Task<bool> MarkNotificationAsReadAsync(int notificationId, string userId);
    }
}
