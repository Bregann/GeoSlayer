using Hangfire;

namespace GeoSlayer.Domain.Helpers
{
    public class HangfireJobSetup
    {
        public static void RegisterJobs()
        {
            // No recurring jobs needed — POI data is imported on-demand.
        }
    }
}
