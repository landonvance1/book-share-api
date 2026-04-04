using BookSharingApp.Cli.Commands;
using BookSharingApp.Data;
using Microsoft.EntityFrameworkCore;

// Resolve connection string from args or environment
var connectionString = ResolveConnectionString(args);
await using var db = CreateDbContext(connectionString);

// If commands were passed (after stripping --connection-string), run one-shot
var commandArgs = StripConnectionStringArgs(args);
if (commandArgs.Length > 0)
{
    return await RunCommandAsync(db, commandArgs);
}

// Interactive REPL mode
Console.WriteLine("BookSharing Admin CLI");
Console.WriteLine("Type 'help' for available commands, 'exit' to quit.");
Console.WriteLine();

while (true)
{
    Console.Write(">> ");
    var line = Console.ReadLine();

    if (line is null) // EOF / Ctrl+D
        break;

    var input = line.Trim();
    if (input.Length == 0)
        continue;

    if (input is "exit" or "quit")
        break;

    if (input == "help")
    {
        PrintHelp();
        continue;
    }

    var inputArgs = SplitArgs(input);
    var result = await RunCommandAsync(db, inputArgs);
    if (result != 0)
        Console.WriteLine($"Command failed (exit code {result}).");

    Console.WriteLine();
}

return 0;

// --- Helpers ---

static async Task<int> RunCommandAsync(ApplicationDbContext db, string[] args)
{
    if (args.Length == 0)
        return 1;

    var baseCommand = args.Length >= 2 ? $"{args[0]} {args[1]}" : args[0];

    try
    {
        switch (baseCommand)
        {
            case "reports list":
                var userFilter = GetOptionValue(args, "--user");
                var showAll = args.Contains("--all");
                await ReportCommands.ListAsync(db, userFilter, showAll);
                return 0;

            case "reports view" when GetPositionalArg(args, 2) is { } viewIdStr && int.TryParse(viewIdStr, out var viewId):
                await ReportCommands.ViewAsync(db, viewId);
                return 0;

            case "reports view":
                Console.Error.WriteLine("Usage: reports view <id>");
                return 1;

            case "reports stats":
                await ReportCommands.StatsAsync(db);
                return 0;

            case "reports resolve" when GetPositionalArg(args, 2) is { } resolveIdStr && int.TryParse(resolveIdStr, out var resolveId):
                await ReportCommands.ResolveAsync(db, resolveId, resolved: true);
                return 0;

            case "reports resolve":
                Console.Error.WriteLine("Usage: reports resolve <id>");
                return 1;

            case "reports unresolve" when GetPositionalArg(args, 2) is { } unresolveIdStr && int.TryParse(unresolveIdStr, out var unresolveId):
                await ReportCommands.ResolveAsync(db, unresolveId, resolved: false);
                return 0;

            case "reports unresolve":
                Console.Error.WriteLine("Usage: reports unresolve <id>");
                return 1;

            case "reports ban" when GetPositionalArg(args, 2) is { } banReportIdStr && int.TryParse(banReportIdStr, out var banReportId):
                await ReportCommands.BanAsync(db, banReportId);
                return 0;

            case "reports ban":
                Console.Error.WriteLine("Usage: reports ban <reportId>");
                return 1;

            case "users list":
                var userSearch = GetOptionValue(args, "--search");
                await UserCommands.ListAsync(db, userSearch);
                return 0;

            case "users ban" when GetPositionalArg(args, 2) is { } banUserIdStr:
                await UserCommands.BanAsync(db, banUserIdStr);
                return 0;

            case "users ban":
                Console.Error.WriteLine("Usage: users ban <id>");
                return 1;

            default:
                Console.Error.WriteLine($"Unknown command: {baseCommand}");
                Console.Error.WriteLine("Type 'help' for available commands.");
                return 1;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

static void PrintHelp()
{
    Console.WriteLine("Available commands:");
    Console.WriteLine("  reports list [--all] [--user <name>]  List reports (unresolved by default)");
    Console.WriteLine("  reports view <id>                     View full details of a report");
    Console.WriteLine("  reports resolve <id>                  Mark a report as resolved");
    Console.WriteLine("  reports unresolve <id>                Mark a report as unresolved");
    Console.WriteLine("  reports ban <reportId>                Ban reported user and resolve report");
    Console.WriteLine("  reports stats                         Show report statistics");
    Console.WriteLine("  users list [--search <name>]          List users with banned status");
    Console.WriteLine("  users ban <id>                        Permanently ban a user");
    Console.WriteLine("  help                                  Show this help message");
    Console.WriteLine("  exit                                  Exit the CLI");
}

static string ResolveConnectionString(string[] args)
{
    var connStr = GetOptionValue(args, "--connection-string");
    connStr ??= Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

    if (string.IsNullOrWhiteSpace(connStr))
    {
        Console.Error.WriteLine("Error: No connection string provided.");
        Console.Error.WriteLine("Use --connection-string or set the ConnectionStrings__DefaultConnection environment variable.");
        Environment.Exit(1);
        return null!;
    }

    return connStr;
}

static ApplicationDbContext CreateDbContext(string connectionString)
{
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(connectionString)
        .Options;

    return new ApplicationDbContext(options);
}

static string? GetOptionValue(string[] args, string optionName)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == optionName)
            return args[i + 1];
    }
    return null;
}

static string? GetPositionalArg(string[] args, int index)
    => index < args.Length ? args[index] : null;

static string[] StripConnectionStringArgs(string[] args)
{
    var result = new List<string>();
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--connection-string" && i + 1 < args.Length)
        {
            i++; // skip value too
            continue;
        }
        result.Add(args[i]);
    }
    return result.ToArray();
}

static string[] SplitArgs(string input)
{
    var args = new List<string>();
    var inQuotes = false;
    var current = new System.Text.StringBuilder();

    foreach (var c in input)
    {
        if (c == '"')
        {
            inQuotes = !inQuotes;
        }
        else if (c == ' ' && !inQuotes)
        {
            if (current.Length > 0)
            {
                args.Add(current.ToString());
                current.Clear();
            }
        }
        else
        {
            current.Append(c);
        }
    }

    if (current.Length > 0)
        args.Add(current.ToString());

    return args.ToArray();
}
