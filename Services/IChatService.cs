using BookSharingApp.Common;
using BookSharingApp.Models;

namespace BookSharingApp.Services
{
    public interface IChatService
    {
        // Chat thread management
        Task CreateShareChatAsync(int shareId);

        // Message operations
        Task<List<ChatMessage>> GetMessageThreadAsync(int shareId, int page, int pageSize);
        Task<int> GetMessageCountAsync(int shareId);
        Task<ChatMessage> SendMessageAsync(int shareId, string senderId, string content);
        Task ReportMessageAsync(int shareId, int messageId, string reporterId, ReportCategory category, string? notes);
    }
}