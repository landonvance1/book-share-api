using BookSharingApp.Common;
using BookSharingApp.Data;
using BookSharingApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookSharingApp.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ChatService> _logger;
        private readonly INotificationService _notificationService;

        public ChatService(ApplicationDbContext context, ILogger<ChatService> logger, INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
        }

        // Chat thread management
        public async Task CreateShareChatAsync(int shareId)
        {
            try
            {
                // Check if chat already exists
                var existingChat = await _context.ShareChatThreads
                    .FirstOrDefaultAsync(sct => sct.ShareId == shareId);

                if (existingChat != null)
                {
                    _logger.LogWarning("Chat thread already exists for share {ShareId}", shareId);
                    return;
                }

                // Create new chat thread
                var thread = new ChatThread
                {
                    CreatedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                };

                _context.ChatThreads.Add(thread);
                await _context.SaveChangesAsync();

                // Create share chat thread relationship
                var shareChatThread = new ShareChatThread
                {
                    ThreadId = thread.Id,
                    ShareId = shareId
                };

                _context.ShareChatThreads.Add(shareChatThread);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created chat thread {ThreadId} for share {ShareId}", thread.Id, shareId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create chat for share {ShareId}", shareId);
                throw;
            }
        }

        // Message operations
        public async Task<List<ChatMessage>> GetMessageThreadAsync(int shareId, int page, int pageSize)
        {
            // Validate page parameters
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            // Find the share chat thread
            var shareChatThread = await _context.ShareChatThreads
                .FirstOrDefaultAsync(sct => sct.ShareId == shareId);

            if (shareChatThread == null)
                throw new InvalidOperationException("Share chat thread not found");

            // Get paginated messages
            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.ThreadId == shareChatThread.ThreadId)
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return messages;
        }

        public async Task<int> GetMessageCountAsync(int shareId)
        {
            var shareChatThread = await _context.ShareChatThreads
                .FirstOrDefaultAsync(sct => sct.ShareId == shareId);

            if (shareChatThread == null)
                return 0;

            return await _context.ChatMessages
                .CountAsync(m => m.ThreadId == shareChatThread.ThreadId);
        }

        public async Task<ChatMessage> SendMessageAsync(int shareId, string senderId, string content)
        {
            // Validate message content
            if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
                throw new ArgumentException("Message content is required and must be less than 2000 characters");

            var chatContext = await ShareChatContext.CreateForShareAsync(shareId, _context);
            if (chatContext == null)
                throw new InvalidOperationException("Share not found");

            if (!await chatContext.CanUserAccessAsync(senderId, _context))
                throw new UnauthorizedAccessException("Access denied to this share chat");

            var message = new ChatMessage
            {
                ThreadId = chatContext.ThreadId,
                SenderId = senderId,
                Content = content.Trim(),
                SentAt = DateTime.UtcNow,
                IsSystemMessage = false
            };

            _context.ChatMessages.Add(message);

            // Update thread last activity
            var thread = await _context.ChatThreads.FindAsync(chatContext.ThreadId);
            if (thread != null)
            {
                thread.LastActivity = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Load sender info for response
            await _context.Entry(message).Reference(m => m.Sender).LoadAsync();

            _logger.LogInformation("Message {MessageId} sent in share {ShareId} by user {SenderId}",
                message.Id, shareId, senderId);

            // Create notification for the other party about new message
            await _notificationService.CreateShareNotificationAsync(
                shareId,
                Common.NotificationType.ShareMessageReceived,
                $"New message from {message.Sender.FullName}",
                senderId);

            return message;
        }

        public async Task ReportMessageAsync(int shareId, int messageId, string reporterId, ReportCategory category, string? notes)
        {
            var shareExists = await _context.Shares.AnyAsync(s => s.Id == shareId);
            if (!shareExists)
                throw new InvalidOperationException("Share not found");

            var chatContext = (await ShareChatContext.CreateForShareAsync(shareId, _context))!;

            if (!await chatContext.CanUserAccessAsync(reporterId, _context))
                throw new UnauthorizedAccessException("Access denied to this share chat");

            var message = await _context.ChatMessages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ThreadId == chatContext.ThreadId);

            if (message == null)
                throw new InvalidOperationException("Message not found");

            if (message.SenderId == reporterId)
                throw new ArgumentException("Cannot report your own message");

            var duplicate = await _context.ChatMessageReports
                .AnyAsync(r => r.MessageId == messageId && r.ReporterId == reporterId);

            if (duplicate)
                throw new DuplicateReportException();

            var report = new ChatMessageReport
            {
                MessageId = messageId,
                ReporterId = reporterId,
                ReportedUserId = message.SenderId,
                Category = category,
                ReportedContent = message.Content,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatMessageReports.Add(report);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Message {MessageId} in share {ShareId} reported by user {ReporterId}", messageId, shareId, reporterId);
        }
    }
}