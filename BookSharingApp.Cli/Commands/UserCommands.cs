using BookSharingApp.Cli.Formatting;
using BookSharingApp.Common;
using BookSharingApp.Data;
using BookSharingApp.Services;
using Microsoft.EntityFrameworkCore;

namespace BookSharingApp.Cli.Commands;

public static class UserCommands
{
    public static async Task ListAsync(ApplicationDbContext db, string? search)
    {
        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var filter = search.ToLower();
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).ToLower().Contains(filter));
        }

        var users = await query
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email
            })
            .ToListAsync();

        if (users.Count == 0)
        {
            Console.WriteLine("No users found.");
            return;
        }

        var headers = new[] { "ID", "Name", "Email" };
        var rows = users.Select(u => new[]
        {
            u.Id,
            $"{u.FirstName} {u.LastName}".Trim(),
            u.Email ?? ""
        }).ToList();

        TableFormatter.Print(headers, rows, "user");
    }

    public static async Task BanAsync(ApplicationDbContext db, string userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            Console.Error.WriteLine($"User '{userId}' not found.");
            return;
        }

        if (user.LockoutEnd == DateTimeOffset.MaxValue)
        {
            Console.Error.WriteLine($"User '{userId}' is already banned.");
            return;
        }

        var name = $"{user.FirstName} {user.LastName}".Trim();
        Console.WriteLine($"User:  {name}");
        Console.WriteLine($"ID:    {user.Id}");
        Console.Write($"Ban {name} (id: {user.Id})? This is permanent and cannot be undone. [y/N]: ");

        var response = Console.ReadLine();
        if (response is null || (response.Trim().ToLower() != "y"))
        {
            Console.WriteLine("Aborted.");
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await ApplyBanAsync(db, user);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        Console.WriteLine($"User '{name}' has been banned.");
    }

    /// <summary>
    /// Stages the ban (token revocation + PII scrub) without saving or managing the transaction.
    /// The caller owns the transaction and must call SaveChangesAsync.
    /// </summary>
    public static async Task ApplyBanAsync(ApplicationDbContext db, BookSharingApp.Models.User user)
    {
        if (user.LockoutEnd == DateTimeOffset.MaxValue)
            throw new InvalidOperationException($"User '{user.Id}' is already banned.");

        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync();
        db.RefreshTokens.RemoveRange(tokens);

        var chatMessages = await db.ChatMessages
            .Where(m => m.SenderId == user.Id && !m.IsSystemMessage)
            .ToListAsync();
        foreach (var message in chatMessages)
            message.Content = "[deleted]";

        var userBooks = await db.UserBooks
            .Where(ub => ub.UserId == user.Id && !ub.IsDeleted)
            .ToListAsync();
        foreach (var userBook in userBooks)
        {
            userBook.IsDeleted = true;
            userBook.DeletedAt = DateTime.UtcNow;
        }

        // Decline only Requested shares — the book hasn't changed hands yet, so the share
        // can be cleanly cancelled. Active shares (Ready, PickedUp, Returned) are mid-flight
        // and need human resolution; auto-declining them would misrepresent physical reality.
        // No notifications are sent because the CLI doesn't have INotificationService wired up;
        // the "[Banned User]" name visible in share details serves as an implicit signal to
        // the remaining participant.
        var requestedShares = await db.Shares
            .Include(s => s.UserBook)
            .Where(s => s.Status == ShareStatus.Requested && !s.IsDisputed &&
                        (s.Borrower == user.Id || s.UserBook.UserId == user.Id))
            .ToListAsync();
        foreach (var share in requestedShares)
            share.Status = ShareStatus.Declined;

        var memberships = await db.CommunityUsers
            .Where(cu => cu.UserId == user.Id)
            .ToListAsync();
        foreach (var membership in memberships)
        {
            var memberCount = await db.CommunityUsers
                .CountAsync(cu => cu.CommunityId == membership.CommunityId);
            db.CommunityUsers.Remove(membership);

            if (memberCount == 1)
            {
                var community = await db.Communities.FindAsync(membership.CommunityId);
                if (community is not null)
                    db.Communities.Remove(community);
            }
        }

        UserPiiScrubber.Scrub(user, "Banned");
    }
}
