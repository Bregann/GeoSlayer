using GeoSlayer.Domain.Database.Context;
using GeoSlayer.Domain.Interfaces.Api.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GeoSlayer.Domain.Services.Admin
{
    /// <summary>
    /// Reads tunable numbers from the database, cached (Stage 18).
    ///
    /// <para>These are read on hot paths — every material price, every sync that trains
    /// Athletics — so hitting the database each time would be a query per material per
    /// request. The whole table is a few dozen small rows, so it is loaded once and held.</para>
    ///
    /// <para><b>A missing or malformed row falls back to the shipped default rather than
    /// throwing.</b> That is deliberate: these are read while a player is mid-walk, and a
    /// bad row should degrade the balance slightly, not fail the request. Validation on the
    /// write side is what keeps bad rows out; this is the belt to that braces.</para>
    ///
    /// <para>Registered as a singleton. <see cref="Reload"/> is called after an admin saves,
    /// so a change takes effect without a restart — which is the entire point of the
    /// table.</para>
    /// </summary>
    public class GameSettings(IServiceProvider services) : IGameSettings
    {
        private readonly Dictionary<string, double> _values = [];
        private readonly Lock _gate = new();
        private bool _loaded;

        /// <summary>Loads every setting from the database, replacing what is cached.</summary>
        public async Task Reload()
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            List<Database.Models.GameSetting> rows;

            try
            {
                rows = await db.GameSettings.AsNoTracking().ToListAsync();
            }
            catch (Exception ex)
            {
                // A settings table that cannot be read must not stop the API booting — every
                // caller falls back to its shipped default, which is the balance that shipped.
                Log.Error(ex, "GameSettings: could not load; defaults will be used");
                return;
            }

            var parsed = new Dictionary<string, double>();

            foreach (var row in rows)
            {
                if (double.TryParse(row.Value, out var value))
                {
                    parsed[row.Key] = value;
                }
                else
                {
                    Log.Warning(
                        "GameSettings: '{Key}' has unparseable value '{Value}'; using the default",
                        row.Key, row.Value);
                }
            }

            lock (_gate)
            {
                _values.Clear();

                foreach (var (key, value) in parsed)
                {
                    _values[key] = value;
                }

                _loaded = true;
            }

            Log.Information("GameSettings: loaded {Count} settings", parsed.Count);
        }

        /// <summary>
        /// The value for a key, or <paramref name="fallback"/> when it is missing.
        /// </summary>
        /// <param name="fallback">
        /// The shipped default. Passed by the caller rather than looked up, so a reader
        /// always has a sane number even before the table has loaded.
        /// </param>
        public double Get(string key, double fallback)
        {
            lock (_gate)
            {
                if (!_loaded)
                {
                    return fallback;
                }

                return _values.TryGetValue(key, out var value) ? value : fallback;
            }
        }

        /// <summary>The value as an integer, rounded.</summary>
        public int GetInt(string key, int fallback) => (int)Math.Round(Get(key, fallback));
    }
}
