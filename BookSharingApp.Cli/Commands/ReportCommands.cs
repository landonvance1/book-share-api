using BookSharingApp.Cli.Formatting;
using BookSharingApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BookSharingApp.Cli.Commands;

public static class ReportCommands
{
    private const int DefaultLimit = 50;

    private static string FormatName(string firstName, string lastName)
        => $"{firstName} {lastName}".Trim();

    public static async Task ListAsync(ApplicationDbContext db, string? userFilter, bool showAll = false)
    {
        var query = db.ChatMessageReports
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .AsQueryable();

        if (!showAll)
            query = query.Where(r => !r.IsResolved);

        if (!string.IsNullOrWhiteSpace(userFilter))
        {
            var filter = userFilter.ToLower();
            query = query.Where(r =>
                (r.ReportedUser.FirstName + " " + r.ReportedUser.LastName).ToLower().Contains(filter));
        }

        var reports = await query
            .Take(DefaultLimit)
            .Select(r => new
            {
                r.Id,
                r.CreatedAt,
                r.Category,
                r.IsResolved,
                ReportedUser = r.ReportedUser.FirstName + " " + r.ReportedUser.LastName,
                Reporter = r.Reporter.FirstName + " " + r.Reporter.LastName,
                Preview = r.ReportedContent.Length > 50
                    ? r.ReportedContent.Substring(0, 50) + "..."
                    : r.ReportedContent
            }).ToListAsync();

        if (reports.Count == 0)
        {
            Console.WriteLine("No reports found.");
            return;
        }

        var headers = new[] { "ID", "Created", "Category", "Resolved", "Reported User", "Reporter", "Message Preview" };
        var rows = reports.Select(r => new[]
        {
            r.Id.ToString(),
            r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            r.Category.ToString(),
            r.IsResolved ? "Yes" : "No",
            r.ReportedUser.Trim(),
            r.Reporter.Trim(),
            r.Preview
        }).ToList();

        TableFormatter.Print(headers, rows, "report");
    }

    public static async Task ViewAsync(ApplicationDbContext db, int reportId)
    {
        var report = await db.ChatMessageReports
            .AsNoTracking()
            .Include(r => r.Reporter)
            .Include(r => r.ReportedUser)
            .Include(r => r.Message)
                .ThenInclude(m => m.Thread)
                    .ThenInclude(t => t.ShareChatThread)
                        .ThenInclude(sct => sct!.Share)
                            .ThenInclude(s => s.UserBook)
                                .ThenInclude(ub => ub.Book)
            .Include(r => r.Message)
                .ThenInclude(m => m.Thread)
                    .ThenInclude(t => t.ShareChatThread)
                        .ThenInclude(sct => sct!.Share)
                            .ThenInclude(s => s.UserBook)
                                .ThenInclude(ub => ub.User)
            .Include(r => r.Message)
                .ThenInclude(m => m.Thread)
                    .ThenInclude(t => t.ShareChatThread)
                        .ThenInclude(sct => sct!.Share)
                            .ThenInclude(s => s.BorrowerUser)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == reportId);

        if (report is null)
        {
            Console.WriteLine($"Report #{reportId} not found.");
            return;
        }

        Console.WriteLine($"Report #{report.Id}");
        Console.WriteLine(new string('─', 40));
        Console.WriteLine($"  Created:        {report.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"  Category:       {report.Category}");
        Console.WriteLine($"  Resolved:       {(report.IsResolved ? "Yes" : "No")}");
        Console.WriteLine($"  Reported User:  {FormatName(report.ReportedUser.FirstName, report.ReportedUser.LastName)}");
        Console.WriteLine($"  Reporter:       {FormatName(report.Reporter.FirstName, report.Reporter.LastName)}");
        Console.WriteLine();
        Console.WriteLine("Message Content:");
        Console.WriteLine($"  {report.ReportedContent}");
        Console.WriteLine();
        Console.WriteLine("Reporter Notes:");
        Console.WriteLine($"  {report.Notes ?? "(none)"}");

        var share = report.Message.Thread.ShareChatThread?.Share;
        if (share is not null)
        {
            Console.WriteLine();
            Console.WriteLine("Share Context:");
            Console.WriteLine($"  Share ID:       {share.Id}");
            Console.WriteLine($"  Book Title:     {share.UserBook.Book.Title}");
            Console.WriteLine($"  Lender:         {FormatName(share.UserBook.User.FirstName, share.UserBook.User.LastName)}");
            Console.WriteLine($"  Borrower:       {FormatName(share.BorrowerUser.FirstName, share.BorrowerUser.LastName)}");
            Console.WriteLine($"  Share Status:   {share.Status}");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("Share Context:");
            Console.WriteLine("  (not available)");
        }
    }

    public static async Task StatsAsync(ApplicationDbContext db)
    {
        var total = await db.ChatMessageReports.AsNoTracking().CountAsync();

        if (total == 0)
        {
            Console.WriteLine("No reports found.");
            return;
        }

        var byCategory = await db.ChatMessageReports.AsNoTracking()
            .GroupBy(r => r.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        var topUsers = await db.ChatMessageReports.AsNoTracking()
            .GroupBy(r => r.ReportedUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var userIds = topUsers.Select(x => x.UserId).ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());

        var resolved = await db.ChatMessageReports.AsNoTracking().CountAsync(r => r.IsResolved);
        var unresolved = total - resolved;

        Console.WriteLine("Report Statistics");
        Console.WriteLine(new string('─', 40));
        Console.WriteLine($"  Total Reports: {total}");
        Console.WriteLine($"  Resolved:      {resolved}");
        Console.WriteLine($"  Unresolved:    {unresolved}");
        Console.WriteLine();

        Console.WriteLine("By Category:");
        foreach (var cat in byCategory)
            Console.WriteLine($"  {cat.Category,-25} {cat.Count}");
        Console.WriteLine();

        Console.WriteLine("Top Reported Users:");
        foreach (var user in topUsers)
        {
            var name = users.GetValueOrDefault(user.UserId, user.UserId);
            Console.WriteLine($"  {name,-25} {user.Count} report(s)");
        }
    }

    public static async Task ResolveAsync(ApplicationDbContext db, int reportId, bool resolved)
    {
        var report = await db.ChatMessageReports.FindAsync(reportId);
        if (report is null)
        {
            Console.WriteLine($"Report #{reportId} not found.");
            return;
        }
        if (report.IsResolved == resolved)
        {
            Console.WriteLine($"Report #{reportId} is already {(resolved ? "resolved" : "unresolved")}.");
            return;
        }

        report.IsResolved = resolved;
        await db.SaveChangesAsync();
        Console.WriteLine($"Report #{reportId} marked as {(resolved ? "resolved" : "unresolved")}.");
    }
}
