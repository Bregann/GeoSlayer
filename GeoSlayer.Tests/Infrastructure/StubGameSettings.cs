using GeoSlayer.Domain.Interfaces.Api.Admin;

namespace GeoSlayer.Tests.Infrastructure
{
    /// <summary>
    /// A <see cref="IGameSettings"/> that always returns the caller's fallback.
    ///
    /// <para>Which is exactly what production does before the table has loaded, so tests see
    /// the shipped balance — the same numbers the pure-function tests assert against. A stub
    /// that returned its own values would make every pricing assertion depend on a fixture
    /// rather than on the formula.</para>
    /// </summary>
    public class StubGameSettings : IGameSettings
    {
        /// <summary>How many times <see cref="Reload"/> was called, for asserting on it.</summary>
        public int ReloadCount { get; private set; }

        public Task Reload()
        {
            ReloadCount++;
            return Task.CompletedTask;
        }

        public double Get(string key, double fallback) => fallback;

        public int GetInt(string key, int fallback) => fallback;
    }
}
