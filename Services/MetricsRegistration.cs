using BookSharingApp.Data;
using System.Diagnostics.Metrics;

namespace BookSharingApp.Services;

public static class MetricsRegistration
{
    private const int MetricCacheMinutes = 2;
    private const string UnresolvedReportsMetricName = "bookshare_reports_unresolved";

    public static void RegisterBookShareMetrics(this IApplicationBuilder app, Meter meter)
    {
        var scopeFactory = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
        var logger = app.ApplicationServices.GetRequiredService<ILogger<Meter>>();

        var unresolvedReportCache = (Count: 0, Expiry: DateTime.MinValue);
        var unresolvedReportCacheLock = new object();

        meter.CreateObservableGauge(
            UnresolvedReportsMetricName,
            () =>
            {
                try
                {
                    lock (unresolvedReportCacheLock)
                    {
                        if (DateTime.UtcNow < unresolvedReportCache.Expiry)
                            return unresolvedReportCache.Count;
                    }

                    using var gaugeScope = scopeFactory.CreateScope();
                    var db = gaugeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var count = db.ChatMessageReports.Count(r => !r.IsResolved);

                    lock (unresolvedReportCacheLock)
                    {
                        unresolvedReportCache = (count, DateTime.UtcNow.AddMinutes(MetricCacheMinutes));
                    }
                    return count;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to calculate unresolved reports metric");
                    return 0;
                }
            },
            description: "Number of chat message reports that have not yet been resolved");

        logger.LogInformation("Registered unresolved chat message reports gauge");
    }
}
