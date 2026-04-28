using GeoSlayer.Domain.Services;
using Hangfire;

namespace GeoSlayer.Domain.Helpers
{
    public class HangfireJobSetup
    {
        public static void RegisterJobs()
        {
            // Refresh stale cells + pre-load adjacent cells near active players.
            // Runs weekly (Sunday 03:00 UTC).
            RecurringJob.AddOrUpdate<StreetImportService>(
                "street-refresh",
                service => service.RefreshStaleCellsAsync(),
                Cron.Weekly(DayOfWeek.Sunday, 3));
        }
    }
}
