using BookSharingApp.Common;
using BookSharingApp.Data;
using BookSharingApp.Models;
using BookSharingApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BookSharingApp.Endpoints
{
    public static class ChatEndpoints
    {
        public static void MapChatEndpoints(this WebApplication app)
        {
            var chats = app.MapGroup("/shares/{shareId:int}/chat").WithTags("Chat").RequireAuthorization();

            // GET /shares/{shareId}/chat/messages - Get paginated chat messages
            chats.MapGet("/messages", async (int shareId, HttpContext httpContext, ApplicationDbContext context,
                IChatService chatService, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                // Verify access permissions
                var shareChatThread = await context.ShareChatThreads
                    .Include(sct => sct.Share)
                        .ThenInclude(s => s.UserBook)
                    .FirstOrDefaultAsync(sct => sct.ShareId == shareId);

                if (shareChatThread?.Share == null)
                    return Results.NotFound("Share not found");

                // Check access permissions
                var share = shareChatThread.Share;
                if (share.Borrower != currentUserId && share.UserBook.UserId != currentUserId)
                    return Results.Forbid();

                try
                {
                    // Get messages through service
                    var messages = await chatService.GetMessageThreadAsync(shareId, page, pageSize);
                    var totalMessages = await chatService.GetMessageCountAsync(shareId);

                    var messagesDto = messages.Select(m => new
                    {
                        m.Id,
                        m.Content,
                        m.SentAt,
                        m.IsSystemMessage,
                        Sender = new
                        {
                            m.Sender.Id,
                            m.Sender.FirstName,
                            m.Sender.LastName
                        }
                    });

                    var result = new
                    {
                        Messages = messagesDto,
                        Pagination = new
                        {
                            Page = page,
                            PageSize = pageSize,
                            TotalMessages = totalMessages,
                            TotalPages = (int)Math.Ceiling(totalMessages / (double)pageSize)
                        }
                    };

                    return Results.Ok(result);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(ex.Message);
                }
            })
            .WithName("GetShareChatMessages")
            .WithOpenApi();

            // POST /shares/{shareId}/chat/messages - Send a message (also broadcasts via SignalR)
            chats.MapPost("/messages", async (int shareId, [FromBody] SendMessageRequest request,
                HttpContext httpContext, IChatService chatService) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                try
                {
                    var message = await chatService.SendMessageAsync(shareId, currentUserId, request.Content);

                    var messageResponse = new
                    {
                        message.Id,
                        message.Content,
                        message.SentAt,
                        message.IsSystemMessage,
                        Sender = new
                        {
                            message.Sender.Id,
                            message.Sender.FirstName,
                            message.Sender.LastName
                        }
                    };

                    return Results.Created($"/shares/{shareId}/chat/messages/{message.Id}", messageResponse);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
                catch (Exception)
                {
                    // Log error if needed
                    return Results.Problem("Failed to send message");
                }
            })
            .WithName("SendChatMessage")
            .WithOpenApi()
            .WithMetadata(new RateLimitAttribute(RateLimitNames.ChatSend, RateLimitScope.User));

            // POST /shares/{shareId}/chat/messages/{messageId}/report - Report a message
            chats.MapPost("/messages/{messageId:int}/report", async (int shareId, int messageId,
                [FromBody] ReportMessageRequest request, HttpContext httpContext, IChatService chatService) =>
            {
                var currentUserId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

                if (!Enum.IsDefined(request.Category))
                    return Results.BadRequest("Invalid report category");

                try
                {
                    await chatService.ReportMessageAsync(shareId, messageId, currentUserId, request.Category, request.Notes);
                    return Results.NoContent();
                }
                catch (DuplicateReportException ex)
                {
                    return Results.Conflict(ex.Message);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.NotFound(ex.Message);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Forbid();
                }
            })
            .WithName("ReportChatMessage")
            .WithOpenApi()
            .WithMetadata(new RateLimitAttribute(RateLimitNames.ChatReport, RateLimitScope.User));
        }
    }

    public record SendMessageRequest(string Content);
    public record ReportMessageRequest(ReportCategory Category, string? Notes);
}